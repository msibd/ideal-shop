using Microsoft.EntityFrameworkCore;
using Optical.Application.Features.POS;
using Optical.Domain.Entities;

namespace Optical.Tests;

/// <summary>
/// The POS attaches a customer to a sale by looking them up on name or mobile number,
/// so the query has to execute for real, not just compile.
/// </summary>
public class PosCustomerSearchTests
{
    private static async Task<Customer> AddAsync(
        TestDatabase test, string name, string phone, bool isActive = true)
    {
        var customer = new Customer { Name = name, Phone = phone, IsActive = isActive };
        test.Db.Customers.Add(customer);
        await test.Db.SaveChangesAsync();

        return customer;
    }

    [Fact]
    public async Task Customer_is_found_by_name()
    {
        using var test = new TestDatabase();
        await AddAsync(test, "Shafiqul Islam", "01700618515");
        await AddAsync(test, "Rahim Uddin", "01811223344");

        var handler = new SearchPosCustomersHandler(test.Db);

        var found = Assert.Single(await handler.SearchAsync("shafiqul"));
        Assert.Equal("Shafiqul Islam", found.Name);
        Assert.Equal("01700618515", found.Phone);
    }

    [Fact]
    public async Task Customer_is_found_by_mobile_number_including_a_partial_one()
    {
        using var test = new TestDatabase();
        await AddAsync(test, "Shafiqul Islam", "01700618515");
        await AddAsync(test, "Rahim Uddin", "01811223344");

        var handler = new SearchPosCustomersHandler(test.Db);

        Assert.Equal("Shafiqul Islam", Assert.Single(await handler.SearchAsync("01700618515")).Name);
        Assert.Equal("Shafiqul Islam", Assert.Single(await handler.SearchAsync("618515")).Name);
        Assert.Equal("Rahim Uddin", Assert.Single(await handler.SearchAsync("0181")).Name);
    }

    [Fact]
    public async Task Search_ignores_inactive_customers()
    {
        using var test = new TestDatabase();
        await AddAsync(test, "Retired Customer", "01999999999", isActive: false);

        var handler = new SearchPosCustomersHandler(test.Db);

        Assert.Empty(await handler.SearchAsync("Retired"));
        Assert.Empty(await handler.SearchAsync("01999999999"));
    }

    [Fact]
    public async Task Search_returns_no_match_for_an_unknown_term()
    {
        using var test = new TestDatabase();
        await AddAsync(test, "Shafiqul Islam", "01700618515");

        var handler = new SearchPosCustomersHandler(test.Db);

        Assert.Empty(await handler.SearchAsync("nobody"));
    }

    [Fact]
    public async Task Chosen_customer_is_re_read_when_a_rejected_checkout_is_redrawn()
    {
        using var test = new TestDatabase();
        var customer = await AddAsync(test, "Shafiqul Islam", "01700618515");

        var handler = new SearchPosCustomersHandler(test.Db);

        var found = await handler.GetByIdAsync(customer.Id);

        Assert.NotNull(found);
        Assert.Equal("Shafiqul Islam", found!.Name);
        Assert.Equal("01700618515", found.Phone);

        Assert.Null(await handler.GetByIdAsync(null));
        Assert.Null(await handler.GetByIdAsync(9999));
    }
}
