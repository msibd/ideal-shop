using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Exceptions;

namespace Optical.Application.Features.Customers;

public sealed record UpdateCustomerCommand(
    int Id,
    string Name,
    string Phone,
    string? Email,
    string? Address,
    string? Notes,
    bool IsActive);

public sealed class UpdateCustomerHandler(IApplicationDbContext db)
{
    public async Task HandleAsync(UpdateCustomerCommand command, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException($"Customer {command.Id} was not found.");

        var phone = command.Phone.Trim();

        if (await db.Customers.AnyAsync(c => c.Id != command.Id && c.Phone == phone, cancellationToken))
        {
            throw new DomainException($"A customer with phone \"{phone}\" already exists.");
        }

        customer.Name = command.Name.Trim();
        customer.Phone = phone;
        customer.Email = command.Email?.Trim();
        customer.Address = command.Address?.Trim();
        customer.Notes = command.Notes?.Trim();
        customer.IsActive = command.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
