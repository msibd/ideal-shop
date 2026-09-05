using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Entities;
using Optical.Domain.Exceptions;

namespace Optical.Application.Features.Purchases;

public sealed record CreatePurchaseItemCommand(int ProductId, int Quantity, decimal PurchasePrice);

public sealed record CreatePurchaseCommand(
    int SupplierId,
    DateOnly PurchaseDate,
    string? InvoiceNumber,
    IReadOnlyList<CreatePurchaseItemCommand> Items);

/// <summary>
/// Creates the purchase, its items and the resulting stock increase.
/// Everything is written by a single SaveChangesAsync, which EF Core runs inside one
/// database transaction, so a purchase can never be saved without its stock movement.
/// </summary>
public sealed class CreatePurchaseHandler(IApplicationDbContext db)
{
    public async Task<int> HandleAsync(CreatePurchaseCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Items.Count == 0)
        {
            throw new DomainException("Add at least one product to the purchase.");
        }

        var supplier = await db.Suppliers
            .FirstOrDefaultAsync(s => s.Id == command.SupplierId, cancellationToken)
            ?? throw new DomainException("The selected supplier no longer exists.");

        if (!supplier.IsActive)
        {
            throw new DomainException($"Supplier \"{supplier.Name}\" is inactive.");
        }

        var productIds = command.Items.Select(i => i.ProductId).ToList();

        if (productIds.Distinct().Count() != productIds.Count)
        {
            throw new DomainException("The same product is listed more than once. Combine it into a single line.");
        }

        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var purchase = new Purchase
        {
            SupplierId = supplier.Id,
            PurchaseDate = command.PurchaseDate,
            InvoiceNumber = string.IsNullOrWhiteSpace(command.InvoiceNumber)
                ? null
                : command.InvoiceNumber.Trim()
        };

        foreach (var item in command.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                throw new DomainException("One of the selected products no longer exists.");
            }

            if (!product.IsActive)
            {
                throw new DomainException($"Product \"{product.Name}\" is inactive and cannot be purchased.");
            }

            if (item.Quantity <= 0)
            {
                throw new DomainException($"Quantity for \"{product.Name}\" must be greater than zero.");
            }

            if (item.PurchasePrice < 0)
            {
                throw new DomainException($"Purchase price for \"{product.Name}\" cannot be negative.");
            }

            purchase.Items.Add(new PurchaseItem
            {
                ProductId = product.Id,
                Quantity = item.Quantity,
                PurchasePrice = item.PurchasePrice,
                LineTotal = item.Quantity * item.PurchasePrice
            });
        }

        // The total is always calculated here, never taken from the browser.
        purchase.TotalAmount = purchase.Items.Sum(i => i.LineTotal);

        db.Purchases.Add(purchase);

        await IncreaseStockAsync(purchase.Items, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return purchase.Id;
    }

    private async Task IncreaseStockAsync(List<PurchaseItem> items, CancellationToken cancellationToken)
    {
        var productIds = items.Select(i => i.ProductId).ToList();

        var stockRows = await db.InventoryItems
            .Where(i => productIds.Contains(i.ProductId))
            .ToDictionaryAsync(i => i.ProductId, cancellationToken);

        foreach (var item in items)
        {
            if (stockRows.TryGetValue(item.ProductId, out var stock))
            {
                stock.Quantity += item.Quantity;
            }
            else
            {
                db.InventoryItems.Add(new InventoryItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                });
            }
        }
    }
}
