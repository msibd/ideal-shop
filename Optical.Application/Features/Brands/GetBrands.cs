using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;

namespace Optical.Application.Features.Brands;

public sealed record BrandListItem(int Id, string Name, string? Description, bool IsActive);

public sealed class GetBrandsHandler(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<BrandListItem>> HandleAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = db.Brands.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(b => b.Name.ToLower().Contains(term));
        }

        return await query
            .OrderBy(b => b.Name)
            .Select(b => new BrandListItem(b.Id, b.Name, b.Description, b.IsActive))
            .ToListAsync(cancellationToken);
    }
}
