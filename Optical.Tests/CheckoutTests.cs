using Microsoft.EntityFrameworkCore;
using Optical.Application.Features.POS;
using Optical.Domain.Entities;
using Optical.Domain.Enums;
using Optical.Domain.Exceptions;

namespace Optical.Tests;

public class CheckoutTests
{
    [Fact]
    public async Task Checkout_creates_the_sale_its_item_and_its_payment()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        test.SetStock(product.Id, 10);

        var handler = new CheckoutHandler(test.Db);

        var result = await handler.HandleAsync(new CheckoutCommand(
            null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 2)]));

        var sale = await test.Db.Sales
            .AsNoTracking()
            .Include(s => s.Items)
            .Include(s => s.Payment)
            .FirstAsync(s => s.Id == result.SaleId);

        Assert.Equal(sale.InvoiceNumber, result.InvoiceNumber);
        Assert.Equal(sale.TotalAmount, result.TotalAmount);

        Assert.Equal(SaleStatus.Completed, sale.Status);
        Assert.Null(sale.CustomerId);
        Assert.Equal(300m, sale.TotalAmount);

        var item = Assert.Single(sale.Items);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(150m, item.UnitPrice);
        Assert.Equal(300m, item.LineTotal);

        Assert.NotNull(sale.Payment);
        Assert.Equal(PaymentMethod.Cash, sale.Payment!.Method);
        Assert.Equal(300m, sale.Payment.Amount);
    }

    [Fact]
    public async Task Checkout_decreases_inventory()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        test.SetStock(product.Id, 10);

        var handler = new CheckoutHandler(test.Db);

        await handler.HandleAsync(new CheckoutCommand(
            null, PaymentMethod.Card, [new CheckoutItemCommand(product.Id, 3)]));

        Assert.Equal(7, test.StockOf(product.Id));
    }

    [Fact]
    public async Task Checkout_handles_several_products_in_one_sale()
    {
        using var test = new TestDatabase();
        var first = test.AddProduct();
        var second = test.AddProduct();
        test.SetStock(first.Id, 5);
        test.SetStock(second.Id, 5);

        var handler = new CheckoutHandler(test.Db);

        var result = await handler.HandleAsync(new CheckoutCommand(
            null,
            PaymentMethod.MobileBanking,
            [
                new CheckoutItemCommand(first.Id, 2),
                new CheckoutItemCommand(second.Id, 1)
            ]));

        var sale = await test.Db.Sales.AsNoTracking().Include(s => s.Items).FirstAsync(s => s.Id == result.SaleId);

        Assert.Equal(2, sale.Items.Count);
        Assert.Equal(450m, sale.TotalAmount);
        Assert.Equal(3, test.StockOf(first.Id));
        Assert.Equal(4, test.StockOf(second.Id));
    }

    [Fact]
    public async Task Sale_total_uses_the_database_price_not_anything_sent_by_the_browser()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        test.SetStock(product.Id, 10);

        // The shop changes the price after the cashier's screen was loaded.
        var tracked = await test.Db.Products.FirstAsync(p => p.Id == product.Id);
        tracked.SalePrice = 200m;
        await test.Db.SaveChangesAsync();

        var handler = new CheckoutHandler(test.Db);

        var result = await handler.HandleAsync(new CheckoutCommand(
            null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 2)]));

        var sale = await test.Db.Sales.AsNoTracking().Include(s => s.Items).FirstAsync(s => s.Id == result.SaleId);

        Assert.Equal(400m, sale.TotalAmount);
        Assert.Equal(200m, sale.Items.Single().UnitPrice);
    }

    [Fact]
    public async Task Sale_can_be_linked_to_a_customer()
    {
        using var test = new TestDatabase();
        var customer = test.AddCustomer();
        var product = test.AddProduct();
        test.SetStock(product.Id, 4);

        var handler = new CheckoutHandler(test.Db);

        var result = await handler.HandleAsync(new CheckoutCommand(
            customer.Id, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 1)]));

        var sale = await test.Db.Sales.AsNoTracking().FirstAsync(s => s.Id == result.SaleId);

        Assert.Equal(customer.Id, sale.CustomerId);
    }

    [Fact]
    public async Task Empty_cart_is_rejected()
    {
        using var test = new TestDatabase();

        var handler = new CheckoutHandler(test.Db);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CheckoutCommand(null, PaymentMethod.Cash, [])));
    }

    [Fact]
    public async Task Insufficient_stock_is_rejected_and_nothing_is_saved()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        test.SetStock(product.Id, 2);

        var handler = new CheckoutHandler(test.Db);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CheckoutCommand(
                null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 3)])));

        Assert.Empty(test.Db.Sales.AsNoTracking());
        Assert.Equal(2, test.StockOf(product.Id));
    }

    [Fact]
    public async Task Product_with_no_stock_row_at_all_is_rejected()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();

        var handler = new CheckoutHandler(test.Db);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CheckoutCommand(
                null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 1)])));

        Assert.Empty(test.Db.Sales.AsNoTracking());
    }

    [Fact]
    public async Task Inactive_product_is_rejected()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct(isActive: false);
        test.SetStock(product.Id, 10);

        var handler = new CheckoutHandler(test.Db);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CheckoutCommand(
                null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 1)])));

        Assert.Equal(10, test.StockOf(product.Id));
    }

    [Fact]
    public async Task Quantity_of_zero_or_less_is_rejected()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        test.SetStock(product.Id, 10);

        var handler = new CheckoutHandler(test.Db);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CheckoutCommand(
                null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 0)])));

        Assert.Equal(10, test.StockOf(product.Id));
    }

    [Fact]
    public async Task Unknown_payment_method_is_rejected()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        test.SetStock(product.Id, 10);

        var handler = new CheckoutHandler(test.Db);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CheckoutCommand(
                null, (PaymentMethod)99, [new CheckoutItemCommand(product.Id, 1)])));

        Assert.Empty(test.Db.Sales.AsNoTracking());
    }

    [Fact]
    public async Task Inactive_customer_is_rejected()
    {
        using var test = new TestDatabase();
        var customer = test.AddCustomer(isActive: false);
        var product = test.AddProduct();
        test.SetStock(product.Id, 10);

        var handler = new CheckoutHandler(test.Db);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CheckoutCommand(
                customer.Id, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 1)])));

        Assert.Empty(test.Db.Sales.AsNoTracking());
        Assert.Equal(10, test.StockOf(product.Id));
    }

    [Fact]
    public async Task Same_product_twice_is_rejected()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        test.SetStock(product.Id, 10);

        var handler = new CheckoutHandler(test.Db);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CheckoutCommand(
                null,
                PaymentMethod.Cash,
                [
                    new CheckoutItemCommand(product.Id, 1),
                    new CheckoutItemCommand(product.Id, 2)
                ])));
    }

    [Fact]
    public async Task One_bad_line_rolls_the_whole_sale_back()
    {
        using var test = new TestDatabase();
        var good = test.AddProduct();
        var short_ = test.AddProduct();
        test.SetStock(good.Id, 10);
        test.SetStock(short_.Id, 1);

        var handler = new CheckoutHandler(test.Db);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CheckoutCommand(
                null,
                PaymentMethod.Cash,
                [
                    new CheckoutItemCommand(good.Id, 2),
                    new CheckoutItemCommand(short_.Id, 5)
                ])));

        Assert.Empty(test.Db.Sales.AsNoTracking());
        Assert.Empty(test.Db.SaleItems.AsNoTracking());
        Assert.Empty(test.Db.Payments.AsNoTracking());
        Assert.Equal(10, test.StockOf(good.Id));
        Assert.Equal(1, test.StockOf(short_.Id));
    }

    [Fact]
    public async Task Unknown_product_rolls_the_whole_sale_back()
    {
        using var test = new TestDatabase();
        var good = test.AddProduct();
        test.SetStock(good.Id, 10);

        var handler = new CheckoutHandler(test.Db);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CheckoutCommand(
                null,
                PaymentMethod.Cash,
                [
                    new CheckoutItemCommand(good.Id, 2),
                    new CheckoutItemCommand(9999, 1)
                ])));

        Assert.Empty(test.Db.Sales.AsNoTracking());
        Assert.Equal(10, test.StockOf(good.Id));
    }

    /// <summary>Puts a running offer on a product, so the till has something to apply.</summary>
    private static void Discount(TestDatabase test, Product product, decimal percent, int days = 1)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        product.DiscountPercent = percent;
        product.DiscountStartsOn = today;
        product.DiscountEndsOn = today.AddDays(days - 1);
        test.Db.SaveChanges();
    }

    [Fact]
    public async Task Checkout_applies_the_products_own_discount()
    {
        using var test = new TestDatabase();
        var discounted = test.AddProduct();
        var fullPrice = test.AddProduct();
        test.SetStock(discounted.Id, 10);
        test.SetStock(fullPrice.Id, 10);

        Discount(test, discounted, percent: 5m);

        var handler = new CheckoutHandler(test.Db);

        var result = await handler.HandleAsync(new CheckoutCommand(
            null,
            PaymentMethod.Cash,
            [
                new CheckoutItemCommand(discounted.Id, 2),
                new CheckoutItemCommand(fullPrice.Id, 1)
            ]));

        var sale = await test.Db.Sales
            .AsNoTracking()
            .Include(s => s.Items)
            .Include(s => s.Payment)
            .FirstAsync(s => s.Id == result.SaleId);

        // 2 x 150 = 300, less 5% = 285. The other line is untouched.
        Assert.Equal(450m, sale.SubTotal);
        Assert.Equal(15m, sale.DiscountAmount);
        Assert.Equal(435m, sale.TotalAmount);
        Assert.Equal(435m, sale.Payment!.Amount);

        var discountedLine = sale.Items.First(i => i.ProductId == discounted.Id);
        Assert.Equal(5m, discountedLine.DiscountPercent);
        Assert.Equal(15m, discountedLine.DiscountAmount);
        Assert.Equal(285m, discountedLine.LineTotal);

        var fullPriceLine = sale.Items.First(i => i.ProductId == fullPrice.Id);
        Assert.Equal(0m, fullPriceLine.DiscountPercent);
        Assert.Equal(0m, fullPriceLine.DiscountAmount);
        Assert.Equal(150m, fullPriceLine.LineTotal);
    }

    [Fact]
    public async Task Checkout_ignores_an_offer_that_has_already_ended()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        test.SetStock(product.Id, 10);

        var today = DateOnly.FromDateTime(DateTime.Now);
        product.DiscountPercent = 10m;
        product.DiscountStartsOn = today.AddDays(-5);
        product.DiscountEndsOn = today.AddDays(-1);
        test.Db.SaveChanges();

        var handler = new CheckoutHandler(test.Db);

        var result = await handler.HandleAsync(new CheckoutCommand(
            null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 1)]));

        Assert.Equal(150m, result.TotalAmount);
        Assert.Equal(0m, result.DiscountAmount);
    }

    [Fact]
    public async Task Checkout_ignores_an_offer_that_has_not_started_yet()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        test.SetStock(product.Id, 10);

        var today = DateOnly.FromDateTime(DateTime.Now);
        product.DiscountPercent = 10m;
        product.DiscountStartsOn = today.AddDays(1);
        product.DiscountEndsOn = today.AddDays(7);
        test.Db.SaveChanges();

        var handler = new CheckoutHandler(test.Db);

        var result = await handler.HandleAsync(new CheckoutCommand(
            null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 1)]));

        Assert.Equal(150m, result.TotalAmount);
        Assert.Equal(0m, result.DiscountAmount);
    }

    [Fact]
    public async Task Checkout_applies_a_discount_on_its_last_valid_day()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        test.SetStock(product.Id, 10);

        // An offer set to run "for today" ends today, and today still counts.
        Discount(test, product, percent: 10m, days: 1);

        var handler = new CheckoutHandler(test.Db);

        var result = await handler.HandleAsync(new CheckoutCommand(
            null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 1)]));

        Assert.Equal(15m, result.DiscountAmount);
        Assert.Equal(135m, result.TotalAmount);
    }

    [Fact]
    public async Task Checkout_rounds_an_awkward_percentage_to_the_nearest_paisa()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        test.SetStock(product.Id, 10);

        // 7% of 150 is 10.5, which stores exactly; 7% of 3 x 150 is 31.50.
        Discount(test, product, percent: 7m);

        var handler = new CheckoutHandler(test.Db);

        var result = await handler.HandleAsync(new CheckoutCommand(
            null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 3)]));

        var sale = await test.Db.Sales
            .AsNoTracking()
            .Include(s => s.Items)
            .FirstAsync(s => s.Id == result.SaleId);

        Assert.Equal(31.50m, sale.DiscountAmount);
        Assert.Equal(418.50m, sale.TotalAmount);
        Assert.Equal(sale.TotalAmount, sale.Items.Sum(i => i.LineTotal));
    }

    [Fact]
    public async Task Checkout_allows_a_full_discount()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        test.SetStock(product.Id, 10);

        Discount(test, product, percent: 100m);

        var handler = new CheckoutHandler(test.Db);

        var result = await handler.HandleAsync(new CheckoutCommand(
            null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 1)]));

        Assert.Equal(150m, result.SubTotal);
        Assert.Equal(150m, result.DiscountAmount);
        Assert.Equal(0m, result.TotalAmount);
        Assert.Equal(9, test.StockOf(product.Id));
    }
}
