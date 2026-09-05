using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Enums;

namespace Optical.Application.Features.Exchanges;

public sealed record ExchangeableLine(
    int SaleItemId,
    string Sku,
    string ProductName,
    int QuantitySold,
    int QuantityAlreadyExchanged,
    /// <summary>What the customer paid per unit on this line, after any discount.</summary>
    decimal UnitPrice)
{
    public int QuantityExchangeable => QuantitySold - QuantityAlreadyExchanged;

    public bool IsFullyExchanged => QuantityExchangeable == 0;
}

public sealed record ReplacementOption(int Id, string Sku, string Name, decimal SalePrice, int InStock);

public sealed record ExchangeableSale(
    int SaleId,
    string InvoiceNumber,
    DateTime SoldAt,
    string? CustomerName,
    decimal SaleTotal,
    IReadOnlyList<ExchangeableLine> Lines,
    IReadOnlyList<ReplacementOption> Replacements)
{
    public bool HasAnythingLeft => Lines.Any(l => !l.IsFullyExchanged);
}

/// <summary>
/// A sale as the exchange desk needs to see it: every line with how much of it can still be
/// handed back, plus the products available to swap into. A read model for the form only —
/// every limit is enforced again on save, because the browser is never trusted.
/// </summary>
public sealed class GetExchangeableSaleHandler(IApplicationDbContext db)
{
    public async Task<ExchangeableSale?> HandleAsync(int saleId, CancellationToken cancellationToken = default)
    {
        var sale = await db.Sales
            .AsNoTracking()
            .Where(s => s.Id == saleId && s.Status == SaleStatus.Completed)
            .Select(s => new
            {
                s.Id,
                s.InvoiceNumber,
                s.CreatedAt,
                CustomerName = s.Customer == null ? null : s.Customer.Name,
                s.TotalAmount,
                Lines = s.Items
                    .OrderBy(i => i.Id)
                    .Select(i => new ExchangeableLine(
                        i.Id,
                        i.Product.Sku,
                        i.Product.Name,
                        i.Quantity,
                        db.ExchangeReturnedItems
                            .Where(r => r.SaleItemId == i.Id)
                            .Sum(r => (int?)r.Quantity) ?? 0,
                        i.LineTotal / i.Quantity))
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (sale is null)
        {
            return null;
        }

        // Only what the shop can actually hand over: active, and physically in stock.
        var replacements = await db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Where(p => (db.InventoryItems
                .Where(i => i.ProductId == p.Id)
                .Select(i => (int?)i.Quantity)
                .FirstOrDefault() ?? 0) > 0)
            .OrderBy(p => p.Name)
            .Select(p => new ReplacementOption(
                p.Id,
                p.Sku,
                p.Name,
                p.SalePrice,
                db.InventoryItems
                    .Where(i => i.ProductId == p.Id)
                    .Select(i => (int?)i.Quantity)
                    .FirstOrDefault() ?? 0))
            .ToListAsync(cancellationToken);

        return new ExchangeableSale(
            sale.Id,
            sale.InvoiceNumber,
            sale.CreatedAt,
            sale.CustomerName,
            sale.TotalAmount,
            sale.Lines,
            replacements);
    }
}
