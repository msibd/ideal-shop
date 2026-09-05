using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Entities;

namespace Optical.Application.Features.Products;

public sealed record ProductListItem(
    int Id,
    string Sku,
    string? Barcode,
    string Name,
    string CategoryName,
    string BrandName,
    decimal PurchasePrice,
    decimal SalePrice,
    int ReorderLevel,
    decimal DiscountPercent,
    DateOnly? DiscountStartsOn,
    DateOnly? DiscountEndsOn,
    bool IsActive)
{
    public bool IsDiscountRunning => Product.IsDiscountRunning(
        DiscountPercent,
        DiscountStartsOn,
        DiscountEndsOn,
        DateOnly.FromDateTime(DateTime.Now));

    /// <summary>Set up, but either not started yet or already over.</summary>
    public bool HasDormantDiscount => DiscountPercent > 0 && !IsDiscountRunning;
}

public sealed record ProductListResult(
    IReadOnlyList<ProductListItem> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}

public sealed class GetProductsHandler(IApplicationDbContext db)
{
    public const int DefaultPageSize = 20;

    public async Task<ProductListResult> HandleAsync(
        string? search,
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
            // Scanning into the search box finds the product, the same as typing its name.
            query = query.Where(p =>
                p.Name.ToLower().Contains(term)
                || p.Sku.ToLower().Contains(term)
                || (p.Barcode != null && p.Barcode.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * DefaultPageSize)
            .Take(DefaultPageSize)
            .Select(p => new ProductListItem(
                p.Id,
                p.Sku,
                p.Barcode,
                p.Name,
                p.Category.Name,
                p.Brand.Name,
                p.PurchasePrice,
                p.SalePrice,
                p.ReorderLevel,
                p.DiscountPercent,
                p.DiscountStartsOn,
                p.DiscountEndsOn,
                p.IsActive))
            .ToListAsync(cancellationToken);

        return new ProductListResult(items, page, DefaultPageSize, totalCount);
    }
}
