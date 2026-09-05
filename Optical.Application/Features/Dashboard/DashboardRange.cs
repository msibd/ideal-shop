namespace Optical.Application.Features.Dashboard;

public enum DashboardRangeKind
{
    Today = 1,
    Last7Days = 2,
    Last30Days = 3,
    ThisMonth = 4,
    ThisYear = 5,
    Custom = 6
}

/// <summary>
/// The period the dashboard reports on, plus the equal-length period immediately before it
/// so every figure can be compared against something real rather than an invented baseline.
/// </summary>
public sealed record DashboardRange(DashboardRangeKind Kind, DateOnly From, DateOnly To)
{
    public int LengthInDays => To.DayNumber - From.DayNumber + 1;

    public string Label => Kind switch
    {
        DashboardRangeKind.Today => "Today",
        DashboardRangeKind.Last7Days => "Last 7 days",
        DashboardRangeKind.Last30Days => "Last 30 days",
        DashboardRangeKind.ThisMonth => "This month",
        DashboardRangeKind.ThisYear => "This year",
        _ => From == To
            ? From.ToString("dd MMM yyyy")
            : $"{From:dd MMM yyyy} - {To:dd MMM yyyy}"
    };

    public string ComparisonLabel => Kind == DashboardRangeKind.Today
        ? "vs yesterday"
        : "vs previous period";

    /// <summary>The same number of days, ending the day before this period starts.</summary>
    public DashboardRange Previous() =>
        new(Kind, From.AddDays(-LengthInDays), From.AddDays(-1));

    public static DashboardRange Create(
        DashboardRangeKind kind,
        DateOnly? from,
        DateOnly? to,
        DateOnly today) => kind switch
    {
        DashboardRangeKind.Last7Days => new DashboardRange(kind, today.AddDays(-6), today),
        DashboardRangeKind.Last30Days => new DashboardRange(kind, today.AddDays(-29), today),
        DashboardRangeKind.ThisMonth => new DashboardRange(kind, new DateOnly(today.Year, today.Month, 1), today),
        DashboardRangeKind.ThisYear => new DashboardRange(kind, new DateOnly(today.Year, 1, 1), today),
        DashboardRangeKind.Custom => CreateCustom(from, to, today),
        _ => new DashboardRange(DashboardRangeKind.Today, today, today)
    };

    private static DashboardRange CreateCustom(DateOnly? from, DateOnly? to, DateOnly today)
    {
        var start = from ?? to ?? today;
        var end = to ?? from ?? today;

        // A backwards range is a slip, not an error worth blocking the dashboard for.
        return start <= end
            ? new DashboardRange(DashboardRangeKind.Custom, start, end)
            : new DashboardRange(DashboardRangeKind.Custom, end, start);
    }
}

/// <summary>
/// One figure and the same figure for the previous period. Percent change is null when
/// there is nothing to compare against, so the UI can stay silent instead of inventing a trend.
/// </summary>
public sealed record DashboardMetric(decimal Current, decimal Previous)
{
    public decimal Change => Current - Previous;

    public decimal? ChangePercent => Previous == 0
        ? null
        : (Current - Previous) / Previous * 100m;
}
