using Optical.Application.Features.Dashboard;
using Optical.Application.Features.Inventory;
using Optical.Application.Features.POS;
using Optical.Application.Features.Purchases;
using Optical.Domain.Entities;
using Optical.Domain.Enums;

namespace Optical.Tests;

public class DashboardTests
{
    private static readonly DashboardRange Today = DashboardRange.Create(
        DashboardRangeKind.Today, null, null, DateOnly.FromDateTime(DateTime.Today));

    [Fact]
    public async Task Stock_buckets_are_mutually_exclusive_and_add_up()
    {
        using var test = new TestDatabase();

        var healthy = test.AddProduct(reorderLevel: 5);
        var low = test.AddProduct(reorderLevel: 5);
        test.AddProduct(reorderLevel: 5); // never purchased -> out of stock

        var adjust = new AdjustStockHandler(test.Db);
        await adjust.HandleAsync(new AdjustStockCommand(healthy.Id, 50));
        await adjust.HandleAsync(new AdjustStockCommand(low.Id, 3));

        var summary = await new GetDashboardSummaryHandler(test.Db).HandleAsync(Today);

        Assert.Equal(1, summary.StockStatus.InStock);
        Assert.Equal(1, summary.StockStatus.LowStock);
        Assert.Equal(1, summary.StockStatus.OutOfStock);
        Assert.Equal(summary.TotalProducts, summary.StockStatus.Total);
    }

    [Fact]
    public async Task Stock_value_is_quantity_times_cost()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();

        await new AdjustStockHandler(test.Db).HandleAsync(new AdjustStockCommand(product.Id, 4));

        var summary = await new GetDashboardSummaryHandler(test.Db).HandleAsync(Today);

