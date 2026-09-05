using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;

namespace Optical.Application.Features.Purchases;

public sealed record SupplierOption(int Id, string Name);

public sealed record PurchaseProductOption(int Id, string Sku, string Name, decimal PurchasePrice);

public sealed record PurchaseFormOptions(
    IReadOnlyList<SupplierOption> Suppliers,
    IReadOnlyList<PurchaseProductOption> Products);

/// <summary>
/// Only active suppliers and active products can be used on a new purchase.
/// The purchase price is offered as a default; the server still validates whatever is posted.
/// </summary>
public sealed class GetPurchaseFormOptionsHandler(IApplicationDbContext db)
{
    public async Task<PurchaseFormOptions> HandleAsync(CancellationToken cancellationToken = default)
    {
        var suppliers = await db.Suppliers
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new SupplierOption(s.Id, s.Name))
            .ToListAsync(cancellationToken);

        var products = await db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new PurchaseProductOption(p.Id, p.Sku, p.Name, p.PurchasePrice))
            .ToListAsync(cancellationToken);

        return new PurchaseFormOptions(suppliers, products);
    }
}
