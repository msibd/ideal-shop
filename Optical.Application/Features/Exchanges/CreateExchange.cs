using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Application.Features.POS;
using Optical.Domain.Entities;
using Optical.Domain.Enums;
using Optical.Domain.Exceptions;

namespace Optical.Application.Features.Exchanges;

/// <summary>One line coming back: which sale line, and how many. Never a price.</summary>
public sealed record ExchangeReturnCommand(int SaleItemId, int Quantity);

/// <summary>One line going out: which product, and how many. Never a price.</summary>
public sealed record ExchangeReplacementCommand(int ProductId, int Quantity);

public sealed record CreateExchangeCommand(
    int SaleId,
    ExchangeReason Reason,
    string? Note,
    PaymentMethod? PaymentMethod,
    IReadOnlyList<ExchangeReturnCommand> Returned,
    IReadOnlyList<ExchangeReplacementCommand> Replacements);

public sealed record CreateExchangeResult(
    int ExchangeId,
    string ExchangeNumber,
    decimal ReturnedAmount,
    decimal ReplacementAmount,
    decimal AmountPaid);

/// <summary>
/// Swaps goods for other goods against an earlier sale: the old lines come back into stock,
/// the new ones go out, and the customer covers any shortfall. The shop never pays out, so a
/// replacement worth less than what came back is refused outright.
///
/// Everything is written by a single SaveChangesAsync, which EF Core runs inside one database
/// transaction, so stock can never move one way without moving the other. All prices and
/// totals are read and calculated here; the browser is never trusted with either.
/// </summary>
public sealed class CreateExchangeHandler(IApplicationDbContext db)
{
    /// <summary>How many times a clash of exchange numbers between two tills is worth retrying.</summary>
    private const int NumberingAttempts = 3;

    private const string NumberPrefix = "EXC";

    public async Task<CreateExchangeResult> HandleAsync(
        CreateExchangeCommand command,
        CancellationToken cancellationToken = default)
    {
        // A form posts every line, so the empty ones are dropped before anything is judged.
        var returned = command.Returned.Where(i => i.Quantity > 0).ToList();
        var replacements = command.Replacements.Where(i => i.Quantity > 0).ToList();

        if (returned.Count == 0)
        {
            throw new DomainException("Enter a quantity for at least one item coming back.");
        }

        if (replacements.Count == 0)
        {
            throw new DomainException("Choose at least one replacement product. Goods can only be swapped, not refunded.");
        }

        if (!Enum.IsDefined(command.Reason))
        {
            throw new DomainException("Select why the goods are being exchanged.");
        }

        // "Other" tells the shop nothing on its own, so it has to carry an explanation.
        if (command.Reason == ExchangeReason.Other && string.IsNullOrWhiteSpace(command.Note))
        {
            throw new DomainException("Describe the reason when choosing \"Other\".");
        }

        var saleItemIds = returned.Select(i => i.SaleItemId).ToList();

        if (saleItemIds.Distinct().Count() != saleItemIds.Count)
        {
            throw new DomainException("The same sale line is listed more than once.");
        }

        var replacementIds = replacements.Select(i => i.ProductId).ToList();

        if (replacementIds.Distinct().Count() != replacementIds.Count)
        {
            throw new DomainException("The same replacement product is listed more than once. Combine it into a single line.");
        }

        var sale = await db.Sales
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == command.SaleId, cancellationToken)
            ?? throw new DomainException("That sale no longer exists.");

        if (sale.Status != SaleStatus.Completed)
        {
            throw new DomainException("Only a completed sale can be exchanged against.");
        }

        var exchange = new Exchange
        {
            SaleId = sale.Id,
            Reason = command.Reason,
            Note = string.IsNullOrWhiteSpace(command.Note) ? null : command.Note.Trim()
        };

        await AddReturnedLinesAsync(exchange, command.SaleId, returned, cancellationToken);
        await AddReplacementLinesAsync(exchange, replacements, cancellationToken);

        exchange.ReturnedAmount = exchange.ReturnedItems.Sum(i => i.LineTotal);
        exchange.ReplacementAmount = exchange.ReplacementItems.Sum(i => i.LineTotal);

