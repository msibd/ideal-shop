using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Exceptions;

namespace Optical.Application.Features.Suppliers;

public sealed record UpdateSupplierCommand(
    int Id,
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive);

public sealed class UpdateSupplierHandler(IApplicationDbContext db)
{
    public async Task HandleAsync(UpdateSupplierCommand command, CancellationToken cancellationToken = default)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException($"Supplier {command.Id} was not found.");

        var name = command.Name.Trim();
        var normalizedName = name.ToLower();

        if (await db.Suppliers.AnyAsync(
                s => s.Id != command.Id && s.Name.ToLower() == normalizedName, cancellationToken))
        {
            throw new DomainException($"A supplier named \"{name}\" already exists.");
        }

        supplier.Name = name;
        supplier.Phone = command.Phone?.Trim();
        supplier.Email = command.Email?.Trim();
        supplier.Address = command.Address?.Trim();
        supplier.IsActive = command.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
