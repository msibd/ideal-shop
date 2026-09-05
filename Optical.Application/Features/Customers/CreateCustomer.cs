using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Entities;
using Optical.Domain.Exceptions;

namespace Optical.Application.Features.Customers;

public sealed record CreateCustomerCommand(
    string Name,
    string Phone,
    string? Email,
    string? Address,
    string? Notes);

public sealed class CreateCustomerHandler(IApplicationDbContext db)
{
    public async Task<int> HandleAsync(CreateCustomerCommand command, CancellationToken cancellationToken = default)
    {
        var phone = command.Phone.Trim();

        if (await db.Customers.AnyAsync(c => c.Phone == phone, cancellationToken))
        {
            throw new DomainException($"A customer with phone \"{phone}\" already exists.");
        }

        var customer = new Customer
        {
            Name = command.Name.Trim(),
            Phone = phone,
            Email = command.Email?.Trim(),
            Address = command.Address?.Trim(),
            Notes = command.Notes?.Trim(),
            IsActive = true
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);

        return customer.Id;
    }
}
