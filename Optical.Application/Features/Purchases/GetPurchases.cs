using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;

namespace Optical.Application.Features.Purchases;

public sealed record PurchaseListItem(
    int Id,
    DateOnly PurchaseDate,
    string SupplierName,
    string? InvoiceNumber,
    int LineCount,
    int TotalQuantity,
    decimal TotalAmount);

public sealed record PurchaseListResult(
    IReadOnlyList<PurchaseListItem> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}

public sealed class GetPurchasesHandler(IApplicationDbContext db)
{
    public const int DefaultPageSize = 20;

    public async Task<PurchaseListResult> HandleAsync(
        string? search,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            page = 1;
        }

        var query = db.Purchases.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p =>
                p.Supplier.Name.ToLower().Contains(term) ||
                (p.InvoiceNumber != null && p.InvoiceNumber.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.PurchaseDate)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * DefaultPageSize)
            .Take(DefaultPageSize)
            .Select(p => new PurchaseListItem(
                p.Id,
                p.PurchaseDate,
                p.Supplier.Name,
                p.InvoiceNumber,
                p.Items.Count,
                p.Items.Sum(i => i.Quantity),
                p.TotalAmount))
            .ToListAsync(cancellationToken);

        return new PurchaseListResult(items, page, DefaultPageSize, totalCount);
    }
}
