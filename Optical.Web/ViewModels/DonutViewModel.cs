namespace Optical.Web.ViewModels;

/// <summary>
/// One slice of a donut. <see cref="Value"/> drives the geometry;
/// <see cref="Display"/> is what the legend shows, so the reader never has to
/// judge a quantity by arc length alone.
/// </summary>
/// <param name="Colour">A CSS custom property name from the chart slots, e.g. "--chart-1".</param>
/// <param name="Status">"good", "warning" or "critical" when the slice is a state
/// rather than a category. Adds an icon so meaning never rests on hue.</param>
public sealed record DonutSlice(
    string Label,
    decimal Value,
    string Display,
    string Colour,
    string? Status = null);

public sealed class DonutViewModel
{
    public required string CenterValue { get; init; }

    public required string CenterLabel { get; init; }

    public required string Description { get; init; }

    public required IReadOnlyList<DonutSlice> Slices { get; init; }

    public string EmptyMessage { get; init; } = "No data yet.";
}
