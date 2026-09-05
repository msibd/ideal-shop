using Optical.Application.Features.Inventory;
using Optical.Application.Features.POS;
using Optical.Application.Features.Reports;
using Optical.Domain.Enums;

namespace Optical.Tests;

public class ReportTests
{
    private static ReportPeriod Today =>
        ReportPeriod.Create(DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today));

    [Fact]
    public async Task Sales_report_totals_the_period_and_splits_by_payment_method()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        test.SetStock(product.Id, 100);

        var checkout = new CheckoutHandler(test.Db);
        await checkout.HandleAsync(new CheckoutCommand(null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 2)]));
        await checkout.HandleAsync(new CheckoutCommand(null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 1)]));
        await checkout.HandleAsync(new CheckoutCommand(null, PaymentMethod.Card, [new CheckoutItemCommand(product.Id, 4)]));

        var report = await new GetSalesReportHandler(test.Db).HandleAsync(Today);

        // 2 + 1 + 4 units at 150.00
        Assert.Equal(3, report.SaleCount);
        Assert.Equal(1050m, report.TotalAmount);
        Assert.Equal(350m, report.AverageSale);

        var cash = Assert.Single(report.Payments, p => p.Method == PaymentMethod.Cash);
        Assert.Equal(2, cash.Count);
        Assert.Equal(450m, cash.Amount);

        var card = Assert.Single(report.Payments, p => p.Method == PaymentMethod.Card);
        Assert.Equal(1, card.Count);
        Assert.Equal(600m, card.Amount);
    }

    [Fact]
    public async Task Sales_report_excludes_sales_outside_the_period()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        test.SetStock(product.Id, 10);

        await new CheckoutHandler(test.Db).HandleAsync(
            new CheckoutCommand(null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 1)]));

        var lastMonth = DateOnly.FromDateTime(DateTime.Today).AddDays(-60);
        var report = await new GetSalesReportHandler(test.Db)
            .HandleAsync(ReportPeriod.Create(lastMonth, lastMonth.AddDays(5), DateOnly.FromDateTime(DateTime.Today)));

        Assert.Equal(0, report.SaleCount);
        Assert.Equal(0m, report.TotalAmount);
        Assert.Empty(report.Payments);
    }

    [Fact]
    public async Task Sales_report_reports_zero_for_a_period_with_no_sales()
    {
        using var test = new TestDatabase();

        var report = await new GetSalesReportHandler(test.Db).HandleAsync(Today);

        Assert.Equal(0, report.SaleCount);
        Assert.Equal(0m, report.TotalAmount);
        Assert.Equal(0m, report.AverageSale);
    }

    [Fact]
    public async Task Inventory_report_totals_units_value_and_stock_status()
    {
        using var test = new TestDatabase();

        var healthy = test.AddProduct(reorderLevel: 5);
        var low = test.AddProduct(reorderLevel: 5);
        test.AddProduct(reorderLevel: 5); // never purchased -> out of stock

        var adjust = new AdjustStockHandler(test.Db);
        await adjust.HandleAsync(new AdjustStockCommand(healthy.Id, 50));
        await adjust.HandleAsync(new AdjustStockCommand(low.Id, 3));

        var report = await new GetInventoryReportHandler(test.Db).HandleAsync();

        Assert.Equal(3, report.TotalCount);
        Assert.Equal(53, report.TotalUnits);
        Assert.Equal(5300m, report.TotalStockValue); // 53 units at a cost of 100.00
        Assert.Equal(2, report.LowStockCount);       // the low one and the out-of-stock one
        Assert.Equal(1, report.OutOfStockCount);
        Assert.Equal(3, report.Rows.Count);
    }

    [Fact]
    public async Task Inventory_report_can_list_low_stock_only_while_totals_stay_shop_wide()
    {
        using var test = new TestDatabase();

        var healthy = test.AddProduct(reorderLevel: 5);
        var low = test.AddProduct(reorderLevel: 5);

        var adjust = new AdjustStockHandler(test.Db);
        await adjust.HandleAsync(new AdjustStockCommand(healthy.Id, 50));
        await adjust.HandleAsync(new AdjustStockCommand(low.Id, 1));

        var report = await new GetInventoryReportHandler(test.Db).HandleAsync(lowStockOnly: true);

        var row = Assert.Single(report.Rows);
        Assert.Equal(low.Sku, row.Sku);
        Assert.True(row.IsLowStock);
        Assert.False(row.IsOutOfStock);

        Assert.Equal(1, report.TotalCount);
        Assert.Equal(51, report.TotalUnits);   // totals still cover the whole catalogue
        Assert.Equal(5100m, report.TotalStockValue);
    }

    [Fact]
    public void Report_period_defaults_to_the_last_thirty_days_and_repairs_a_backwards_range()
    {
        var today = new DateOnly(2026, 9, 5);

        var defaulted = ReportPeriod.Create(null, null, today);
        Assert.Equal(new DateOnly(2026, 8, 7), defaulted.From);
        Assert.Equal(today, defaulted.To);

        var backwards = ReportPeriod.Create(today, today.AddDays(-3), today);
        Assert.Equal(today.AddDays(-3), backwards.From);
        Assert.Equal(today, backwards.To);
    }
}
