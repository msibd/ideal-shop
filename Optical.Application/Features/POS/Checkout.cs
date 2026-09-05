using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Entities;
using Optical.Domain.Enums;
using Optical.Domain.Exceptions;

namespace Optical.Application.Features.POS;

/// <summary>A cart line as it arrives from the browser: product and quantity only, never money.</summary>
public sealed record CheckoutItemCommand(int ProductId, int Quantity);

public sealed record CheckoutResult(
    int SaleId,
    string InvoiceNumber,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TotalAmount);

public sealed record CheckoutCommand(
    int? CustomerId,
    PaymentMethod PaymentMethod,
    IReadOnlyList<CheckoutItemCommand> Items);

/// <summary>
/// Turns a cart into a Sale, its SaleItems, a Payment and the matching stock decrease.
/// Everything is written by a single SaveChangesAsync, which EF Core runs inside one database
/// transaction, so a sale can never be saved without its payment or its stock movement.
/// Prices and totals are read and calculated here; the browser is never trusted with either.
/// </summary>
public sealed class CheckoutHandler(IApplicationDbContext db)
{
    /// <summary>How many times a clash of invoice numbers between two tills is worth retrying.</summary>
    private const int NumberingAttempts = 3;

    public async Task<CheckoutResult> HandleAsync(
        CheckoutCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Items.Count == 0)
        {
            throw new DomainException("The cart is empty.");
        }

        if (!Enum.IsDefined(command.PaymentMethod))
        {
            throw new DomainException("Select a valid payment method.");
        }

        var productIds = command.Items.Select(i => i.ProductId).ToList();

        if (productIds.Distinct().Count() != productIds.Count)
        {
            throw new DomainException("The same product is listed more than once. Combine it into a single line.");
        }

        await ValidateCustomerAsync(command.CustomerId, cancellationToken);

        // Read-only: the sale price is looked up here, never written back.
        var products = await db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var stockRows = await db.InventoryItems
            .Where(i => productIds.Contains(i.ProductId))
            .ToDictionaryAsync(i => i.ProductId, cancellationToken);

        // One "today" for the whole sale, so a checkout running over midnight cannot price
        // its first line inside the offer and its last line outside it.
        var today = DateOnly.FromDateTime(DateTime.Now);

        var sale = new Sale
        {
            CustomerId = command.CustomerId,
            Status = SaleStatus.Completed
        };

        foreach (var item in command.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                throw new DomainException("One of the products in the cart no longer exists.");
            }

            if (!product.IsActive)
            {
                throw new DomainException($"Product \"{product.Name}\" is inactive and cannot be sold.");
            }

            if (item.Quantity <= 0)
            {
                throw new DomainException($"Quantity for \"{product.Name}\" must be greater than zero.");
            }

            if (!stockRows.TryGetValue(item.ProductId, out var stock) || stock.Quantity < item.Quantity)
            {
                var available = stock?.Quantity ?? 0;

                throw new DomainException(
                    $"Not enough stock for \"{product.Name}\". In stock: {available}, requested: {item.Quantity}.");
            }

            stock.Quantity -= item.Quantity;

            // Both the price and the discount are the product's own, read here and not sent by
            // the browser. An offer that has expired simply returns zero.
            var lineGross = item.Quantity * product.SalePrice;
            var percent = product.DiscountPercentOn(today);
            var lineDiscount = decimal.Round(
                lineGross * percent / 100m,
                2,
                MidpointRounding.AwayFromZero);

            sale.Items.Add(new SaleItem
            {
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = product.SalePrice,
                DiscountPercent = percent,
                DiscountAmount = lineDiscount,
                LineTotal = lineGross - lineDiscount
            });
        }

        sale.SubTotal = sale.Items.Sum(i => i.Quantity * i.UnitPrice);
        sale.DiscountAmount = sale.Items.Sum(i => i.DiscountAmount);
        sale.TotalAmount = sale.Items.Sum(i => i.LineTotal);

        sale.Payment = new Payment
        {
            Amount = sale.TotalAmount,
            Method = command.PaymentMethod
        };

        db.Sales.Add(sale);

        await SaveWithInvoiceNumberAsync(sale, cancellationToken);

        return new CheckoutResult(
            sale.Id,
            sale.InvoiceNumber,
            sale.SubTotal,
            sale.DiscountAmount,
            sale.TotalAmount);
    }

    /// <summary>
    /// The invoice number is the day's next in sequence, so two tills closing at the same
    /// instant can pick the same one. The unique index rejects the loser rather than letting
    /// a duplicate through, and the sale is renumbered and retried instead of being lost.
    /// Every attempt is still one SaveChangesAsync, so a failed one saves nothing at all.
    /// </summary>
    private async Task SaveWithInvoiceNumberAsync(Sale sale, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        for (var attempt = 1; ; attempt++)
        {
            sale.InvoiceNumber = await NextInvoiceNumberAsync(today, cancellationToken);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException) when (attempt < NumberingAttempts)
            {
                // Nothing was written, so the pending sale can simply be renumbered.
            }
        }
    }

    private async Task<string> NextInvoiceNumberAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var dayPrefix = InvoiceNumber.DayPrefix(InvoiceNumber.Prefix, date);

        // Fixed-width sequences sort as text, so the highest string is the highest number.
        var latest = await db.Sales
            .AsNoTracking()
            .Where(s => s.InvoiceNumber.StartsWith(dayPrefix))
            .OrderByDescending(s => s.InvoiceNumber)
            .Select(s => s.InvoiceNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var sequence = latest is null ? 1 : InvoiceNumber.SequenceOf(latest) + 1;

        return InvoiceNumber.Build(InvoiceNumber.Prefix, date, sequence);
    }

    private async Task ValidateCustomerAsync(int? customerId, CancellationToken cancellationToken)
    {
        if (customerId is null)
        {
            return;
        }

        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken)
            ?? throw new DomainException("The selected customer no longer exists.");

        if (!customer.IsActive)
        {
            throw new DomainException($"Customer \"{customer.Name}\" is inactive.");
        }
    }
}
