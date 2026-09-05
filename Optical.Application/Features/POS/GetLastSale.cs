using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Enums;

namespace Optical.Application.Features.POS;

public sealed record LastSale(int Id, string InvoiceNumber, decimal TotalAmount, DateTime CreatedAt);

/// <summary>The most recent completed sale, shown on the till so the cashier can see what just rang up.</summary>
public sealed class GetLastSaleHandler(IApplicationDbContext db)
{
    public Task<LastSale?> HandleAsync(CancellationToken cancellationToken = default) =>
        db.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed)
            .OrderByDescending(s => s.Id)
            .Select(s => new LastSale(s.Id, s.InvoiceNumber, s.TotalAmount, s.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
}
