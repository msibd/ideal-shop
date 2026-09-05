using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;

namespace Optical.Application.Features.Purchases;

public sealed record PurchaseItemDetails(
    string Sku,
    string ProductName,
    int Quantity,
    decimal PurchasePrice,
    decimal LineTotal);

public sealed record PurchaseDetails(
    int Id,
    DateOnly PurchaseDate,
    string SupplierName,
    string? SupplierPhone,
    string? InvoiceNumber,
    decimal TotalAmount,
    DateTime CreatedAt,
    IReadOnlyList<PurchaseItemDetails> Items);

public sealed class GetPurchaseByIdHandler(IApplicationDbContext db)
{
    public Task<PurchaseDetails?> HandleAsync(int id, CancellationToken cancellationToken = default) =>
        db.Purchases
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new PurchaseDetails(
                p.Id,
                p.PurchaseDate,
                p.Supplier.Name,
                p.Supplier.Phone,
                p.InvoiceNumber,
                p.TotalAmount,
                p.CreatedAt,
                p.Items
                    .OrderBy(i => i.Id)
                    .Select(i => new PurchaseItemDetails(
                        i.Product.Sku,
                        i.Product.Name,
                        i.Quantity,
                        i.PurchasePrice,
                        i.LineTotal))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);
}
