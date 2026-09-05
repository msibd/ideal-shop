using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Entities;
using Optical.Domain.Exceptions;

namespace Optical.Application.Features.Brands;

public sealed record CreateBrandCommand(string Name, string? Description);

public sealed class CreateBrandHandler(IApplicationDbContext db)
{
    public async Task<int> HandleAsync(CreateBrandCommand command, CancellationToken cancellationToken = default)
    {
        var name = command.Name.Trim();
        var normalized = name.ToLower();

        if (await db.Brands.AnyAsync(b => b.Name.ToLower() == normalized, cancellationToken))
        {
            throw new DomainException($"A brand named \"{name}\" already exists.");
        }

        var brand = new Brand
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            IsActive = true
        };

        db.Brands.Add(brand);
        await db.SaveChangesAsync(cancellationToken);

        return brand.Id;
    }
}
