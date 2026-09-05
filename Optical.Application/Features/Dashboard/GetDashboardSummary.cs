using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Enums;

namespace Optical.Application.Features.Dashboard;

/// <summary>
/// The three stock buckets are mutually exclusive, so they always add up to the
/// product count and can be shown as a share of the whole.
/// </summary>
public sealed record StockStatusBreakdown(int InStock, int LowStock, int OutOfStock)
{
    public int Total => InStock + LowStock + OutOfStock;
}

public sealed record LowStockAlert(int ProductId, string Sku, string Name, int Quantity, int ReorderLevel);

public sealed record CategoryStockValue(string CategoryName, decimal Value);

public sealed record ActivityEntry(string Kind, string Title, string Detail, DateTime At);

public sealed record RecentSale(
    int Id,
    string InvoiceNumber,
    DateTime At,
    string? CustomerName,
    PaymentMethod? Method,
    decimal Total);

public sealed record TopProduct(string Sku, string Name, int UnitsSold, decimal Revenue);

public sealed record SalesByMethod(PaymentMethod Method, int Count, decimal Amount);

public sealed record DashboardSummary(
    DashboardRange Range,
    DashboardMetric Sales,
    DashboardMetric Orders,
    DashboardMetric GrossProfit,
    DashboardMetric NewCustomers,
    int LowStockCount,
    int OutOfStockCount,
    decimal ExchangedAmount,
    decimal OutstandingAmount,
    StockStatusBreakdown StockStatus,
    decimal StockValue,
    IReadOnlyList<LowStockAlert> LowStockAlerts,
    IReadOnlyList<CategoryStockValue> StockValueByCategory,
    IReadOnlyList<RecentSale> RecentSales,
    IReadOnlyList<TopProduct> TopProducts,
    IReadOnlyList<SalesByMethod> SalesByMethod,
    IReadOnlyList<ActivityEntry> RecentActivities)
{
    public int TotalProducts => StockStatus.Total;
}

public sealed class GetDashboardSummaryHandler(IApplicationDbContext db)
{
    private const int ListSize = 5;

    /// <summary>
    /// The donut palette seats four hues that stay distinguishable under colour-vision
    /// deficiency; everything past the fourth category is folded into one "Other" slice.
    /// </summary>
    private const int MaxCategorySlices = 4;

    public async Task<DashboardSummary> HandleAsync(
        DashboardRange range,
        CancellationToken cancellationToken = default)
    {
        var previous = range.Previous();

        // Current and previous periods are adjacent, so each pair of figures is read in a
        // single pass over the combined window, split on the day the current period starts.
        var sales = await SalesAsync(range, previous, cancellationToken);
        var profit = await GrossProfitAsync(range, previous, cancellationToken);
        var customers = await NewCustomersAsync(range, previous, cancellationToken);

        var paid = await PaidAsync(range, cancellationToken);

        // Products left-joined to their stock row, so a product that has never been
        // purchased counts as zero instead of dropping out of the numbers.
        var withStock = db.Products
            .AsNoTracking()
            .Select(p => new
            {
                p.Id,
                p.Sku,
                p.Name,
                p.ReorderLevel,
                Quantity = db.InventoryItems
                    .Where(i => i.ProductId == p.Id)
                    .Select(i => (int?)i.Quantity)
                    .FirstOrDefault() ?? 0
            });

        var totalProducts = await db.Products.CountAsync(cancellationToken);

        var outOfStock = await withStock.CountAsync(p => p.Quantity == 0, cancellationToken);

        var lowStock = await withStock
            .CountAsync(p => p.Quantity > 0 && p.Quantity <= p.ReorderLevel, cancellationToken);

        var stockValue = await db.InventoryItems
            .SumAsync(i => (decimal?)(i.Quantity * i.Product.PurchasePrice), cancellationToken) ?? 0m;

        // Scarcest first: the product closest to running out is the one to reorder.
        var lowStockAlerts = await withStock
            .Where(p => p.Quantity > 0 && p.Quantity <= p.ReorderLevel)
            .OrderBy(p => p.Quantity)
            .ThenBy(p => p.Name)
            .Take(ListSize)
            .Select(p => new LowStockAlert(p.Id, p.Sku, p.Name, p.Quantity, p.ReorderLevel))
            .ToListAsync(cancellationToken);

        return new DashboardSummary(
            range,
            new DashboardMetric(sales.Amount, sales.PreviousAmount),
            new DashboardMetric(sales.Count, sales.PreviousCount),
            new DashboardMetric(profit.Current, profit.Previous),
            new DashboardMetric(customers.Current, customers.Previous),
            lowStock,
            outOfStock,
            await ExchangedAsync(range, cancellationToken),
            sales.Amount - paid,
            new StockStatusBreakdown(totalProducts - lowStock - outOfStock, lowStock, outOfStock),
            stockValue,
            lowStockAlerts,
            await GetStockValueByCategoryAsync(cancellationToken),
            await GetRecentSalesAsync(range, cancellationToken),
            await GetTopProductsAsync(range, cancellationToken),
            await GetSalesByMethodAsync(range, cancellationToken),
            await GetRecentActivitiesAsync(cancellationToken));
    }

