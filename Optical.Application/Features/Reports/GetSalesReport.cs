using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Enums;

namespace Optical.Application.Features.Reports;

public sealed record PaymentMethodSummary(PaymentMethod Method, int Count, decimal Amount);

public sealed record SalesReportResult(
    ReportPeriod Period,
    int SaleCount,
    decimal TotalAmount,
    IReadOnlyList<PaymentMethodSummary> Payments)
{
    public decimal AverageSale => SaleCount == 0 ? 0m : TotalAmount / SaleCount;
}

/// <summary>
/// Completed sales for a date range, with the split by payment method.
/// Cancelled sales are excluded so the figures match what the till actually took.
/// </summary>
public sealed class GetSalesReportHandler(IApplicationDbContext db)
{
    public async Task<SalesReportResult> HandleAsync(
        ReportPeriod period,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtc) = period.ToUtcWindow();

        var totals = await db.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed && s.CreatedAt >= fromUtc && s.CreatedAt < toUtc)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Amount = g.Sum(s => s.TotalAmount) })
            .FirstOrDefaultAsync(cancellationToken);

        var payments = await db.Payments
            .AsNoTracking()
            .Where(p => p.Sale.Status == SaleStatus.Completed
                && p.Sale.CreatedAt >= fromUtc
                && p.Sale.CreatedAt < toUtc)
            .GroupBy(p => p.Method)
            .Select(g => new PaymentMethodSummary(g.Key, g.Count(), g.Sum(p => p.Amount)))
            .ToListAsync(cancellationToken);

        return new SalesReportResult(
            period,
            totals?.Count ?? 0,
            totals?.Amount ?? 0m,
            payments.OrderByDescending(p => p.Amount).ToList());
    }
}
