using Optical.Application.Features.Dashboard;
using Optical.Application.Features.POS;
using Optical.Domain.Enums;

namespace Optical.Tests;

public class DashboardRangeTests
{
    private static readonly DateOnly Today = new(2026, 9, 5);

    private static DashboardRange Range(DashboardRangeKind kind, DateOnly? from = null, DateOnly? to = null) =>
        DashboardRange.Create(kind, from, to, Today);

    [Fact]
    public void Presets_cover_the_periods_the_filter_offers()
    {
        Assert.Equal((Today, Today), AsPair(Range(DashboardRangeKind.Today)));
        Assert.Equal((new DateOnly(2026, 8, 30), Today), AsPair(Range(DashboardRangeKind.Last7Days)));
        Assert.Equal((new DateOnly(2026, 8, 7), Today), AsPair(Range(DashboardRangeKind.Last30Days)));
        Assert.Equal((new DateOnly(2026, 9, 1), Today), AsPair(Range(DashboardRangeKind.ThisMonth)));
        Assert.Equal((new DateOnly(2026, 1, 1), Today), AsPair(Range(DashboardRangeKind.ThisYear)));

        Assert.Equal(7, Range(DashboardRangeKind.Last7Days).LengthInDays);
        Assert.Equal(30, Range(DashboardRangeKind.Last30Days).LengthInDays);
    }

    [Fact]
    public void Custom_range_uses_the_dates_given_and_survives_being_entered_backwards()
    {
        var from = new DateOnly(2026, 3, 1);
        var to = new DateOnly(2026, 3, 31);

        Assert.Equal((from, to), AsPair(Range(DashboardRangeKind.Custom, from, to)));
        Assert.Equal((from, to), AsPair(Range(DashboardRangeKind.Custom, to, from)));

        // A half-filled form still produces a usable single day.
        Assert.Equal((from, from), AsPair(Range(DashboardRangeKind.Custom, from, null)));
        Assert.Equal((Today, Today), AsPair(Range(DashboardRangeKind.Custom, null, null)));
    }

    [Fact]
    public void Previous_period_is_the_same_length_ending_the_day_before()
    {
        Assert.Equal(
            (new DateOnly(2026, 9, 4), new DateOnly(2026, 9, 4)),
            AsPair(Range(DashboardRangeKind.Today).Previous()));

        Assert.Equal(
            (new DateOnly(2026, 8, 23), new DateOnly(2026, 8, 29)),
            AsPair(Range(DashboardRangeKind.Last7Days).Previous()));

        var custom = Range(DashboardRangeKind.Custom, new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 19));
        Assert.Equal(10, custom.LengthInDays);
        Assert.Equal(
            (new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 9)),
            AsPair(custom.Previous()));
    }

    [Fact]
    public void Percent_change_stays_null_when_there_is_no_baseline_to_compare_against()
    {
        Assert.Null(new DashboardMetric(500m, 0m).ChangePercent);
        Assert.Equal(500m, new DashboardMetric(500m, 0m).Change);
        Assert.Equal(25m, new DashboardMetric(500m, 400m).ChangePercent);
        Assert.Equal(-50m, new DashboardMetric(200m, 400m).ChangePercent);
    }

    [Fact]
    public async Task Sales_orders_and_profit_only_count_the_selected_period()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();          // cost 100, sells at 150
        test.SetStock(product.Id, 100);

        var checkout = new CheckoutHandler(test.Db);
        await checkout.HandleAsync(new CheckoutCommand(
            null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 2)]));

        var today = DateOnly.FromDateTime(DateTime.Today);
        var handler = new GetDashboardSummaryHandler(test.Db);

        var current = await handler.HandleAsync(
            DashboardRange.Create(DashboardRangeKind.Today, null, null, today));

        Assert.Equal(300m, current.Sales.Current);
        Assert.Equal(1m, current.Orders.Current);
        Assert.Equal(100m, current.GrossProfit.Current);   // 300 sold - 200 cost
        Assert.Equal(0m, current.Sales.Previous);

        // A window that ended before the sale was rung up sees none of it.
        var earlier = await handler.HandleAsync(new DashboardRange(
            DashboardRangeKind.Custom, today.AddDays(-10), today.AddDays(-4)));

        Assert.Equal(0m, earlier.Sales.Current);
        Assert.Equal(0m, earlier.Orders.Current);
        Assert.Equal(0m, earlier.GrossProfit.Current);
    }

    [Fact]
    public async Task Outstanding_is_zero_while_checkout_settles_every_sale_in_full()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        test.SetStock(product.Id, 10);

        await new CheckoutHandler(test.Db).HandleAsync(new CheckoutCommand(
            null, PaymentMethod.Card, [new CheckoutItemCommand(product.Id, 1)]));

        var summary = await new GetDashboardSummaryHandler(test.Db).HandleAsync(
            DashboardRange.Create(DashboardRangeKind.Today, null, null, DateOnly.FromDateTime(DateTime.Today)));

        Assert.Equal(150m, summary.Sales.Current);
        Assert.Equal(0m, summary.OutstandingAmount);
        Assert.Equal(0m, summary.ExchangedAmount);
    }

    [Fact]
    public async Task New_customers_are_counted_against_the_period()
    {
        using var test = new TestDatabase();
        test.AddCustomer();
        test.AddCustomer();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var handler = new GetDashboardSummaryHandler(test.Db);

        var current = await handler.HandleAsync(
            DashboardRange.Create(DashboardRangeKind.Today, null, null, today));

        Assert.Equal(2m, current.NewCustomers.Current);
        Assert.Equal(0m, current.NewCustomers.Previous);
        Assert.Equal(2m, current.NewCustomers.Change);
        Assert.Null(current.NewCustomers.ChangePercent);
    }

    [Fact]
    public async Task Stock_counts_ignore_the_period_because_they_describe_right_now()
    {
        using var test = new TestDatabase();
        var lowProduct = test.AddProduct(reorderLevel: 5);
        test.AddProduct(reorderLevel: 5);          // never stocked -> out of stock
        test.SetStock(lowProduct.Id, 2);

        var handler = new GetDashboardSummaryHandler(test.Db);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var current = await handler.HandleAsync(
            DashboardRange.Create(DashboardRangeKind.Today, null, null, today));

        var lastYear = await handler.HandleAsync(new DashboardRange(
            DashboardRangeKind.Custom, today.AddDays(-400), today.AddDays(-370)));

        Assert.Equal(1, current.LowStockCount);
        Assert.Equal(1, current.OutOfStockCount);
        Assert.Equal(current.LowStockCount, lastYear.LowStockCount);
        Assert.Equal(current.OutOfStockCount, lastYear.OutOfStockCount);
    }

    private static (DateOnly From, DateOnly To) AsPair(DashboardRange range) => (range.From, range.To);
}
