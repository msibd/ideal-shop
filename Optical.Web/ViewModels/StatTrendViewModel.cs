using Optical.Application.Features.Dashboard;

namespace Optical.Web.ViewModels;

/// <summary>
/// How one stat tile shows its movement against the previous period.
/// </summary>
public class StatTrendViewModel
{
    public required DashboardMetric Metric { get; init; }

    public required string ComparisonLabel { get; init; }

    /// <summary>Money and counts read better as a percentage; small headcounts read better as ±n.</summary>
    public bool AsPercent { get; init; } = true;

    /// <summary>Shown when the previous period gives nothing to compare against.</summary>
    public string NoComparisonHint { get; init; } = "No earlier data to compare";
}