        // The shop does not refund: the replacement has to be worth at least as much.
        if (exchange.ReplacementAmount < exchange.ReturnedAmount)
        {
            throw new DomainException(
                $"The replacement must be worth at least {exchange.ReturnedAmount:N2}. "
                + $"It currently comes to {exchange.ReplacementAmount:N2}. "
                + "Add another item or choose a higher-priced product.");
        }

        exchange.AmountPaid = exchange.ReplacementAmount - exchange.ReturnedAmount;

        if (exchange.AmountPaid > 0)
        {
            if (command.PaymentMethod is null)
            {
                throw new DomainException(
                    $"The customer owes an adjustment of {exchange.AmountPaid:N2}. "
                    + "Select how it was paid.");
            }

            if (!Enum.IsDefined(command.PaymentMethod.Value))
            {
                throw new DomainException("Select a valid payment method.");
            }

            exchange.PaymentMethod = command.PaymentMethod;
        }

        db.Exchanges.Add(exchange);

        await MoveStockAsync(exchange, cancellationToken);

        await SaveWithExchangeNumberAsync(exchange, cancellationToken);

        return new CreateExchangeResult(
            exchange.Id,
            exchange.ExchangeNumber,
            exchange.ReturnedAmount,
            exchange.ReplacementAmount,
            exchange.AmountPaid);
    }

    /// <summary>Validates what is coming back against what was sold and not yet exchanged.</summary>
    private async Task AddReturnedLinesAsync(
        Exchange exchange,
        int saleId,
        List<ExchangeReturnCommand> returned,
        CancellationToken cancellationToken)
    {
        var saleItemIds = returned.Select(i => i.SaleItemId).ToList();

        // Read-only: the sale line is the source of truth for price and for how many were sold.
        var saleItems = await db.SaleItems
            .AsNoTracking()
            .Where(i => i.SaleId == saleId && saleItemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        var alreadyExchanged = await db.ExchangeReturnedItems
            .AsNoTracking()
            .Where(r => saleItemIds.Contains(r.SaleItemId))
            .GroupBy(r => r.SaleItemId)
            .Select(g => new { SaleItemId = g.Key, Quantity = g.Sum(r => r.Quantity) })
            .ToDictionaryAsync(x => x.SaleItemId, x => x.Quantity, cancellationToken);

        var names = await db.Products
            .AsNoTracking()
            .Where(p => saleItems.Values.Select(i => i.ProductId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        foreach (var item in returned)
        {
            if (!saleItems.TryGetValue(item.SaleItemId, out var saleItem))
            {
                throw new DomainException("One of the lines does not belong to this sale.");
            }

            var name = names.GetValueOrDefault(saleItem.ProductId, "this product");

            alreadyExchanged.TryGetValue(item.SaleItemId, out var exchangedSoFar);
            var remaining = saleItem.Quantity - exchangedSoFar;

            if (remaining <= 0)
            {
                throw new DomainException($"\"{name}\" has already been exchanged in full.");
            }

            if (item.Quantity > remaining)
            {
                throw new DomainException(
                    $"Cannot exchange {item.Quantity} of \"{name}\". Only {remaining} of the {saleItem.Quantity} sold "
                    + "can still be exchanged.");
            }

            // Valued at the price the customer actually paid, not today's price and not the
            // list price: a discounted line refunds the discounted rate, or the shop would hand
            // back more than it took. The last unit returned carries any rounding remainder,
            // so returning a line in full always refunds exactly what was charged for it.
            var paidPerUnit = decimal.Round(
                saleItem.LineTotal / saleItem.Quantity,
                2,
                MidpointRounding.AwayFromZero);

            var refund = item.Quantity == remaining
                ? saleItem.LineTotal - paidPerUnit * exchangedSoFar
                : paidPerUnit * item.Quantity;

            exchange.ReturnedItems.Add(new ExchangeReturnedItem
            {
                SaleItemId = saleItem.Id,
                ProductId = saleItem.ProductId,
                Quantity = item.Quantity,
                UnitPrice = paidPerUnit,
                LineTotal = refund
            });
        }
    }

    /// <summary>Validates what is going out: real, active, and actually on the shelf.</summary>
    private async Task AddReplacementLinesAsync(
        Exchange exchange,
        List<ExchangeReplacementCommand> replacements,
        CancellationToken cancellationToken)
    {
        var productIds = replacements.Select(i => i.ProductId).ToList();

        var products = await db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var stockRows = await db.InventoryItems
            .AsNoTracking()
            .Where(i => productIds.Contains(i.ProductId))
            .ToDictionaryAsync(i => i.ProductId, i => i.Quantity, cancellationToken);

        foreach (var item in replacements)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                throw new DomainException("One of the replacement products no longer exists.");
            }

            if (!product.IsActive)
            {
                throw new DomainException($"Product \"{product.Name}\" is inactive and cannot be given out.");
            }

            var available = stockRows.GetValueOrDefault(item.ProductId);

            if (available < item.Quantity)
            {
                throw new DomainException(
                    $"Not enough stock for \"{product.Name}\". In stock: {available}, requested: {item.Quantity}.");
            }

            // The unit price is the current database sale price, whatever the browser sent.
            exchange.ReplacementItems.Add(new ExchangeReplacementItem
            {
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = product.SalePrice,
                LineTotal = item.Quantity * product.SalePrice
            });
        }
    }

    /// <summary>
    /// Returned goods go back on the shelf and replacements come off it. A product can appear
    /// on both sides — swapping one frame for two of the same — so the net movement per
    /// product is applied once, and only then checked for going negative.
    /// </summary>
    private async Task MoveStockAsync(Exchange exchange, CancellationToken cancellationToken)
    {
        var movements = new Dictionary<int, int>();

        foreach (var item in exchange.ReturnedItems)
        {
            movements[item.ProductId] = movements.GetValueOrDefault(item.ProductId) + item.Quantity;
        }

        foreach (var item in exchange.ReplacementItems)
        {
            movements[item.ProductId] = movements.GetValueOrDefault(item.ProductId) - item.Quantity;
        }

        var productIds = movements.Keys.ToList();

        var stockRows = await db.InventoryItems
            .Where(i => productIds.Contains(i.ProductId))
            .ToDictionaryAsync(i => i.ProductId, cancellationToken);

        var names = await db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        foreach (var (productId, change) in movements)
        {
            if (stockRows.TryGetValue(productId, out var stock))
            {
                if (stock.Quantity + change < 0)
                {
                    var name = names.GetValueOrDefault(productId, "this product");

                    throw new DomainException(
                        $"Not enough stock for \"{name}\". In stock: {stock.Quantity}, needed: {-change}.");
                }

                stock.Quantity += change;
            }
            else if (change < 0)
            {
                var name = names.GetValueOrDefault(productId, "this product");

                throw new DomainException($"\"{name}\" has no stock on record.");
            }
            else
            {
                db.InventoryItems.Add(new InventoryItem { ProductId = productId, Quantity = change });
            }
        }
    }

    /// <summary>
    /// The exchange number is the day's next in sequence, so two tills closing at the same
    /// instant can pick the same one. The unique index rejects the loser rather than letting
    /// a duplicate through, and the exchange is renumbered and retried instead of being lost.
    /// Every attempt is still one SaveChangesAsync, so a failed one saves nothing at all.
    /// </summary>
    private async Task SaveWithExchangeNumberAsync(Exchange exchange, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        for (var attempt = 1; ; attempt++)
        {
            exchange.ExchangeNumber = await NextExchangeNumberAsync(today, cancellationToken);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException) when (attempt < NumberingAttempts)
            {
                // Nothing was written, so the pending exchange can simply be renumbered.
            }
        }
    }

    private async Task<string> NextExchangeNumberAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var dayPrefix = InvoiceNumber.DayPrefix(NumberPrefix, date);

        // Fixed-width sequences sort as text, so the highest string is the highest number.
        var latest = await db.Exchanges
            .AsNoTracking()
            .Where(e => e.ExchangeNumber.StartsWith(dayPrefix))
            .OrderByDescending(e => e.ExchangeNumber)
            .Select(e => e.ExchangeNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var sequence = latest is null ? 1 : InvoiceNumber.SequenceOf(latest) + 1;

        return InvoiceNumber.Build(NumberPrefix, date, sequence);
    }
}