    /// <summary>
    /// Sales are stamped in UTC but the shop thinks in its own days, so the calendar range
    /// is widened into the matching UTC window using the server's own time zone.
    /// </summary>
    private static (DateTime FromUtc, DateTime ToUtc) Window(DashboardRange range)
    {
        var from = DateTime.SpecifyKind(range.From.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
        var toExclusive = DateTime.SpecifyKind(range.To.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);

        return (from.ToUniversalTime(), toExclusive.ToUniversalTime());
    }

    /// <summary>
    /// Sale totals and order count for the current and previous periods in one pass.
    /// Conditional sums do the splitting in the database so the dashboard costs one round
    /// trip here instead of four.
    /// </summary>
    private async Task<(decimal Amount, int Count, decimal PreviousAmount, int PreviousCount)>
        SalesAsync(DashboardRange range, DashboardRange previous, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = Window(range);
        var (previousFromUtc, _) = Window(previous);

        var totals = await db.Sales
            .AsNoTracking()
            .Where(s => s.CreatedAt >= previousFromUtc && s.CreatedAt < toUtc)
            .GroupBy(s => 1)
            .Select(g => new
            {
                Amount = g.Sum(s =>
                    s.Status == SaleStatus.Completed && s.CreatedAt >= fromUtc ? s.TotalAmount : 0m),
                Count = g.Sum(s =>
                    s.Status == SaleStatus.Completed && s.CreatedAt >= fromUtc ? 1 : 0),
                PreviousAmount = g.Sum(s =>
                    s.Status == SaleStatus.Completed && s.CreatedAt < fromUtc ? s.TotalAmount : 0m),
                PreviousCount = g.Sum(s =>
                    s.Status == SaleStatus.Completed && s.CreatedAt < fromUtc ? 1 : 0)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return totals is null
            ? (0m, 0, 0m, 0)
            : (totals.Amount, totals.Count, totals.PreviousAmount, totals.PreviousCount);
    }

    /// <summary>
    /// Sale price less what the stock currently costs to replace. The MVP keeps no cost
    /// snapshot on the sale line, so this follows the product's present purchase price.
    /// </summary>
    private async Task<(decimal Current, decimal Previous)> GrossProfitAsync(
        DashboardRange range,
        DashboardRange previous,
        CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = Window(range);
        var (previousFromUtc, _) = Window(previous);

        var totals = await db.SaleItems
            .AsNoTracking()
            .Where(i => i.Sale.Status == SaleStatus.Completed
                && i.Sale.CreatedAt >= previousFromUtc
                && i.Sale.CreatedAt < toUtc)
            .GroupBy(i => 1)
            .Select(g => new
            {
                Current = g.Sum(i => i.Sale.CreatedAt >= fromUtc
                    ? i.LineTotal - i.Quantity * i.Product.PurchasePrice
                    : 0m),
                Previous = g.Sum(i => i.Sale.CreatedAt < fromUtc
                    ? i.LineTotal - i.Quantity * i.Product.PurchasePrice
                    : 0m)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return totals is null ? (0m, 0m) : (totals.Current, totals.Previous);
    }

    private async Task<(int Current, int Previous)> NewCustomersAsync(
        DashboardRange range,
        DashboardRange previous,
        CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = Window(range);
        var (previousFromUtc, _) = Window(previous);

        var totals = await db.Customers
            .AsNoTracking()
            .Where(c => c.CreatedAt >= previousFromUtc && c.CreatedAt < toUtc)
            .GroupBy(c => 1)
            .Select(g => new
            {
                Current = g.Sum(c => c.CreatedAt >= fromUtc ? 1 : 0),
                Previous = g.Sum(c => c.CreatedAt < fromUtc ? 1 : 0)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return totals is null ? (0, 0) : (totals.Current, totals.Previous);
    }

    /// <summary>
    /// What has actually been collected. Checkout always settles a sale in full today, so
    /// billed minus paid is zero until partial payment exists.
    /// </summary>
    private async Task<decimal> PaidAsync(DashboardRange range, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = Window(range);

        return await db.Payments
            .AsNoTracking()
            .Where(p => p.Sale.Status == SaleStatus.Completed
                && p.Sale.CreatedAt >= fromUtc
                && p.Sale.CreatedAt < toUtc)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
    }

    private async Task<IReadOnlyList<CategoryStockValue>> GetStockValueByCategoryAsync(
        CancellationToken cancellationToken)
    {
        // Driven from the category side: one correlated sum per category translates
        // cleanly, where grouping stock rows by a two-hop navigation does not.
        // A shop has a handful of categories, so dropping the empty ones and
        // ordering them happens in memory rather than in a second round trip.
        var byCategory = (await db.Categories
                .AsNoTracking()
                .Select(c => new CategoryStockValue(
                    c.Name,
                    db.InventoryItems
                        .Where(i => i.Product.CategoryId == c.Id)
                        .Sum(i => (decimal?)(i.Quantity * i.Product.PurchasePrice)) ?? 0m))
                .ToListAsync(cancellationToken))
            .Where(c => c.Value > 0)
            .OrderByDescending(c => c.Value)
            .ToList();

        if (byCategory.Count <= MaxCategorySlices)
        {
            return byCategory;
        }

        var top = byCategory.Take(MaxCategorySlices).ToList();
        top.Add(new CategoryStockValue("Other", byCategory.Skip(MaxCategorySlices).Sum(c => c.Value)));

        return top;
    }

    /// <summary>
    /// The value of goods handed back through exchanges in the period. No money leaves the
    /// shop on an exchange, so this measures merchandise coming back, not refunds paid.
    /// </summary>
    private async Task<decimal> ExchangedAsync(DashboardRange range, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = Window(range);

        return await db.Exchanges
            .AsNoTracking()
            .Where(e => e.CreatedAt >= fromUtc && e.CreatedAt < toUtc)
            .SumAsync(e => (decimal?)e.ReturnedAmount, cancellationToken) ?? 0m;
    }

    /// <summary>The period's own sales, newest first. Cancelled sales never took money.</summary>
    private async Task<IReadOnlyList<RecentSale>> GetRecentSalesAsync(
        DashboardRange range,
        CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = Window(range);

        return await db.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed && s.CreatedAt >= fromUtc && s.CreatedAt < toUtc)
            .OrderByDescending(s => s.Id)
            .Take(ListSize)
            .Select(s => new RecentSale(
                s.Id,
                s.InvoiceNumber,
                s.CreatedAt,
                s.Customer == null ? null : s.Customer.Name,
                s.Payment == null ? null : (PaymentMethod?)s.Payment.Method,
                s.TotalAmount))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// What actually moved in the period, ranked by units rather than money so a
    /// cheap fast-mover is not hidden behind one expensive frame.
    /// </summary>
    private async Task<IReadOnlyList<TopProduct>> GetTopProductsAsync(
        DashboardRange range,
        CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = Window(range);

        return await db.SaleItems
            .AsNoTracking()
            .Where(i => i.Sale.Status == SaleStatus.Completed
                && i.Sale.CreatedAt >= fromUtc
                && i.Sale.CreatedAt < toUtc)
            .GroupBy(i => new { i.Product.Sku, i.Product.Name })
            .OrderByDescending(g => g.Sum(i => i.Quantity))
            .ThenByDescending(g => g.Sum(i => i.LineTotal))
            .Take(ListSize)
            .Select(g => new TopProduct(
                g.Key.Sku,
                g.Key.Name,
                g.Sum(i => i.Quantity),
                g.Sum(i => i.LineTotal)))
            .ToListAsync(cancellationToken);
    }

    /// <summary>How the period's takings were settled, largest share first.</summary>
    private async Task<IReadOnlyList<SalesByMethod>> GetSalesByMethodAsync(
        DashboardRange range,
        CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = Window(range);

        var rows = await db.Payments
            .AsNoTracking()
            .Where(p => p.Sale.Status == SaleStatus.Completed
                && p.Sale.CreatedAt >= fromUtc
                && p.Sale.CreatedAt < toUtc)
            .GroupBy(p => p.Method)
            .Select(g => new SalesByMethod(g.Key, g.Count(), g.Sum(p => p.Amount)))
            .ToListAsync(cancellationToken);

        return rows.OrderByDescending(r => r.Amount).ToList();
    }

    private async Task<IReadOnlyList<ActivityEntry>> GetRecentActivitiesAsync(CancellationToken cancellationToken)
    {
        var sales = await db.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed)
            .OrderByDescending(s => s.Id)
            .Take(ListSize)
            .Select(s => new ActivityEntry(
                "Sale",
                "Sale " + s.InvoiceNumber + " completed",
                s.Customer == null ? "Walk-in customer" : s.Customer.Name,
                s.CreatedAt))
            .ToListAsync(cancellationToken);

        var purchases = await db.Purchases
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Take(ListSize)
            .Select(p => new ActivityEntry(
                "Purchase",
                "Purchase #" + p.Id + " recorded",
                p.Supplier.Name,
                p.CreatedAt))
            .ToListAsync(cancellationToken);

        var products = await db.Products
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Take(ListSize)
            .Select(p => new ActivityEntry(
                "Product",
                "New product added: " + p.Name,
                "SKU: " + p.Sku,
                p.CreatedAt))
            .ToListAsync(cancellationToken);

        var suppliers = await db.Suppliers
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Take(ListSize)
            .Select(s => new ActivityEntry(
                "Supplier",
                "New supplier added: " + s.Name,
                s.Phone ?? "No phone on file",
                s.CreatedAt))
            .ToListAsync(cancellationToken);

        return sales
            .Concat(purchases)
            .Concat(products)
            .Concat(suppliers)
            .OrderByDescending(a => a.At)
            .Take(ListSize)
            .ToList();
    }
}
