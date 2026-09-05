using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;

namespace Optical.Application.Features.Customers;

public sealed record CustomerListItem(
    int Id,
    string Name,
    string Phone,
    string? Email,
    string? Address,
    bool IsActive);

public sealed record CustomerListResult(
    IReadOnlyList<CustomerListItem> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}

public sealed class GetCustomersHandler(IApplicationDbContext db)
{
    public const int DefaultPageSize = 20;

    public async Task<CustomerListResult> HandleAsync(
        string? search,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            page = 1;
        }

        var query = db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(term) ||
                c.Phone.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * DefaultPageSize)
            .Take(DefaultPageSize)
            .Select(c => new CustomerListItem(c.Id, c.Name, c.Phone, c.Email, c.Address, c.IsActive))
            .ToListAsync(cancellationToken);

        return new CustomerListResult(items, page, DefaultPageSize, totalCount);
    }
}
