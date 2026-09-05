using Microsoft.EntityFrameworkCore;
using Optical.Application.Features.Inventory;
using Optical.Application.Features.Purchases;
using Optical.Domain.Entities;
using Optical.Domain.Exceptions;

namespace Optical.Tests;

public class PurchaseTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    [Fact]
    public async Task Purchase_creates_stock_for_a_product_that_has_none()
    {
        using var test = new TestDatabase();
        var supplier = test.AddSupplier();
        var product = test.AddProduct();

        var handler = new CreatePurchaseHandler(test.Db);

        var id = await handler.HandleAsync(new CreatePurchaseCommand(
            supplier.Id,
            Today,
            "INV-001",
            [new CreatePurchaseItemCommand(product.Id, 10, 90m)]));

        Assert.True(id > 0);
        Assert.Equal(10, test.StockOf(product.Id));
    }

    [Fact]
    public async Task Purchase_adds_to_existing_stock()
    {
        using var test = new TestDatabase();
        var supplier = test.AddSupplier();
        var product = test.AddProduct();

        test.Db.InventoryItems.Add(new InventoryItem { ProductId = product.Id, Quantity = 4 });
        await test.Db.SaveChangesAsync();

        var handler = new CreatePurchaseHandler(test.Db);

        await handler.HandleAsync(new CreatePurchaseCommand(
            supplier.Id, Today, null, [new CreatePurchaseItemCommand(product.Id, 6, 90m)]));

        Assert.Equal(10, test.StockOf(product.Id));
    }

    [Fact]
    public async Task Purchase_total_is_calculated_on_the_server()
    {
        using var test = new TestDatabase();
        var supplier = test.AddSupplier();
        var first = test.AddProduct();
        var second = test.AddProduct();

        var handler = new CreatePurchaseHandler(test.Db);

        var id = await handler.HandleAsync(new CreatePurchaseCommand(
            supplier.Id,
            Today,
            null,
            [
                new CreatePurchaseItemCommand(first.Id, 3, 25.50m),
                new CreatePurchaseItemCommand(second.Id, 2, 10.00m)
            ]));

        var purchase = await test.Db.Purchases
            .AsNoTracking()
            .Include(p => p.Items)
            .FirstAsync(p => p.Id == id);

        Assert.Equal(96.50m, purchase.TotalAmount);
        Assert.Equal(76.50m, purchase.Items.Single(i => i.ProductId == first.Id).LineTotal);
        Assert.Equal(3, test.StockOf(first.Id));
        Assert.Equal(2, test.StockOf(second.Id));
    }

    [Fact]
    public async Task Purchase_with_no_items_is_rejected()
    {
        using var test = new TestDatabase();
        var supplier = test.AddSupplier();

        var handler = new CreatePurchaseHandler(test.Db);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CreatePurchaseCommand(supplier.Id, Today, null, [])));
    }

    [Fact]
    public async Task Invalid_quantity_rolls_the_whole_purchase_back()
    {
        using var test = new TestDatabase();
        var supplier = test.AddSupplier();
        var good = test.AddProduct();
        var bad = test.AddProduct();

        var handler = new CreatePurchaseHandler(test.Db);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CreatePurchaseCommand(
                supplier.Id,
                Today,
                null,
                [
                    new CreatePurchaseItemCommand(good.Id, 5, 90m),
                    new CreatePurchaseItemCommand(bad.Id, 0, 90m)
                ])));

        Assert.Empty(test.Db.Purchases.AsNoTracking());
        Assert.Empty(test.Db.PurchaseItems.AsNoTracking());
        Assert.Equal(0, test.StockOf(good.Id));
    }

    [Fact]
    public async Task Unknown_product_rolls_the_whole_purchase_back()
    {
        using var test = new TestDatabase();
        var supplier = test.AddSupplier();
        var good = test.AddProduct();

        var handler = new CreatePurchaseHandler(test.Db);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CreatePurchaseCommand(
                supplier.Id,
                Today,
                null,
                [
                    new CreatePurchaseItemCommand(good.Id, 5, 90m),
                    new CreatePurchaseItemCommand(9999, 1, 90m)
                ])));

        Assert.Empty(test.Db.Purchases.AsNoTracking());
        Assert.Equal(0, test.StockOf(good.Id));
    }

    [Fact]
    public async Task Inactive_product_is_rejected()
    {
        using var test = new TestDatabase();
        var supplier = test.AddSupplier();
        var product = test.AddProduct(isActive: false);

        var handler = new CreatePurchaseHandler(test.Db);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CreatePurchaseCommand(
                supplier.Id, Today, null, [new CreatePurchaseItemCommand(product.Id, 5, 90m)])));

        Assert.Equal(0, test.StockOf(product.Id));
    }

    [Fact]
    public async Task Inactive_supplier_is_rejected()
    {
        using var test = new TestDatabase();
        var supplier = test.AddSupplier(isActive: false);
        var product = test.AddProduct();

        var handler = new CreatePurchaseHandler(test.Db);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CreatePurchaseCommand(
                supplier.Id, Today, null, [new CreatePurchaseItemCommand(product.Id, 5, 90m)])));
    }

    [Fact]
    public async Task Same_product_twice_is_rejected()
    {
        using var test = new TestDatabase();
        var supplier = test.AddSupplier();
        var product = test.AddProduct();

        var handler = new CreatePurchaseHandler(test.Db);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CreatePurchaseCommand(
                supplier.Id,
                Today,
                null,
                [
                    new CreatePurchaseItemCommand(product.Id, 1, 90m),
                    new CreatePurchaseItemCommand(product.Id, 2, 90m)
                ])));
    }
}

public class InventoryTests
{
    [Fact]
    public async Task Adjustment_sets_the_counted_quantity()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();

        var handler = new AdjustStockHandler(test.Db);

        await handler.HandleAsync(new AdjustStockCommand(product.Id, 7));
        Assert.Equal(7, test.StockOf(product.Id));

        await handler.HandleAsync(new AdjustStockCommand(product.Id, 3));
        Assert.Equal(3, test.StockOf(product.Id));
    }

    [Fact]
    public async Task Adjustment_cannot_make_stock_negative()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();

        var handler = new AdjustStockHandler(test.Db);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new AdjustStockCommand(product.Id, -1)));

        Assert.Equal(0, test.StockOf(product.Id));
    }

    [Fact]
    public async Task Low_stock_includes_products_that_were_never_purchased()
    {
        using var test = new TestDatabase();
        var stocked = test.AddProduct(reorderLevel: 5);
        var neverPurchased = test.AddProduct(reorderLevel: 5);

        await new AdjustStockHandler(test.Db).HandleAsync(new AdjustStockCommand(stocked.Id, 50));

        var result = await new GetInventoryHandler(test.Db).HandleAsync(search: null, lowStockOnly: true);

        Assert.Equal(1, result.LowStockCount);
        Assert.Equal(neverPurchased.Id, Assert.Single(result.Items).ProductId);
    }
}
