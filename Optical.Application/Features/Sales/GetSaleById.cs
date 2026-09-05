using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Enums;

namespace Optical.Application.Features.Sales;

public sealed record SaleItemDetails(
    string Sku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal DiscountAmount,
    decimal LineTotal);

public sealed record SaleDetails(
    int Id,
    string InvoiceNumber,
    DateTime CreatedAt,
    string? CustomerName,
    string? CustomerPhone,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TotalAmount,
    PaymentMethod? PaymentMethod,
    decimal? PaidAmount,
    SaleStatus Status,
    IReadOnlyList<SaleItemDetails> Items)
{
    public int TotalQuantity => Items.Sum(i => i.Quantity);

    public bool HasDiscount => DiscountAmount > 0;
}

public sealed class GetSaleByIdHandler(IApplicationDbContext db)
{
    public Task<SaleDetails?> HandleAsync(int id, CancellationToken cancellationToken = default) =>
        db.Sales
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new SaleDetails(
                s.Id,
                s.InvoiceNumber,
                s.CreatedAt,
                s.Customer == null ? null : s.Customer.Name,
                s.Customer == null ? null : s.Customer.Phone,
                s.SubTotal,
                s.DiscountAmount,
                s.TotalAmount,
                s.Payment == null ? null : (PaymentMethod?)s.Payment.Method,
                s.Payment == null ? null : (decimal?)s.Payment.Amount,
                s.Status,
                s.Items
                    .OrderBy(i => i.Id)
                    .Select(i => new SaleItemDetails(
                        i.Product.Sku,
                        i.Product.Name,
                        i.Quantity,
                        i.UnitPrice,
                        i.DiscountPercent,
                        i.DiscountAmount,
                        i.LineTotal))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);
}
