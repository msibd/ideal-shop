using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Exceptions;

namespace Optical.Application.Features.Categories;

public sealed record UpdateCategoryCommand(int Id, string Name, string? Description, bool IsActive);

public sealed class UpdateCategoryHandler(IApplicationDbContext db)
{
    public async Task HandleAsync(UpdateCategoryCommand command, CancellationToken cancellationToken = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException($"Category {command.Id} was not found.");

        var name = command.Name.Trim();
        var normalized = name.ToLower();

        if (await db.Categories.AnyAsync(
                c => c.Id != command.Id && c.Name.ToLower() == normalized, cancellationToken))
        {
            throw new DomainException($"A category named \"{name}\" already exists.");
        }

        category.Name = name;
        category.Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();
        category.IsActive = command.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
