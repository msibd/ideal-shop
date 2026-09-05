using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Entities;
using Optical.Domain.Exceptions;

namespace Optical.Application.Features.Categories;

public sealed record CreateCategoryCommand(string Name, string? Description);

public sealed class CreateCategoryHandler(IApplicationDbContext db)
{
    public async Task<int> HandleAsync(CreateCategoryCommand command, CancellationToken cancellationToken = default)
    {
        var name = command.Name.Trim();
        var normalized = name.ToLower();

        if (await db.Categories.AnyAsync(c => c.Name.ToLower() == normalized, cancellationToken))
        {
            throw new DomainException($"A category named \"{name}\" already exists.");
        }

        var category = new Category
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            IsActive = true
        };

        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        return category.Id;
    }
}
