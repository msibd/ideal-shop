using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;

namespace Optical.Application.Features.Inventory;

public sealed record ProductStock(
    int ProductId,
    string Sku,
    string Name,
    int Quantity,
    int ReorderLevel,
    bool IsActive);

public sealed class GetProductStockHandler(IApplicationDbContext db)
{
    public Task<ProductStock?> HandleAsync(int productId, CancellationToken cancellationToken = default) =>
        db.Products
            .AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => new ProductStock(
                p.Id,
                p.Sku,
                p.Name,
                db.InventoryItems
                    .Where(i => i.ProductId == p.Id)
                    .Select(i => (int?)i.Quantity)
                    .FirstOrDefault() ?? 0,
                p.ReorderLevel,
                p.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
}
