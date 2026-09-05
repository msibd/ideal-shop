using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;

namespace Optical.Application.Features.POS;

public sealed record PosCustomer(int Id, string Name, string Phone);

/// <summary>
/// Finds a customer for the sale by name or mobile number, and re-reads the chosen one
/// when a rejected checkout is redrawn. Only active customers can be attached to a sale.
/// </summary>
public sealed class SearchPosCustomersHandler(IApplicationDbContext db)
{
    public const int MaxResults = 10;

    public async Task<IReadOnlyList<PosCustomer>> SearchAsync(
        string? term,
        CancellationToken cancellationToken = default)
    {
        var query = db.Customers.AsNoTracking().Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var search = term.Trim().ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(search) ||
                c.Phone.Contains(search));
        }

        return await query
            .OrderBy(c => c.Name)
            .Take(MaxResults)
            .Select(c => new PosCustomer(c.Id, c.Name, c.Phone))
            .ToListAsync(cancellationToken);
    }

    public async Task<PosCustomer?> GetByIdAsync(int? id, CancellationToken cancellationToken = default)
    {
        if (id is null)
        {
            return null;
        }

        return await db.Customers
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new PosCustomer(c.Id, c.Name, c.Phone))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
