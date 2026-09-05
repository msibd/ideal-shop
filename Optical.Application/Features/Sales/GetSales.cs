using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Enums;

namespace Optical.Application.Features.Sales;

public sealed record SaleListItem(
    int Id,
    string InvoiceNumber,
    DateTime CreatedAt,
    string? CustomerName,
    int LineCount,
    int TotalQuantity,
    decimal TotalAmount,
    PaymentMethod? PaymentMethod,
    SaleStatus Status);

public sealed record SaleListResult(
    IReadOnlyList<SaleListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    decimal TotalAmount)
{
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}

public sealed class GetSalesHandler(IApplicationDbContext db)
{
    public const int DefaultPageSize = 20;

    public async Task<SaleListResult> HandleAsync(
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

        var query = db.Sales.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(s =>
                s.InvoiceNumber.ToLower().Contains(term) ||
                // The receipt barcode carries the number without its dashes, so a scan into
                // this box has to match too.
                s.InvoiceNumber.Replace("-", "").ToLower().Contains(term) ||
                (s.Customer != null && s.Customer.Name.ToLower().Contains(term)) ||
                (s.Customer != null && s.Customer.Phone.Contains(term)));
        }

        // Sales are stamped in UTC but the shop filters by its own calendar days,
        // so each bound is widened into the matching UTC instant.
        if (from is not null)
        {
            var fromUtc = DateTime.SpecifyKind(from.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local)
                .ToUniversalTime();
            query = query.Where(s => s.CreatedAt >= fromUtc);
        }

        if (to is not null)
        {
            var toUtc = DateTime.SpecifyKind(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local)
                .ToUniversalTime();
            query = query.Where(s => s.CreatedAt < toUtc);
        }

        var totals = await query
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Amount = g.Sum(s => s.TotalAmount) })
            .FirstOrDefaultAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.Id)
            .Skip((page - 1) * DefaultPageSize)
            .Take(DefaultPageSize)
            .Select(s => new SaleListItem(
                s.Id,
                s.InvoiceNumber,
                s.CreatedAt,
                s.Customer == null ? null : s.Customer.Name,
                s.Items.Count,
                s.Items.Sum(i => i.Quantity),
                s.TotalAmount,
                s.Payment == null ? null : (PaymentMethod?)s.Payment.Method,
                s.Status))
            .ToListAsync(cancellationToken);

        return new SaleListResult(
            items,
            page,
            DefaultPageSize,
            totals?.Count ?? 0,
            totals?.Amount ?? 0m);
    }
}
