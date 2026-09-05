using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Enums;

namespace Optical.Application.Features.Exchanges;

public sealed record ExchangeListItem(
    int Id,
    string ExchangeNumber,
    DateTime CreatedAt,
    int SaleId,
    string InvoiceNumber,
    string? CustomerName,
    int ReturnedUnits,
    int ReplacementUnits,
    decimal ReturnedAmount,
    decimal ReplacementAmount,
    decimal AmountPaid,
    PaymentMethod? PaymentMethod,
    ExchangeReason Reason);

public sealed record ExchangeListResult(
    IReadOnlyList<ExchangeListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    decimal ReturnedAmount,
    decimal AmountPaid)
{
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}

public sealed class GetExchangesHandler(IApplicationDbContext db)
{
    public const int DefaultPageSize = 20;

    public async Task<ExchangeListResult> HandleAsync(
        string? search,
        DateOnly? from,
        DateOnly? to,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            page = 1;
        }

        var query = db.Exchanges.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(e =>
                e.ExchangeNumber.ToLower().Contains(term) ||
                e.Sale.InvoiceNumber.ToLower().Contains(term) ||
                // A receipt scanned at the exchange desk arrives without its dashes.
                e.Sale.InvoiceNumber.Replace("-", "").ToLower().Contains(term) ||
                (e.Sale.Customer != null && e.Sale.Customer.Name.ToLower().Contains(term)));
        }

        // Exchanges are stamped in UTC but the shop filters by its own calendar days.
        if (from is not null)
        {
            var fromUtc = DateTime.SpecifyKind(from.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local)
                .ToUniversalTime();
            query = query.Where(e => e.CreatedAt >= fromUtc);
        }

        if (to is not null)
        {
            var toUtc = DateTime.SpecifyKind(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local)
                .ToUniversalTime();
            query = query.Where(e => e.CreatedAt < toUtc);
        }

        var totals = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Returned = g.Sum(e => e.ReturnedAmount),
                Paid = g.Sum(e => e.AmountPaid)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.Id)
            .Skip((page - 1) * DefaultPageSize)
            .Take(DefaultPageSize)
            .Select(e => new ExchangeListItem(
                e.Id,
                e.ExchangeNumber,
                e.CreatedAt,
                e.SaleId,
                e.Sale.InvoiceNumber,
                e.Sale.Customer == null ? null : e.Sale.Customer.Name,
                e.ReturnedItems.Sum(i => i.Quantity),
                e.ReplacementItems.Sum(i => i.Quantity),
                e.ReturnedAmount,
                e.ReplacementAmount,
                e.AmountPaid,
                e.PaymentMethod,
                e.Reason))
            .ToListAsync(cancellationToken);

        return new ExchangeListResult(
            items,
            page,
            DefaultPageSize,
            totals?.Count ?? 0,
            totals?.Returned ?? 0m,
            totals?.Paid ?? 0m);
    }
}
