using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;

namespace Optical.Application.Features.Customers;

public sealed record CustomerDetails(
    int Id,
    string Name,
    string Phone,
    string? Email,
    string? Address,
    string? Notes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class GetCustomerByIdHandler(IApplicationDbContext db)
{
    public Task<CustomerDetails?> HandleAsync(int id, CancellationToken cancellationToken = default) =>
        db.Customers
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CustomerDetails(
                c.Id,
                c.Name,
                c.Phone,
                c.Email,
                c.Address,
                c.Notes,
                c.IsActive,
                c.CreatedAt,
                c.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);
}
