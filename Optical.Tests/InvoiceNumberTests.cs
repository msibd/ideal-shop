using Microsoft.EntityFrameworkCore;
using Optical.Application.Features.POS;
using Optical.Domain.Entities;
using Optical.Domain.Enums;

namespace Optical.Tests;

public class InvoiceNumberTests
{
    [Fact]
    public void Number_reads_as_prefix_date_and_a_five_digit_sequence()
    {
        var number = InvoiceNumber.Build(InvoiceNumber.Prefix, new DateOnly(2026, 9, 5), 7);

        Assert.Equal("INV-20260905-00007", number);
        Assert.Equal(7, InvoiceNumber.SequenceOf(number));

        Assert.Equal(
            "INV-20260905-",
            InvoiceNumber.DayPrefix(InvoiceNumber.Prefix, new DateOnly(2026, 9, 5)));
    }

    [Fact]
    public void Sequences_sort_as_text_so_the_highest_string_is_the_highest_number()
    {
        var date = new DateOnly(2026, 9, 5);

        var numbers = new[] { 9, 10, 2, 100 }
            .Select(sequence => InvoiceNumber.Build(InvoiceNumber.Prefix, date, sequence))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(InvoiceNumber.Build(InvoiceNumber.Prefix, date, 100), numbers[^1]);
    }

    [Fact]
    public async Task Checkout_numbers_the_day_sequentially()
    {
        using var test = new TestDatabase();

        // The shop name has no bearing on the number any more.
        test.SetBusinessName("Future Vision");

        var product = test.AddProduct();
        test.SetStock(product.Id, 100);

        var handler = new CheckoutHandler(test.Db);
        var today = DateOnly.FromDateTime(DateTime.Now);

        var first = await handler.HandleAsync(new CheckoutCommand(
            null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 1)]));

        var second = await handler.HandleAsync(new CheckoutCommand(
            null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 1)]));

        Assert.Equal(InvoiceNumber.Build(InvoiceNumber.Prefix, today, 1), first.InvoiceNumber);
        Assert.Equal(InvoiceNumber.Build(InvoiceNumber.Prefix, today, 2), second.InvoiceNumber);
    }

    [Fact]
    public async Task Yesterdays_numbers_do_not_advance_todays_sequence()
    {
        using var test = new TestDatabase();
        test.SetBusinessName("Future Vision");

        var today = DateOnly.FromDateTime(DateTime.Now);

        // A busy day that has already been and gone.
        test.Db.Sales.Add(new Sale
        {
            InvoiceNumber = InvoiceNumber.Build(InvoiceNumber.Prefix, today.AddDays(-1), 57),
            TotalAmount = 0m,
            Status = SaleStatus.Completed
        });
        await test.Db.SaveChangesAsync();

        var product = test.AddProduct();
        test.SetStock(product.Id, 10);

        var sale = await new CheckoutHandler(test.Db).HandleAsync(new CheckoutCommand(
            null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 1)]));

        Assert.Equal(InvoiceNumber.Build(InvoiceNumber.Prefix, today, 1), sale.InvoiceNumber);
    }

    [Fact]
    public async Task Invoice_numbers_cannot_repeat()
    {
        using var test = new TestDatabase();
        var today = DateOnly.FromDateTime(DateTime.Now);

        test.Db.Sales.Add(new Sale
        {
            InvoiceNumber = InvoiceNumber.Build(InvoiceNumber.Prefix, today, 1),
            TotalAmount = 0m,
            Status = SaleStatus.Completed
        });
        await test.Db.SaveChangesAsync();

        test.Db.Sales.Add(new Sale
        {
            InvoiceNumber = InvoiceNumber.Build(InvoiceNumber.Prefix, today, 1),
            TotalAmount = 0m,
            Status = SaleStatus.Completed
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => test.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Last_sale_is_the_most_recent_completed_one()
    {
        using var test = new TestDatabase();
        test.SetBusinessName("Future Vision");

        var product = test.AddProduct();
        test.SetStock(product.Id, 100);

        var handler = new CheckoutHandler(test.Db);
        var lastSale = new GetLastSaleHandler(test.Db);

        Assert.Null(await lastSale.HandleAsync());

        await handler.HandleAsync(new CheckoutCommand(
            null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 1)]));

        var second = await handler.HandleAsync(new CheckoutCommand(
            null, PaymentMethod.Cash, [new CheckoutItemCommand(product.Id, 2)]));

        var latest = await lastSale.HandleAsync();

        Assert.NotNull(latest);
        Assert.Equal(second.InvoiceNumber, latest!.InvoiceNumber);
        Assert.Equal(300m, latest.TotalAmount);
    }

    [Theory]
    [InlineData("INV-20260905-00009", "26090500009")]
    [InlineData("INV-20261231-00123", "26123100123")]
    public void The_barcode_carries_the_number_without_its_prefix_dashes_or_century(
        string invoiceNumber,
        string expected) =>
        Assert.Equal(expected, InvoiceNumber.ScanCode(invoiceNumber));

    [Fact]
    public void The_scan_code_is_short_enough_to_be_worth_printing()
    {
        var full = InvoiceNumber.Build(InvoiceNumber.Prefix, new DateOnly(2026, 9, 5), 9);

        // Eighteen characters of Code 39 do not fit an 80mm receipt at a readable bar width.
        Assert.Equal(18, full.Length);
        Assert.Equal(11, InvoiceNumber.ScanCode(full).Length);
    }

    [Fact]
    public void A_scanned_code_still_finds_its_sale()
    {
        var full = InvoiceNumber.Build(InvoiceNumber.Prefix, new DateOnly(2026, 9, 5), 9);
        var scanned = InvoiceNumber.ScanCode(full);

        // This is the comparison the sales and exchange searches make.
        Assert.Contains(scanned, full.Replace("-", ""), StringComparison.OrdinalIgnoreCase);
    }
}
