using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;

namespace Optical.Application.Features.Categories;

public sealed record CategoryDetails(int Id, string Name, string? Description, bool IsActive);

public sealed class GetCategoryByIdHandler(IApplicationDbContext db)
{
    public Task<CategoryDetails?> HandleAsync(int id, CancellationToken cancellationToken = default) =>
        db.Categories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDetails(c.Id, c.Name, c.Description, c.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
}
