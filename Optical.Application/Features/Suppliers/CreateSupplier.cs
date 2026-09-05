using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Entities;
using Optical.Domain.Exceptions;

namespace Optical.Application.Features.Suppliers;

public sealed record CreateSupplierCommand(
    string Name,
    string? Phone,
    string? Email,
    string? Address);

public sealed class CreateSupplierHandler(IApplicationDbContext db)
{
    public async Task<int> HandleAsync(CreateSupplierCommand command, CancellationToken cancellationToken = default)
    {
        var name = command.Name.Trim();
        var normalizedName = name.ToLower();

        if (await db.Suppliers.AnyAsync(s => s.Name.ToLower() == normalizedName, cancellationToken))
        {
            throw new DomainException($"A supplier named \"{name}\" already exists.");
        }

        var supplier = new Supplier
        {
            Name = name,
            Phone = command.Phone?.Trim(),
            Email = command.Email?.Trim(),
            Address = command.Address?.Trim(),
            IsActive = true
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);

        return supplier.Id;
    }
}