        // AddProduct seeds a purchase price of 100.
        Assert.Equal(400m, summary.StockValue);
        Assert.Equal(400m, Assert.Single(summary.StockValueByCategory).Value);
    }

    [Fact]
    public async Task Empty_database_returns_zeroes_rather_than_throwing()
    {
        using var test = new TestDatabase();

        var summary = await new GetDashboardSummaryHandler(test.Db).HandleAsync(Today);

        Assert.Equal(0, summary.TotalProducts);
        Assert.Equal(0m, summary.StockValue);
        Assert.Equal(0m, summary.Sales.Current);
        Assert.Equal(0m, summary.Orders.Current);
        Assert.Null(summary.Sales.ChangePercent);
        Assert.Empty(summary.LowStockAlerts);
        Assert.Empty(summary.StockValueByCategory);
        Assert.Empty(summary.RecentActivities);
    }

    [Fact]
    public async Task Category_slices_are_capped_at_four_with_the_tail_folded_into_other()
    {
        using var test = new TestDatabase();
        var adjust = new AdjustStockHandler(test.Db);

        // Six products, each in its own category, with descending stock value.
        for (var quantity = 6; quantity >= 1; quantity--)
        {
            var product = test.AddProduct();
            await adjust.HandleAsync(new AdjustStockCommand(product.Id, quantity));
        }

        var summary = await new GetDashboardSummaryHandler(test.Db).HandleAsync(Today);

        Assert.Equal(5, summary.StockValueByCategory.Count);
        Assert.Equal("Other", summary.StockValueByCategory[^1].CategoryName);

        // 6+5+4+3 named, 2+1 folded, at a cost of 100 each.
        Assert.Equal(300m, summary.StockValueByCategory[^1].Value);
        Assert.Equal(2100m, summary.StockValueByCategory.Sum(c => c.Value));
    }

    [Fact]
    public async Task Low_stock_alerts_list_the_scarcest_first_and_exclude_zero_stock()
    {
        using var test = new TestDatabase();
        var adjust = new AdjustStockHandler(test.Db);

        var scarce = test.AddProduct(reorderLevel: 10);
        var lessScarce = test.AddProduct(reorderLevel: 10);
        var empty = test.AddProduct(reorderLevel: 10);

        await adjust.HandleAsync(new AdjustStockCommand(scarce.Id, 1));
        await adjust.HandleAsync(new AdjustStockCommand(lessScarce.Id, 8));

        var summary = await new GetDashboardSummaryHandler(test.Db).HandleAsync(Today);

        Assert.Equal(2, summary.LowStockAlerts.Count);
        Assert.Equal(scarce.Id, summary.LowStockAlerts[0].ProductId);
        Assert.Equal(lessScarce.Id, summary.LowStockAlerts[1].ProductId);
        Assert.DoesNotContain(summary.LowStockAlerts, a => a.ProductId == empty.Id);
    }

    [Fact]
    public async Task Recent_activities_are_newest_first_across_sources()
    {
        using var test = new TestDatabase();
        var supplier = test.AddSupplier();
        var product = test.AddProduct();

        await new CreatePurchaseHandler(test.Db).HandleAsync(new CreatePurchaseCommand(
            supplier.Id,
            DateOnly.FromDateTime(DateTime.Today),
            null,
            [new CreatePurchaseItemCommand(product.Id, 1, 100m)]));

        var summary = await new GetDashboardSummaryHandler(test.Db).HandleAsync(Today);

        Assert.NotEmpty(summary.RecentActivities);
        Assert.Equal("Purchase", summary.RecentActivities[0].Kind);
        Assert.Equal(
            summary.RecentActivities.OrderByDescending(a => a.At).Select(a => a.At),
            summary.RecentActivities.Select(a => a.At));
    }

    [Fact]
    public async Task Recent_sales_list_the_periods_own_sales_newest_first()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        test.SetStock(product.Id, 100);
        var customer = test.AddCustomer();

        var checkout = new CheckoutHandler(test.Db);
        await checkout.HandleAsync(new CheckoutCommand(
            null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 1)]));
        var second = await checkout.HandleAsync(new CheckoutCommand(
            customer.Id, PaymentMethod.Card, [new CheckoutItemCommand(product.Id, 2)]));

        var summary = await new GetDashboardSummaryHandler(test.Db).HandleAsync(Today);

        Assert.Equal(2, summary.RecentSales.Count);

        var newest = summary.RecentSales[0];
        Assert.Equal(second.InvoiceNumber, newest.InvoiceNumber);
        Assert.Equal(customer.Name, newest.CustomerName);
        Assert.Equal(PaymentMethod.Card, newest.Method);
        Assert.Equal(300m, newest.Total);

        Assert.Null(summary.RecentSales[1].CustomerName);
    }

    [Fact]
    public async Task Top_products_rank_by_units_sold()
    {
        using var test = new TestDatabase();
        var slow = test.AddProduct();
        var fast = test.AddProduct();
        test.SetStock(slow.Id, 100);
        test.SetStock(fast.Id, 100);

        var checkout = new CheckoutHandler(test.Db);
        await checkout.HandleAsync(new CheckoutCommand(null, PaymentMethod.Cash,
            [new CheckoutItemCommand(slow.Id, 2)]));
        await checkout.HandleAsync(new CheckoutCommand(null, PaymentMethod.Cash,
            [new CheckoutItemCommand(fast.Id, 3), new CheckoutItemCommand(slow.Id, 1)]));
        await checkout.HandleAsync(new CheckoutCommand(null, PaymentMethod.Cash,
            [new CheckoutItemCommand(fast.Id, 4)]));

        var summary = await new GetDashboardSummaryHandler(test.Db).HandleAsync(Today);

        Assert.Equal(2, summary.TopProducts.Count);

        // fast: 3 + 4 = 7 units, slow: 2 + 1 = 3 units
        Assert.Equal(fast.Sku, summary.TopProducts[0].Sku);
        Assert.Equal(7, summary.TopProducts[0].UnitsSold);
        Assert.Equal(1050m, summary.TopProducts[0].Revenue);

        Assert.Equal(slow.Sku, summary.TopProducts[1].Sku);
        Assert.Equal(3, summary.TopProducts[1].UnitsSold);
    }

    [Fact]
    public async Task Sales_are_split_by_payment_method_largest_first()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        test.SetStock(product.Id, 100);

        var checkout = new CheckoutHandler(test.Db);
        await checkout.HandleAsync(new CheckoutCommand(null, PaymentMethod.Cash,
            [new CheckoutItemCommand(product.Id, 1)]));
        await checkout.HandleAsync(new CheckoutCommand(null, PaymentMethod.Card,
            [new CheckoutItemCommand(product.Id, 4)]));

        var summary = await new GetDashboardSummaryHandler(test.Db).HandleAsync(Today);

        Assert.Equal(2, summary.SalesByMethod.Count);
        Assert.Equal(PaymentMethod.Card, summary.SalesByMethod[0].Method);
        Assert.Equal(600m, summary.SalesByMethod[0].Amount);
        Assert.Equal(PaymentMethod.Cash, summary.SalesByMethod[1].Method);
        Assert.Equal(150m, summary.SalesByMethod[1].Amount);

        // The split must account for every taka the Sales card claims.
        Assert.Equal(summary.Sales.Current, summary.SalesByMethod.Sum(m => m.Amount));
    }

    [Fact]
    public async Task Recent_activities_include_sales()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        test.SetStock(product.Id, 10);

        var sale = await new CheckoutHandler(test.Db).HandleAsync(new CheckoutCommand(
            null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 1)]));

        var summary = await new GetDashboardSummaryHandler(test.Db).HandleAsync(Today);

        var entry = Assert.Single(summary.RecentActivities, a => a.Kind == "Sale");
        Assert.Contains(sale.InvoiceNumber, entry.Title);
        Assert.Equal("Walk-in customer", entry.Detail);
    }
}
