using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Exceptions;

namespace Optical.Application.Features.Brands;

public sealed record UpdateBrandCommand(int Id, string Name, string? Description, bool IsActive);

public sealed class UpdateBrandHandler(IApplicationDbContext db)
{
    public async Task HandleAsync(UpdateBrandCommand command, CancellationToken cancellationToken = default)
    {
        var brand = await db.Brands.FirstOrDefaultAsync(b => b.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException($"Brand {command.Id} was not found.");

        var name = command.Name.Trim();
        var normalized = name.ToLower();

        if (await db.Brands.AnyAsync(
                b => b.Id != command.Id && b.Name.ToLower() == normalized, cancellationToken))
        {
            throw new DomainException($"A brand named \"{name}\" already exists.");
        }

        brand.Name = name;
        brand.Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();
        brand.IsActive = command.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
