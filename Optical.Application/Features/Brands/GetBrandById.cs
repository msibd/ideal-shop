using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;

namespace Optical.Application.Features.Brands;

public sealed record BrandDetails(int Id, string Name, string? Description, bool IsActive);

public sealed class GetBrandByIdHandler(IApplicationDbContext db)
{
    public Task<BrandDetails?> HandleAsync(int id, CancellationToken cancellationToken = default) =>
        db.Brands
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => new BrandDetails(b.Id, b.Name, b.Description, b.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
}
