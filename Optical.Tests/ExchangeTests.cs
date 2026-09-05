using Microsoft.EntityFrameworkCore;
using Optical.Application.Features.Exchanges;
using Optical.Application.Features.POS;
using Optical.Domain.Entities;
using Optical.Domain.Enums;
using Optical.Domain.Exceptions;

namespace Optical.Tests;

public class ExchangeTests
{
    /// <summary>Rings up a sale so there is something to exchange against.</summary>
    private static async Task<(int SaleId, int SaleItemId, Product Product)> SellAsync(
        TestDatabase test,
        int quantity,
        int stock = 100)
    {
        var product = test.AddProduct();
        test.SetStock(product.Id, stock);

        var sale = await new CheckoutHandler(test.Db).HandleAsync(new CheckoutCommand(
            null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, quantity)]));

        var saleItemId = await test.Db.SaleItems
            .AsNoTracking()
            .Where(i => i.SaleId == sale.SaleId)
            .Select(i => i.Id)
            .FirstAsync();

        return (sale.SaleId, saleItemId, product);
    }

    /// <summary>A product priced however the test needs, with stock on the shelf.</summary>
    private static Product Stocked(TestDatabase test, decimal salePrice, int stock)
    {
        var product = test.AddProduct();
        product.SalePrice = salePrice;
        test.Db.SaveChanges();
        test.SetStock(product.Id, stock);

        return product;
    }

    [Fact]
    public async Task Even_swap_moves_stock_both_ways_and_collects_nothing()
    {
        using var test = new TestDatabase();
        var (saleId, saleItemId, sold) = await SellAsync(test, quantity: 1, stock: 10);
        var replacement = Stocked(test, salePrice: 150m, stock: 5);

        Assert.Equal(9, test.StockOf(sold.Id));

        var result = await new CreateExchangeHandler(test.Db).HandleAsync(new CreateExchangeCommand(
            saleId, ExchangeReason.PoorFit, "Frame did not fit", null,
            [new ExchangeReturnCommand(saleItemId, 1)],
            [new ExchangeReplacementCommand(replacement.Id, 1)]));

        Assert.StartsWith("EXC-", result.ExchangeNumber);
        Assert.Equal(150m, result.ReturnedAmount);
        Assert.Equal(150m, result.ReplacementAmount);
        Assert.Equal(0m, result.AmountPaid);

        // The old one is back on the shelf, the new one has left it.
        Assert.Equal(10, test.StockOf(sold.Id));
        Assert.Equal(4, test.StockOf(replacement.Id));

        var saved = await test.Db.Exchanges.AsNoTracking().FirstAsync(e => e.Id == result.ExchangeId);
        Assert.Null(saved.PaymentMethod);
        Assert.Equal(ExchangeReason.PoorFit, saved.Reason);
        Assert.Equal("Frame did not fit", saved.Note);
    }

    [Fact]
    public async Task A_dearer_replacement_collects_only_the_difference()
    {
        using var test = new TestDatabase();
        var (saleId, saleItemId, _) = await SellAsync(test, quantity: 1);
        var replacement = Stocked(test, salePrice: 400m, stock: 5);

        var result = await new CreateExchangeHandler(test.Db).HandleAsync(new CreateExchangeCommand(
            saleId, ExchangeReason.WrongPrescription, null, PaymentMethod.Card,
            [new ExchangeReturnCommand(saleItemId, 1)],
            [new ExchangeReplacementCommand(replacement.Id, 1)]));

        // Sold at 150.00, replaced with 400.00.
        Assert.Equal(150m, result.ReturnedAmount);
        Assert.Equal(400m, result.ReplacementAmount);
        Assert.Equal(250m, result.AmountPaid);

        var saved = await test.Db.Exchanges.AsNoTracking().FirstAsync(e => e.Id == result.ExchangeId);
        Assert.Equal(PaymentMethod.Card, saved.PaymentMethod);
    }

    [Fact]
    public async Task A_cheaper_replacement_is_refused_because_the_shop_never_pays_out()
    {
        using var test = new TestDatabase();
        var (saleId, saleItemId, sold) = await SellAsync(test, quantity: 1, stock: 10);
        var cheaper = Stocked(test, salePrice: 120m, stock: 5);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            new CreateExchangeHandler(test.Db).HandleAsync(new CreateExchangeCommand(
                saleId, ExchangeReason.WrongPrescription, null, null,
                [new ExchangeReturnCommand(saleItemId, 1)],
                [new ExchangeReplacementCommand(cheaper.Id, 1)])));

        Assert.Contains("must be worth at least 150.00", error.Message);

        // Nothing was saved, so no stock moved either way.
        Assert.Empty(test.Db.Exchanges);
        Assert.Equal(9, test.StockOf(sold.Id));
        Assert.Equal(5, test.StockOf(cheaper.Id));
    }

    [Fact]
    public async Task A_shortfall_can_be_covered_by_adding_another_item()
    {
        using var test = new TestDatabase();
        var (saleId, saleItemId, _) = await SellAsync(test, quantity: 1);
        var cheaper = Stocked(test, salePrice: 120m, stock: 5);
        var extra = Stocked(test, salePrice: 60m, stock: 5);

        var result = await new CreateExchangeHandler(test.Db).HandleAsync(new CreateExchangeCommand(
            saleId, ExchangeReason.WrongPrescription, null, PaymentMethod.Cash,
            [new ExchangeReturnCommand(saleItemId, 1)],
            [
                new ExchangeReplacementCommand(cheaper.Id, 1),
                new ExchangeReplacementCommand(extra.Id, 1)
            ]));

        // 120 + 60 = 180 against 150 returned.
        Assert.Equal(180m, result.ReplacementAmount);
        Assert.Equal(30m, result.AmountPaid);
    }

    [Fact]
    public async Task The_difference_needs_a_payment_method()
    {
        using var test = new TestDatabase();
        var (saleId, saleItemId, _) = await SellAsync(test, quantity: 1);
        var dearer = Stocked(test, salePrice: 400m, stock: 5);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            new CreateExchangeHandler(test.Db).HandleAsync(new CreateExchangeCommand(
                saleId, ExchangeReason.WrongPrescription, null, null,
                [new ExchangeReturnCommand(saleItemId, 1)],
                [new ExchangeReplacementCommand(dearer.Id, 1)])));

        Assert.Contains("owes an adjustment of 250.00", error.Message);
        Assert.Empty(test.Db.Exchanges);
    }

    [Fact]
    public async Task An_exchange_with_no_replacement_is_refused()
    {
        using var test = new TestDatabase();
        var (saleId, saleItemId, _) = await SellAsync(test, quantity: 2);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            new CreateExchangeHandler(test.Db).HandleAsync(new CreateExchangeCommand(
                saleId, ExchangeReason.WrongPrescription, null, null, [new ExchangeReturnCommand(saleItemId, 1)], [])));

        // This is the rule that stops an exchange being used as a refund.
        Assert.Contains("swapped, not refunded", error.Message);
    }

    [Fact]
    public async Task Exchanging_more_than_was_sold_is_rejected_across_repeated_exchanges()
    {
        using var test = new TestDatabase();
        var (saleId, saleItemId, sold) = await SellAsync(test, quantity: 3, stock: 10);
        var replacement = Stocked(test, salePrice: 150m, stock: 20);

        var handler = new CreateExchangeHandler(test.Db);

        await handler.HandleAsync(new CreateExchangeCommand(
            saleId, ExchangeReason.WrongPrescription, null, null,
            [new ExchangeReturnCommand(saleItemId, 2)],
            [new ExchangeReplacementCommand(replacement.Id, 2)]));

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CreateExchangeCommand(
                saleId, ExchangeReason.WrongPrescription, null, null,
                [new ExchangeReturnCommand(saleItemId, 2)],
                [new ExchangeReplacementCommand(replacement.Id, 2)])));

        Assert.Contains("Only 1", error.Message);
    }

    [Fact]
    public async Task A_replacement_with_too_little_stock_is_refused_and_nothing_moves()
    {
        using var test = new TestDatabase();
        var (saleId, saleItemId, sold) = await SellAsync(test, quantity: 1, stock: 10);
        var scarce = Stocked(test, salePrice: 200m, stock: 1);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            new CreateExchangeHandler(test.Db).HandleAsync(new CreateExchangeCommand(
                saleId, ExchangeReason.WrongPrescription, null, PaymentMethod.Cash,
                [new ExchangeReturnCommand(saleItemId, 1)],
                [new ExchangeReplacementCommand(scarce.Id, 3)])));

        Assert.Contains("Not enough stock", error.Message);

        Assert.Empty(test.Db.Exchanges);
        Assert.Equal(9, test.StockOf(sold.Id));
        Assert.Equal(1, test.StockOf(scarce.Id));
    }

    [Fact]
    public async Task Swapping_the_same_product_nets_the_stock_movement()
    {
        using var test = new TestDatabase();

        // One of the same product goes back, two come out: net one off the shelf.
        var (saleId, saleItemId, product) = await SellAsync(test, quantity: 1, stock: 10);

        Assert.Equal(9, test.StockOf(product.Id));

        var result = await new CreateExchangeHandler(test.Db).HandleAsync(new CreateExchangeCommand(
            saleId, ExchangeReason.CustomerChangedMind, "Wanted a spare pair", PaymentMethod.Cash,
            [new ExchangeReturnCommand(saleItemId, 1)],
            [new ExchangeReplacementCommand(product.Id, 2)]));

        Assert.Equal(150m, result.ReturnedAmount);
        Assert.Equal(300m, result.ReplacementAmount);
        Assert.Equal(150m, result.AmountPaid);

        // 9 + 1 back - 2 out = 8.
        Assert.Equal(8, test.StockOf(product.Id));
    }

    [Fact]
    public async Task An_inactive_replacement_is_refused()
    {
        using var test = new TestDatabase();
        var (saleId, saleItemId, _) = await SellAsync(test, quantity: 1);

        var retired = Stocked(test, salePrice: 200m, stock: 5);
        retired.IsActive = false;
        await test.Db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            new CreateExchangeHandler(test.Db).HandleAsync(new CreateExchangeCommand(
                saleId, ExchangeReason.WrongPrescription, null, PaymentMethod.Cash,
                [new ExchangeReturnCommand(saleItemId, 1)],
                [new ExchangeReplacementCommand(retired.Id, 1)])));

        Assert.Contains("inactive", error.Message);
    }

    [Fact]
    public async Task A_line_from_another_sale_is_rejected()
    {
        using var test = new TestDatabase();
        var first = await SellAsync(test, quantity: 1);
        var second = await SellAsync(test, quantity: 1);
        var replacement = Stocked(test, salePrice: 500m, stock: 5);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            new CreateExchangeHandler(test.Db).HandleAsync(new CreateExchangeCommand(
                first.SaleId, ExchangeReason.WrongPrescription, null, PaymentMethod.Cash,
                [new ExchangeReturnCommand(second.SaleItemId, 1)],
                [new ExchangeReplacementCommand(replacement.Id, 1)])));

        Assert.Contains("does not belong to this sale", error.Message);
    }

    [Fact]
    public async Task Sale_totals_are_left_alone_so_gross_sales_never_shift()
    {
        using var test = new TestDatabase();
        var (saleId, saleItemId, _) = await SellAsync(test, quantity: 2);
        var replacement = Stocked(test, salePrice: 150m, stock: 10);

        await new CreateExchangeHandler(test.Db).HandleAsync(new CreateExchangeCommand(
            saleId, ExchangeReason.WrongPrescription, null, null,
            [new ExchangeReturnCommand(saleItemId, 2)],
            [new ExchangeReplacementCommand(replacement.Id, 2)]));

        var sale = await test.Db.Sales.AsNoTracking().FirstAsync(s => s.Id == saleId);

        // Fully exchanged, yet the sale still reads as it was taken: gross stays gross.
        Assert.Equal(SaleStatus.Completed, sale.Status);
        Assert.Equal(300m, sale.TotalAmount);
    }

    [Fact]
    public async Task The_exchangeable_view_reports_what_is_left_and_what_can_be_swapped_into()
    {
        using var test = new TestDatabase();
        var (saleId, saleItemId, _) = await SellAsync(test, quantity: 5);
        var replacement = Stocked(test, salePrice: 150m, stock: 4);
        var outOfStock = Stocked(test, salePrice: 150m, stock: 0);

        await new CreateExchangeHandler(test.Db).HandleAsync(new CreateExchangeCommand(
            saleId, ExchangeReason.WrongPrescription, null, null,
            [new ExchangeReturnCommand(saleItemId, 2)],
            [new ExchangeReplacementCommand(replacement.Id, 2)]));

        var view = await new GetExchangeableSaleHandler(test.Db).HandleAsync(saleId);

        Assert.NotNull(view);
        var line = Assert.Single(view!.Lines);
        Assert.Equal(5, line.QuantitySold);
        Assert.Equal(2, line.QuantityAlreadyExchanged);
        Assert.Equal(3, line.QuantityExchangeable);

        // Only what the shop can actually hand over is offered.
        Assert.DoesNotContain(view.Replacements, r => r.Id == outOfStock.Id);
        Assert.All(view.Replacements, r => Assert.True(r.InStock > 0));
    }

    [Fact]
    public async Task The_reason_is_recorded_against_the_exchange()
    {
        using var test = new TestDatabase();
        var (saleId, saleItemId, _) = await SellAsync(test, quantity: 1);
        var replacement = Stocked(test, salePrice: 150m, stock: 5);

        var result = await new CreateExchangeHandler(test.Db).HandleAsync(new CreateExchangeCommand(
            saleId, ExchangeReason.DamagedOrDefective, "Left hinge snapped", null,
            [new ExchangeReturnCommand(saleItemId, 1)],
            [new ExchangeReplacementCommand(replacement.Id, 1)]));

        var saved = await test.Db.Exchanges.AsNoTracking().FirstAsync(e => e.Id == result.ExchangeId);

        Assert.Equal(ExchangeReason.DamagedOrDefective, saved.Reason);
        Assert.Equal("Left hinge snapped", saved.Note);
    }

    [Fact]
    public async Task Other_needs_an_explanation()
    {
        using var test = new TestDatabase();
        var (saleId, saleItemId, _) = await SellAsync(test, quantity: 1);
        var replacement = Stocked(test, salePrice: 150m, stock: 5);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            new CreateExchangeHandler(test.Db).HandleAsync(new CreateExchangeCommand(
                saleId, ExchangeReason.Other, "   ", null,
                [new ExchangeReturnCommand(saleItemId, 1)],
                [new ExchangeReplacementCommand(replacement.Id, 1)])));

        Assert.Contains("Describe the reason", error.Message);
        Assert.Empty(test.Db.Exchanges);

        // With a note it goes through.
        var result = await new CreateExchangeHandler(test.Db).HandleAsync(new CreateExchangeCommand(
            saleId, ExchangeReason.Other, "Gift for a relative", null,
            [new ExchangeReturnCommand(saleItemId, 1)],
            [new ExchangeReplacementCommand(replacement.Id, 1)]));

        Assert.NotEqual(0, result.ExchangeId);
    }

    [Fact]
    public async Task An_unknown_reason_is_rejected()
    {
        using var test = new TestDatabase();
        var (saleId, saleItemId, _) = await SellAsync(test, quantity: 1);
        var replacement = Stocked(test, salePrice: 150m, stock: 5);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            new CreateExchangeHandler(test.Db).HandleAsync(new CreateExchangeCommand(
                saleId, (ExchangeReason)99, null, null,
                [new ExchangeReturnCommand(saleItemId, 1)],
                [new ExchangeReplacementCommand(replacement.Id, 1)])));

        Assert.Contains("Select why", error.Message);
    }

    [Fact]
    public async Task Returning_a_discounted_line_refunds_the_discounted_rate()
    {
        using var test = new TestDatabase();
        var sold = test.AddProduct();
        test.SetStock(sold.Id, 10);

        // A 20% offer on a 150 frame: the customer paid 120 each, not 150.
        var today = DateOnly.FromDateTime(DateTime.Now);
        sold.DiscountPercent = 20m;
        sold.DiscountStartsOn = today;
        sold.DiscountEndsOn = today;
        test.Db.SaveChanges();

        var sale = await new CheckoutHandler(test.Db).HandleAsync(new CheckoutCommand(
            null, PaymentMethod.Cash, [new CheckoutItemCommand(sold.Id, 2)]));

        var saleItemId = await test.Db.SaleItems
            .AsNoTracking()
            .Where(i => i.SaleId == sale.SaleId)
            .Select(i => i.Id)
            .FirstAsync();

        var replacement = Stocked(test, salePrice: 150m, stock: 5);

        var result = await new CreateExchangeHandler(test.Db).HandleAsync(new CreateExchangeCommand(
            sale.SaleId, ExchangeReason.PoorFit, "Frame did not fit", PaymentMethod.Cash,
            [new ExchangeReturnCommand(saleItemId, 1)],
            [new ExchangeReplacementCommand(replacement.Id, 1)]));

        Assert.Equal(120m, result.ReturnedAmount);

        // The swap is for a full-price frame, so the customer covers the 30 difference.
        Assert.Equal(150m, result.ReplacementAmount);
        Assert.Equal(30m, result.AmountPaid);
    }

    [Fact]
    public async Task Returning_a_discounted_line_in_full_refunds_exactly_what_was_charged()
    {
        using var test = new TestDatabase();
        var sold = test.AddProduct();
        test.SetStock(sold.Id, 10);

        // 3 at 150 is 450, less 7% is 418.50, which does not divide cleanly over three units.
        var today = DateOnly.FromDateTime(DateTime.Now);
        sold.DiscountPercent = 7m;
        sold.DiscountStartsOn = today;
        sold.DiscountEndsOn = today;
        test.Db.SaveChanges();

        var sale = await new CheckoutHandler(test.Db).HandleAsync(new CheckoutCommand(
            null, PaymentMethod.Cash, [new CheckoutItemCommand(sold.Id, 3)]));

        var saleItemId = await test.Db.SaleItems
            .AsNoTracking()
            .Where(i => i.SaleId == sale.SaleId)
            .Select(i => i.Id)
            .FirstAsync();

        var replacement = Stocked(test, salePrice: 150m, stock: 5);

        var first = await new CreateExchangeHandler(test.Db).HandleAsync(new CreateExchangeCommand(
            sale.SaleId, ExchangeReason.PoorFit, "One did not fit", PaymentMethod.Cash,
            [new ExchangeReturnCommand(saleItemId, 1)],
            [new ExchangeReplacementCommand(replacement.Id, 1)]));

        var rest = await new CreateExchangeHandler(test.Db).HandleAsync(new CreateExchangeCommand(
            sale.SaleId, ExchangeReason.PoorFit, "The other two as well", PaymentMethod.Cash,
            [new ExchangeReturnCommand(saleItemId, 2)],
            [new ExchangeReplacementCommand(replacement.Id, 2)]));

        // Whatever the rounding, the shop hands back the 418.50 it took and no more.
        Assert.Equal(418.50m, first.ReturnedAmount + rest.ReturnedAmount);
    }
}
