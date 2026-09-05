using Microsoft.EntityFrameworkCore;
using Optical.Application.Common;
using Optical.Application.Features.Permissions;
using Optical.Domain.Entities;
using Optical.Domain.Exceptions;

namespace Optical.Tests;

public class PermissionTests
{
    private static async Task GrantDefaultsAsync(TestDatabase test)
    {
        foreach (var (role, permissions) in AppPermissions.Defaults)
        {
            foreach (var permission in permissions)
            {
                test.Db.RolePermissions.Add(new RolePermission { Role = role, Permission = permission });
            }
        }

        await test.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task Defaults_give_the_till_only_what_it_needs()
    {
        using var test = new TestDatabase();
        await GrantDefaultsAsync(test);

        var store = new RolePermissionStore(test.Db);
        string[] cashier = [AppRoles.Cashier];

        Assert.True(await store.IsGrantedAsync(cashier, AppPermissions.Pos));
        Assert.True(await store.IsGrantedAsync(cashier, AppPermissions.Sales));
        Assert.True(await store.IsGrantedAsync(cashier, AppPermissions.Customers));

        // Exchanges are priced from the original sale line, so the till may take one.
        Assert.True(await store.IsGrantedAsync(cashier, AppPermissions.Exchanges));

        // Nothing that sets a price or edits stock by hand.
        Assert.False(await store.IsGrantedAsync(cashier, AppPermissions.Catalog));
        Assert.False(await store.IsGrantedAsync(cashier, AppPermissions.Inventory));
        Assert.False(await store.IsGrantedAsync(cashier, AppPermissions.Dashboard));
        Assert.False(await store.IsGrantedAsync(cashier, AppPermissions.Reports));
        Assert.False(await store.IsGrantedAsync(cashier, AppPermissions.ManageUsers));
    }

    [Fact]
    public async Task An_owner_runs_the_shop_but_not_the_system()
    {
        using var test = new TestDatabase();
        await GrantDefaultsAsync(test);

        var store = new RolePermissionStore(test.Db);
        string[] owner = [AppRoles.Owner];

        Assert.True(await store.IsGrantedAsync(owner, AppPermissions.Dashboard));
        Assert.True(await store.IsGrantedAsync(owner, AppPermissions.Reports));
        Assert.True(await store.IsGrantedAsync(owner, AppPermissions.Catalog));
        Assert.False(await store.IsGrantedAsync(owner, AppPermissions.ManageUsers));
    }

    [Fact]
    public async Task Granting_a_module_takes_effect_for_that_role()
    {
        using var test = new TestDatabase();
        await GrantDefaultsAsync(test);

        string[] cashier = [AppRoles.Cashier];

        Assert.False(await new RolePermissionStore(test.Db).IsGrantedAsync(cashier, AppPermissions.Reports));

        // The admin ticks Reports for Cashier.
        await new RolePermissionStore(test.Db).SaveAsync(
            AppRoles.Cashier,
            [AppPermissions.Pos, AppPermissions.Sales, AppPermissions.Customers, AppPermissions.Reports]);

        Assert.True(await new RolePermissionStore(test.Db).IsGrantedAsync(cashier, AppPermissions.Reports));
    }

    [Fact]
    public async Task Revoking_a_module_removes_the_grant()
    {
        using var test = new TestDatabase();
        await GrantDefaultsAsync(test);

        string[] cashier = [AppRoles.Cashier];

        await new RolePermissionStore(test.Db).SaveAsync(
            AppRoles.Cashier, [AppPermissions.Pos]);

        var store = new RolePermissionStore(test.Db);
        Assert.True(await store.IsGrantedAsync(cashier, AppPermissions.Pos));
        Assert.False(await store.IsGrantedAsync(cashier, AppPermissions.Sales));
        Assert.False(await store.IsGrantedAsync(cashier, AppPermissions.Customers));
    }

    [Fact]
    public async Task A_role_can_be_stripped_of_everything()
    {
        using var test = new TestDatabase();
        await GrantDefaultsAsync(test);

        await new RolePermissionStore(test.Db).SaveAsync(AppRoles.Cashier, []);

        var store = new RolePermissionStore(test.Db);
        string[] cashier = [AppRoles.Cashier];

        foreach (var definition in AppPermissions.All)
        {
            Assert.False(await store.IsGrantedAsync(cashier, definition.Key));
        }
    }

    [Fact]
    public async Task Admin_always_keeps_staff_management_however_the_form_is_posted()
    {
        using var test = new TestDatabase();
        await GrantDefaultsAsync(test);

        // Every box cleared, including Staff Accounts.
        await new RolePermissionStore(test.Db).SaveAsync(AppRoles.Admin, []);

        var store = new RolePermissionStore(test.Db);
        string[] admin = [AppRoles.Admin];

        Assert.True(await store.IsGrantedAsync(admin, AppPermissions.ManageUsers));

        // Everything else really was cleared — only the locked one survives.
        Assert.False(await store.IsGrantedAsync(admin, AppPermissions.Reports));
    }

    [Fact]
    public async Task An_unknown_module_is_refused()
    {
        using var test = new TestDatabase();

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            new RolePermissionStore(test.Db).SaveAsync(AppRoles.Cashier, ["Everything"]));

        Assert.Contains("not a known module", error.Message);
    }

