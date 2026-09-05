using Microsoft.EntityFrameworkCore;
using Optical.Application.Features.POS;
using Optical.Application.Features.Products;
using Optical.Domain.Exceptions;

namespace Optical.Tests;

/// <summary>
/// Barcodes on products, and scanning them at the till. The whole feature rests on one code
/// meaning one product, so most of this is about what must be refused.
/// </summary>
public class BarcodeTests
{
    private static CreateProductCommand NewProduct(
        TestDatabase test,
        string? barcode = null,
        bool generate = false)
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
            Barcode: barcode,
            GenerateBarcode: generate);
    }

    [Fact]
    public async Task A_scanned_barcode_is_stored_in_upper_case()
    {
        using var test = new TestDatabase();

        var id = await new CreateProductHandler(test.Db).HandleAsync(NewProduct(test, "abc-123"));
        var product = await test.Db.Products.AsNoTracking().FirstAsync(p => p.Id == id);

        Assert.Equal("ABC-123", product.Barcode);
    }

    [Fact]
    public async Task No_barcode_is_stored_as_null_so_many_products_can_go_without_one()
    {
        using var test = new TestDatabase();
        var handler = new CreateProductHandler(test.Db);

        var first = await handler.HandleAsync(NewProduct(test, "   "));
        var second = await handler.HandleAsync(NewProduct(test));

        // An empty string would have collided on the unique index here.
        Assert.Null(await test.Db.Products.AsNoTracking()
            .Where(p => p.Id == first).Select(p => p.Barcode).FirstAsync());

        Assert.Null(await test.Db.Products.AsNoTracking()
            .Where(p => p.Id == second).Select(p => p.Barcode).FirstAsync());
    }

    [Fact]
    public async Task The_same_barcode_cannot_go_on_two_products()
    {
        using var test = new TestDatabase();
        var handler = new CreateProductHandler(test.Db);

        await handler.HandleAsync(NewProduct(test, "5901234123457"));

        var clash = await Assert.ThrowsAsync<DomainException>(
            () => handler.HandleAsync(NewProduct(test, "5901234123457")));

        Assert.Contains("already used", clash.Message);
    }

    [Fact]
    public async Task A_product_keeps_its_own_barcode_when_edited()
    {
        using var test = new TestDatabase();

        var id = await new CreateProductHandler(test.Db).HandleAsync(NewProduct(test, "ABC-123"));
        var product = await test.Db.Products.FirstAsync(p => p.Id == id);

        // Saving the same product again must not read its own barcode as a clash.
        await new UpdateProductHandler(test.Db).HandleAsync(new UpdateProductCommand(
            id,
            product.Sku,
            "Renamed frame",
            product.CategoryId,
            product.BrandId,
            product.PurchasePrice,
            product.SalePrice,
            product.ReorderLevel,
            IsActive: true,
            Barcode: "ABC-123"));

        var updated = await test.Db.Products.AsNoTracking().FirstAsync(p => p.Id == id);

        Assert.Equal("ABC-123", updated.Barcode);
        Assert.Equal("Renamed frame", updated.Name);
    }

    [Theory]
    [InlineData("ABC_123")]
    [InlineData("ABC 123")]
    [InlineData("ABC#123")]
    public async Task A_barcode_the_printer_cannot_carry_is_refused(string barcode)
    {
        using var test = new TestDatabase();

        await Assert.ThrowsAsync<DomainException>(
            () => new CreateProductHandler(test.Db).HandleAsync(NewProduct(test, barcode)));
    }

    [Fact]
    public async Task Generated_barcodes_run_in_sequence()
    {
        using var test = new TestDatabase();
        var handler = new CreateProductHandler(test.Db);

        var first = await handler.HandleAsync(NewProduct(test, generate: true));
        var second = await handler.HandleAsync(NewProduct(test, generate: true));

        Assert.Equal("P000001", await test.Db.Products.AsNoTracking()
            .Where(p => p.Id == first).Select(p => p.Barcode).FirstAsync());

        Assert.Equal("P000002", await test.Db.Products.AsNoTracking()
            .Where(p => p.Id == second).Select(p => p.Barcode).FirstAsync());
    }

    [Fact]
    public async Task A_typed_barcode_wins_over_generating_one()
    {
        using var test = new TestDatabase();

        var id = await new CreateProductHandler(test.Db).HandleAsync(
            NewProduct(test, barcode: "5901234123457", generate: true));

        var product = await test.Db.Products.AsNoTracking().FirstAsync(p => p.Id == id);

        Assert.Equal("5901234123457", product.Barcode);
    }

    [Fact]
    public async Task Scanning_finds_the_product_whatever_case_it_is_typed_in()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        product.Barcode = "ABC-123";
        test.Db.SaveChanges();
        test.SetStock(product.Id, 4);

        var found = await new SearchPosProductsHandler(test.Db).FindByBarcodeAsync(" abc-123 ");

        Assert.NotNull(found);
        Assert.Equal(product.Id, found!.Id);
        Assert.Equal(4, found.Stock);
    }

    [Fact]
    public async Task Scanning_matches_the_whole_code_and_not_part_of_it()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        product.Barcode = "ABC-1234";
        test.Db.SaveChanges();

        // A partial match would ring up the wrong frame, which is worse than finding nothing.
        Assert.Null(await new SearchPosProductsHandler(test.Db).FindByBarcodeAsync("ABC-123"));
    }

    [Fact]
    public async Task An_inactive_product_cannot_be_scanned_into_a_sale()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct(isActive: false);
        product.Barcode = "ABC-123";
        test.Db.SaveChanges();

        Assert.Null(await new SearchPosProductsHandler(test.Db).FindByBarcodeAsync("ABC-123"));
    }

    [Fact]
    public async Task Searching_the_till_by_barcode_finds_the_product()
    {
        using var test = new TestDatabase();
        var product = test.AddProduct();
        product.Barcode = "5901234123457";
        test.Db.SaveChanges();
        test.SetStock(product.Id, 2);

        var results = await new SearchPosProductsHandler(test.Db).SearchAsync("590123");

        Assert.Equal(product.Id, Assert.Single(results.Items).Id);
    }
}
