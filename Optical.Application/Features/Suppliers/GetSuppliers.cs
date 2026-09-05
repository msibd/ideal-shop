using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;

namespace Optical.Application.Features.Suppliers;

public sealed record SupplierListItem(
    int Id,
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive);

public sealed record SupplierListResult(
    IReadOnlyList<SupplierListItem> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}

public sealed class GetSuppliersHandler(IApplicationDbContext db)
{
    public const int DefaultPageSize = 20;

    public async Task<SupplierListResult> HandleAsync(
        string? search,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            page = 1;
        }

        var query = db.Suppliers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(s =>
                s.Name.ToLower().Contains(term) ||
                (s.Phone != null && s.Phone.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * DefaultPageSize)
            .Take(DefaultPageSize)
            .Select(s => new SupplierListItem(s.Id, s.Name, s.Phone, s.Email, s.Address, s.IsActive))
            .ToListAsync(cancellationToken);

        return new SupplierListResult(items, page, DefaultPageSize, totalCount);
    }
}
