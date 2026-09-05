using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Enums;

namespace Optical.Application.Features.Exchanges;

public sealed record ExchangeLineDetails(
    string Sku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record ExchangeDetails(
    int Id,
    string ExchangeNumber,
    DateTime CreatedAt,
    int SaleId,
    string InvoiceNumber,
    DateTime SoldAt,
    string? CustomerName,
    string? CustomerPhone,
    ExchangeReason Reason,
    string? Note,
    decimal ReturnedAmount,
    decimal ReplacementAmount,
    decimal AmountPaid,
    PaymentMethod? PaymentMethod,
    IReadOnlyList<ExchangeLineDetails> ReturnedItems,
    IReadOnlyList<ExchangeLineDetails> ReplacementItems);

public sealed class GetExchangeByIdHandler(IApplicationDbContext db)
{
    public Task<ExchangeDetails?> HandleAsync(int id, CancellationToken cancellationToken = default) =>
        db.Exchanges
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new ExchangeDetails(
                e.Id,
                e.ExchangeNumber,
                e.CreatedAt,
                e.SaleId,
                e.Sale.InvoiceNumber,
                e.Sale.CreatedAt,
                e.Sale.Customer == null ? null : e.Sale.Customer.Name,
                e.Sale.Customer == null ? null : e.Sale.Customer.Phone,
                e.Reason,
                e.Note,
                e.ReturnedAmount,
                e.ReplacementAmount,
                e.AmountPaid,
                e.PaymentMethod,
                e.ReturnedItems
                    .OrderBy(i => i.Id)
                    .Select(i => new ExchangeLineDetails(
                        i.Product.Sku,
                        i.Product.Name,
                        i.Quantity,
                        i.UnitPrice,
                        i.LineTotal))
                    .ToList(),
                e.ReplacementItems
                    .OrderBy(i => i.Id)
                    .Select(i => new ExchangeLineDetails(
                        i.Product.Sku,
                        i.Product.Name,
                        i.Quantity,
                        i.UnitPrice,
                        i.LineTotal))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);
}
