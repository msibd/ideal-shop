using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;

namespace Optical.Application.Features.Reports;

public sealed record InventoryReportRow(
    string Sku,
    string Name,
    string CategoryName,
    int Quantity,
    int ReorderLevel,
    decimal PurchasePrice,
    bool IsActive)
{
    public bool IsOutOfStock => Quantity == 0;

    public bool IsLowStock => Quantity <= ReorderLevel;

    public decimal StockValue => Quantity * PurchasePrice;
}

public sealed record InventoryReportResult(
    IReadOnlyList<InventoryReportRow> Rows,
    int Page,
    int PageSize,
    int TotalCount,
    int LowStockCount,
    int OutOfStockCount,
    int TotalUnits,
    decimal TotalStockValue)
{
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}

/// <summary>
/// Stock as it stands, listed from the product side so a product that has never been
/// purchased still appears with a quantity of zero. The totals cover the whole catalogue,
/// not just the page on screen.
/// </summary>
public sealed class GetInventoryReportHandler(IApplicationDbContext db)
{
    public const int DefaultPageSize = 50;

    public async Task<InventoryReportResult> HandleAsync(
        bool lowStockOnly = false,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            page = 1;
        }

        var products = db.Products.AsNoTracking();

        var totals = await products
            .Select(p => new
            {
                Quantity = db.InventoryItems
                    .Where(i => i.ProductId == p.Id)
                    .Select(i => (int?)i.Quantity)
                    .FirstOrDefault() ?? 0,
                p.ReorderLevel,
                p.PurchasePrice
            })
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                LowStock = g.Count(r => r.Quantity <= r.ReorderLevel),
                OutOfStock = g.Count(r => r.Quantity == 0),
                Units = g.Sum(r => r.Quantity),
                Value = g.Sum(r => r.Quantity * r.PurchasePrice)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // A product with no stock row counts as zero, so it must still be treated as low stock.
        if (lowStockOnly)
        {
            products = products.Where(p =>
                (db.InventoryItems
                    .Where(i => i.ProductId == p.Id)
                    .Select(i => (int?)i.Quantity)
                    .FirstOrDefault() ?? 0) <= p.ReorderLevel);
        }

        var rows = await products
            .OrderBy(p => p.Name)
            .Skip((page - 1) * DefaultPageSize)
            .Take(DefaultPageSize)
            .Select(p => new InventoryReportRow(
                p.Sku,
                p.Name,
                p.Category.Name,
                db.InventoryItems
                    .Where(i => i.ProductId == p.Id)
                    .Select(i => (int?)i.Quantity)
                    .FirstOrDefault() ?? 0,
                p.ReorderLevel,
                p.PurchasePrice,
                p.IsActive))
            .ToListAsync(cancellationToken);

        return new InventoryReportResult(
            rows,
            page,
            DefaultPageSize,
            lowStockOnly ? totals?.LowStock ?? 0 : totals?.Count ?? 0,
            totals?.LowStock ?? 0,
            totals?.OutOfStock ?? 0,
            totals?.Units ?? 0,
            totals?.Value ?? 0m);
    }
}
