using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;

namespace Optical.Application.Features.Categories;

public sealed record CategoryListItem(int Id, string Name, string? Description, bool IsActive);

public sealed class GetCategoriesHandler(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<CategoryListItem>> HandleAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = db.Categories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(term));
        }

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new CategoryListItem(c.Id, c.Name, c.Description, c.IsActive))
            .ToListAsync(cancellationToken);
    }
}
