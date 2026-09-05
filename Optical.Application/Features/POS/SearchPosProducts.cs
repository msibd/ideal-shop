using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Entities;

namespace Optical.Application.Features.POS;

public sealed record PosProduct(
    int Id,
    string Sku,
    string? Barcode,
    string Name,
    decimal SalePrice,
    int Stock,
    decimal DiscountPercent,
    DateOnly? DiscountStartsOn,
    DateOnly? DiscountEndsOn)
{
    /// <summary>
    /// The percentage the till should show today. Display only — checkout works this out
    /// again from the product itself, so a stale page cannot sell at yesterday's offer.
    /// </summary>
    public decimal ActiveDiscountPercent =>
        Product.IsDiscountRunning(
            DiscountPercent,
            DiscountStartsOn,
            DiscountEndsOn,
            DateOnly.FromDateTime(DateTime.Now))
            ? DiscountPercent
            : 0m;

    public decimal EffectivePrice =>
        decimal.Round(
            SalePrice * (100m - ActiveDiscountPercent) / 100m,
            2,
            MidpointRounding.AwayFromZero);
}

/// <summary>The page of products shown on the till, and how many matched in total.</summary>
public sealed record PosProductResults(IReadOnlyList<PosProduct> Items, int TotalCount);

public enum PosProductSort
{
    NameAscending = 1,
    NameDescending = 2,
    PriceLowToHigh = 3,
    PriceHighToLow = 4
}

/// <summary>
/// Feeds the POS product list and re-builds the cart after a rejected checkout.
/// Prices and stock always come from the database, never from the browser.
/// </summary>
public sealed class SearchPosProductsHandler(IApplicationDbContext db)
{
    public const int MaxResults = 20;

    public async Task<PosProductResults> SearchAsync(
        string? term,
        int? categoryId = null,
        PosProductSort sort = PosProductSort.NameAscending,
        CancellationToken cancellationToken = default)
    {
        var query = db.Products.AsNoTracking().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var search = term.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(search) ||
                p.Sku.ToLower().Contains(search) ||
                (p.Barcode != null && p.Barcode.ToLower().Contains(search)));
        }

        if (categoryId is not null)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Order and page the products themselves, then project. Ordering the projection
        // instead is not translatable, because the projection carries the stock subquery.
        var items = await Project(Sort(query, sort).Take(MaxResults))
            .ToListAsync(cancellationToken);

        return new PosProductResults(items, totalCount);
    }

    public async Task<IReadOnlyList<PosProduct>> GetByIdsAsync(
        IReadOnlyList<int> productIds,
        CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
        {
            return [];
        }

        return await Project(db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The one product carrying this exact barcode, or null. A scan has to be an exact match:
    /// a partial match would ring up the wrong frame, which is worse than finding nothing.
    /// Inactive products are excluded, so a discontinued line cannot be scanned into a sale.
    /// </summary>
    public async Task<PosProduct?> FindByBarcodeAsync(
        string? barcode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return null;
        }

        var scanned = barcode.Trim().ToUpperInvariant();

        return await Project(db.Products.AsNoTracking().Where(p => p.IsActive && p.Barcode == scanned))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static IQueryable<Product> Sort(IQueryable<Product> query, PosProductSort sort) => sort switch
    {
        PosProductSort.NameDescending => query.OrderByDescending(p => p.Name),
        PosProductSort.PriceLowToHigh => query.OrderBy(p => p.SalePrice).ThenBy(p => p.Name),
        PosProductSort.PriceHighToLow => query.OrderByDescending(p => p.SalePrice).ThenBy(p => p.Name),
        _ => query.OrderBy(p => p.Name)
    };

    private IQueryable<PosProduct> Project(IQueryable<Product> query) =>
        query.Select(p => new PosProduct(
            p.Id,
            p.Sku,
            p.Barcode,
            p.Name,
            p.SalePrice,
            db.InventoryItems.Where(i => i.ProductId == p.Id).Select(i => i.Quantity).FirstOrDefault(),
            p.DiscountPercent,
            p.DiscountStartsOn,
            p.DiscountEndsOn));
}
