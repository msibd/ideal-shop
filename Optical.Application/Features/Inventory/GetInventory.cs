using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;

namespace Optical.Application.Features.Inventory;

public sealed record InventoryListItem(
    int ProductId,
    string Sku,
    string Name,
    string CategoryName,
    string BrandName,
    int Quantity,
    int ReorderLevel,
    bool IsActive)
{
    public bool IsLowStock => Quantity <= ReorderLevel;
}

public sealed record InventoryListResult(
    IReadOnlyList<InventoryListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int LowStockCount)
{
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}

/// <summary>
/// Stock is listed from the product side, so products that have never been purchased
/// still show up with a quantity of zero.
/// </summary>
public sealed class GetInventoryHandler(IApplicationDbContext db)
{
    public const int DefaultPageSize = 20;

    public async Task<InventoryListResult> HandleAsync(
        string? search,
        bool lowStockOnly = false,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            page = 1;
        }

        var query = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term) || p.Sku.ToLower().Contains(term));
        }

        // A product with no stock row counts as zero, so it must still be treated as low stock.
        var lowStock = query.Where(p =>
            (db.InventoryItems
                .Where(i => i.ProductId == p.Id)
                .Select(i => (int?)i.Quantity)
                .FirstOrDefault() ?? 0) <= p.ReorderLevel);

        var lowStockCount = await lowStock.CountAsync(cancellationToken);

        if (lowStockOnly)
        {
            query = lowStock;
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * DefaultPageSize)
            .Take(DefaultPageSize)
            .Select(p => new InventoryListItem(
                p.Id,
                p.Sku,
                p.Name,
                p.Category.Name,
                p.Brand.Name,
                db.InventoryItems
                    .Where(i => i.ProductId == p.Id)
                    .Select(i => (int?)i.Quantity)
                    .FirstOrDefault() ?? 0,
                p.ReorderLevel,
                p.IsActive))
            .ToListAsync(cancellationToken);

        return new InventoryListResult(items, page, DefaultPageSize, totalCount, lowStockCount);
    }
}