    [Fact]
    public async Task An_unknown_role_is_refused()
    {
        using var test = new TestDatabase();

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            new RolePermissionStore(test.Db).SaveAsync("Superuser", [AppPermissions.Pos]));

        Assert.Contains("does not exist", error.Message);
    }

    [Fact]
    public async Task Saving_twice_does_not_duplicate_a_grant()
    {
        using var test = new TestDatabase();

        var store = new RolePermissionStore(test.Db);
        await store.SaveAsync(AppRoles.Cashier, [AppPermissions.Pos, AppPermissions.Sales]);
        await store.SaveAsync(AppRoles.Cashier, [AppPermissions.Pos, AppPermissions.Sales]);

        var rows = await test.Db.RolePermissions
            .AsNoTracking()
            .Where(p => p.Role == AppRoles.Cashier)
            .ToListAsync();

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task Holding_any_granted_role_is_enough()
    {
        using var test = new TestDatabase();
        await GrantDefaultsAsync(test);

        var store = new RolePermissionStore(test.Db);

        // A user in both roles gets the union, not the intersection.
        Assert.True(await store.IsGrantedAsync([AppRoles.Cashier, AppRoles.Owner], AppPermissions.Reports));
        Assert.False(await store.IsGrantedAsync([AppRoles.Cashier], AppPermissions.Reports));
    }

    [Fact]
    public void Every_module_in_the_list_is_granted_to_somebody_by_default()
    {
        foreach (var definition in AppPermissions.All)
        {
            var held = AppPermissions.Defaults.Any(entry => entry.Value.Contains(definition.Key));

            Assert.True(held, $"{definition.Key} is not granted to any role by default.");
        }
    }

    [Fact]
    public async Task The_till_cannot_change_its_own_name_or_password()
    {
        using var test = new TestDatabase();
        await GrantDefaultsAsync(test);

        var store = new RolePermissionStore(test.Db);

        Assert.False(await store.IsGrantedAsync([AppRoles.Cashier], AppPermissions.Profile));
        Assert.True(await store.IsGrantedAsync([AppRoles.Owner], AppPermissions.Profile));
        Assert.True(await store.IsGrantedAsync([AppRoles.Admin], AppPermissions.Profile));
    }

    [Fact]
    public async Task Self_service_can_be_handed_to_the_till_later()
    {
        using var test = new TestDatabase();
        await GrantDefaultsAsync(test);

        await new RolePermissionStore(test.Db).SaveAsync(
            AppRoles.Cashier,
            [AppPermissions.Pos, AppPermissions.Sales, AppPermissions.Customers, AppPermissions.Profile]);

        Assert.True(await new RolePermissionStore(test.Db)
            .IsGrantedAsync([AppRoles.Cashier], AppPermissions.Profile));
    }
}
