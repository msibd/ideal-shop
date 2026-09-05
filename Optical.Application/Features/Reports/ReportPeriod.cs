namespace Optical.Application.Features.Reports;

/// <summary>
/// The calendar range a report covers. Sales are stamped in UTC but the shop thinks in its
/// own days, so the range is widened into the matching UTC window before querying.
/// </summary>
public sealed record ReportPeriod(DateOnly From, DateOnly To)
{
    public const int DefaultDays = 30;

    public static ReportPeriod Create(DateOnly? from, DateOnly? to, DateOnly today)
    {
        var end = to ?? today;
        var start = from ?? end.AddDays(-(DefaultDays - 1));

        // A backwards range is a typo, not a reason to show nothing.
        return start > end ? new ReportPeriod(end, start) : new ReportPeriod(start, end);
    }

    public (DateTime FromUtc, DateTime ToUtc) ToUtcWindow()
    {
        var fromLocal = DateTime.SpecifyKind(From.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
        var toLocal = DateTime.SpecifyKind(To.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);

        return (fromLocal.ToUniversalTime(), toLocal.ToUniversalTime());
    }
}
