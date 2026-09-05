using Microsoft.EntityFrameworkCore;
using Optical.Application.Features.Products;
using Optical.Domain.Exceptions;

namespace Optical.Tests;

/// <summary>
/// The offer stored against a product, and when it counts. The till trusts these values
/// completely, so a half-filled offer must never reach the database.
/// </summary>
public class ProductDiscountTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);

    private static CreateProductCommand NewProduct(
        TestDatabase test,
        decimal percent = 0m,
        DateOnly? startsOn = null,
        DateOnly? endsOn = null)
    {
        var existing = test.AddProduct();

        return new CreateProductCommand(
            $"SKU-{Guid.NewGuid():N}"[..12],
            "Reading frame",
            existing.CategoryId,
            existing.BrandId,
            PurchasePrice: 100m,
            SalePrice: 150m,
            ReorderLevel: 5,
            DiscountPercent: percent,
            DiscountStartsOn: startsOn,
            DiscountEndsOn: endsOn);
    }

    [Fact]
    public async Task A_discount_is_stored_with_the_days_it_runs()
    {
        using var test = new TestDatabase();

        var id = await new CreateProductHandler(test.Db).HandleAsync(
            NewProduct(test, percent: 5m, startsOn: Today, endsOn: Today.AddDays(2)));

        var product = await test.Db.Products.AsNoTracking().FirstAsync(p => p.Id == id);

        Assert.Equal(5m, product.DiscountPercent);
        Assert.Equal(Today, product.DiscountStartsOn);
        Assert.Equal(Today.AddDays(2), product.DiscountEndsOn);
        Assert.Equal(5m, product.DiscountPercentOn(Today));
        Assert.Equal(5m, product.DiscountPercentOn(Today.AddDays(2)));
        Assert.Equal(0m, product.DiscountPercentOn(Today.AddDays(3)));
    }

    [Fact]
    public async Task A_discount_without_dates_is_refused()
    {
        using var test = new TestDatabase();

        await Assert.ThrowsAsync<DomainException>(() =>
            new CreateProductHandler(test.Db).HandleAsync(NewProduct(test, percent: 5m)));
    }

    [Fact]
    public async Task A_discount_that_ends_before_it_starts_is_refused()
    {
        using var test = new TestDatabase();

        await Assert.ThrowsAsync<DomainException>(() =>
            new CreateProductHandler(test.Db).HandleAsync(
                NewProduct(test, percent: 5m, startsOn: Today, endsOn: Today.AddDays(-1))));
    }

    [Fact]
    public async Task A_discount_over_a_hundred_percent_is_refused()
    {
        using var test = new TestDatabase();

        await Assert.ThrowsAsync<DomainException>(() =>
            new CreateProductHandler(test.Db).HandleAsync(
                NewProduct(test, percent: 101m, startsOn: Today, endsOn: Today)));
    }

    [Fact]
    public async Task Clearing_the_percentage_clears_the_dates_with_it()
    {
        using var test = new TestDatabase();

        var id = await new CreateProductHandler(test.Db).HandleAsync(
            NewProduct(test, percent: 5m, startsOn: Today, endsOn: Today.AddDays(6)));

        var product = await test.Db.Products.FirstAsync(p => p.Id == id);

        // The dates are still posted by the form; dropping the percentage must end the offer
        // outright, or it would quietly start applying again on the next edit.
        await new UpdateProductHandler(test.Db).HandleAsync(new UpdateProductCommand(
            id,
            product.Sku,
            product.Name,
            product.CategoryId,
            product.BrandId,
            product.PurchasePrice,
            product.SalePrice,
            product.ReorderLevel,
            IsActive: true,
            DiscountPercent: 0m,
            DiscountStartsOn: Today,
            DiscountEndsOn: Today.AddDays(6)));

        var updated = await test.Db.Products.AsNoTracking().FirstAsync(p => p.Id == id);

        Assert.Equal(0m, updated.DiscountPercent);
        Assert.Null(updated.DiscountStartsOn);
        Assert.Null(updated.DiscountEndsOn);
    }
}
