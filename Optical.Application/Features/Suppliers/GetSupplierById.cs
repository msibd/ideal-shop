using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;

namespace Optical.Application.Features.Suppliers;

public sealed record SupplierDetails(
    int Id,
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive);

public sealed class GetSupplierByIdHandler(IApplicationDbContext db)
{
    public Task<SupplierDetails?> HandleAsync(int id, CancellationToken cancellationToken = default) =>
        db.Suppliers
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new SupplierDetails(s.Id, s.Name, s.Phone, s.Email, s.Address, s.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
}
