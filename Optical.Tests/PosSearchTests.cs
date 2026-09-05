using Optical.Application.Features.POS;

namespace Optical.Tests;

/// <summary>
/// The POS search feeds the till, so it has to actually execute against the database.
/// These tests run the real query: a translation failure here would be a silent 500 at the counter.
/// </summary>
public class PosSearchTests
{
    [Fact]
    public async Task Search_returns_active_products_with_their_price_and_stock()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        test.SetStock(product.Id, 25);

        var handler = new SearchPosProductsHandler(test.Db);

        var results = (await handler.SearchAsync(null)).Items;

        var found = Assert.Single(results, p => p.Id == product.Id);
        Assert.Equal(product.Name, found.Name);
        Assert.Equal(product.Sku, found.Sku);
        Assert.Equal(150m, found.SalePrice);
        Assert.Equal(25, found.Stock);
    }

    [Fact]
    public async Task Search_matches_on_name_and_on_sku()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        test.SetStock(product.Id, 5);

        var handler = new SearchPosProductsHandler(test.Db);

        Assert.Single((await handler.SearchAsync(product.Name[..8])).Items);
        Assert.Single((await handler.SearchAsync(product.Sku)).Items);
        Assert.Empty((await handler.SearchAsync("no-such-product")).Items);
    }

    [Fact]
    public async Task Search_reports_zero_stock_when_the_product_has_no_inventory_row()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();

        var handler = new SearchPosProductsHandler(test.Db);

        var found = Assert.Single((await handler.SearchAsync(null)).Items, p => p.Id == product.Id);
        Assert.Equal(0, found.Stock);
    }

    [Fact]
    public async Task Search_excludes_inactive_products()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct(isActive: false);
        test.SetStock(product.Id, 10);

        var handler = new SearchPosProductsHandler(test.Db);

        Assert.Empty((await handler.SearchAsync(null)).Items);
    }

    [Fact]
    public async Task Rejected_cart_is_rebuilt_from_database_prices()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        test.SetStock(product.Id, 7);

        var handler = new SearchPosProductsHandler(test.Db);

        var found = Assert.Single(await handler.GetByIdsAsync([product.Id]));
        Assert.Equal(150m, found.SalePrice);
        Assert.Equal(7, found.Stock);

        Assert.Empty(await handler.GetByIdsAsync([]));
    }

    [Fact]
    public async Task Total_count_reports_every_match_even_when_the_list_is_capped()
    {
        using var test = new TestDatabase();

        for (var i = 0; i < SearchPosProductsHandler.MaxResults + 5; i++)
        {
            test.AddProduct();
        }

        var results = await new SearchPosProductsHandler(test.Db).SearchAsync(null);

        Assert.Equal(SearchPosProductsHandler.MaxResults, results.Items.Count);
        Assert.Equal(SearchPosProductsHandler.MaxResults + 5, results.TotalCount);
    }

    [Fact]
    public async Task Category_filter_narrows_the_list_and_the_count()
    {
        using var test = new TestDatabase();
        var wanted = test.AddProduct();
        test.AddProduct();

        var handler = new SearchPosProductsHandler(test.Db);

        var all = await handler.SearchAsync(null);
        var filtered = await handler.SearchAsync(null, wanted.CategoryId);

        Assert.Equal(2, all.TotalCount);
        Assert.Equal(1, filtered.TotalCount);
        Assert.Equal(wanted.Id, Assert.Single(filtered.Items).Id);
    }

    [Fact]
    public async Task Sorting_orders_by_name_and_by_price_in_both_directions()
    {
        using var test = new TestDatabase();
        var cheap = test.AddProduct();
        var dear = test.AddProduct();

        cheap.SalePrice = 10m;
        dear.SalePrice = 900m;
        await test.Db.SaveChangesAsync();

        var handler = new SearchPosProductsHandler(test.Db);

        var lowFirst = await handler.SearchAsync(null, null, PosProductSort.PriceLowToHigh);
        var highFirst = await handler.SearchAsync(null, null, PosProductSort.PriceHighToLow);
        var nameUp = await handler.SearchAsync(null, null, PosProductSort.NameAscending);
        var nameDown = await handler.SearchAsync(null, null, PosProductSort.NameDescending);

        Assert.Equal(cheap.Id, lowFirst.Items[0].Id);
        Assert.Equal(dear.Id, highFirst.Items[0].Id);
        Assert.Equal(nameUp.Items.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal),
            nameUp.Items.Select(p => p.Name));
        Assert.Equal(nameUp.Items.Select(p => p.Id).Reverse(), nameDown.Items.Select(p => p.Id));
    }
}
