using Optical.Application.Abstractions.Identity;
using Optical.Application.Common;
using Optical.Application.Features.Users;
using Optical.Domain.Exceptions;

namespace Optical.Tests;

/// <summary>
/// The rules that stop an administrator locking everyone out. Identity itself is not
/// re-tested here — only the decisions the Application layer makes before calling it.
/// </summary>
public class UserManagementTests
{
    private sealed class FakeUserAdmin : IUserAdminService
    {
        public List<StaffUser> Users { get; } = [];

        public UpdateUserCommand? LastUpdate { get; private set; }

        public bool CreateCalled { get; private set; }

        public Task<IReadOnlyList<StaffUser>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StaffUser>>(Users);

        public Task<StaffUser?> FindAsync(string userId) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Id == userId));

        public Task<int> CountInRoleAsync(string role) =>
            Task.FromResult(Users.Count(u => u.Role == role));

        public Task<IdentityOutcome> CreateAsync(
            string userName, string fullName, string? email, string password, string role)
        {
            CreateCalled = true;
            Users.Add(new StaffUser(userName, userName, fullName, email, role, true));

            return Task.FromResult(IdentityOutcome.Success());
        }

        public Task<IdentityOutcome> UpdateAsync(
            string userId, string fullName, string? email, string role, bool isActive)
        {
            LastUpdate = new UpdateUserCommand(userId, fullName, email, role, isActive);

            return Task.FromResult(IdentityOutcome.Success());
        }

        public Task<IdentityOutcome> SetPasswordAsync(string userId, string newPassword) =>
            Task.FromResult(IdentityOutcome.Success());
    }

    private sealed class FakeCurrentUser(string userId) : ICurrentUserService
    {
        public string? UserId => userId;

        public string? UserName => userId;

        public bool IsAuthenticated => true;
    }

    private static (ManageUsersHandler Handler, FakeUserAdmin Admin) Build(
        string signedInAs,
        params StaffUser[] users)
    {
        var admin = new FakeUserAdmin();
        admin.Users.AddRange(users);

        return (new ManageUsersHandler(admin, new FakeCurrentUser(signedInAs)), admin);
    }

    private static StaffUser User(string id, string role, bool active = true) =>
        new(id, id, id + " name", null, role, active);

    [Fact]
    public async Task An_admin_can_create_a_cashier()
    {
        var (handler, admin) = Build("boss", User("boss", AppRoles.Admin));

        var outcome = await handler.CreateAsync(
            new CreateUserCommand("till1", "Till One", null, "Passw0rd!", AppRoles.Cashier));

        Assert.True(outcome.Succeeded);
        Assert.True(admin.CreateCalled);
        Assert.Contains(admin.Users, u => u.UserName == "till1" && u.Role == AppRoles.Cashier);
    }

    [Fact]
    public async Task A_role_outside_the_fixed_list_is_rejected()
    {
        var (handler, _) = Build("boss", User("boss", AppRoles.Admin));

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            handler.CreateAsync(new CreateUserCommand("x", "X", null, "Passw0rd!", "Superuser")));

        Assert.Contains("valid role", error.Message);
    }

    [Fact]
    public async Task You_cannot_disable_your_own_account()
    {
        var (handler, admin) = Build("boss",
            User("boss", AppRoles.Admin),
            User("other", AppRoles.Admin));

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            handler.UpdateAsync(new UpdateUserCommand("boss", "Boss", null, AppRoles.Admin, false)));

        Assert.Contains("your own account", error.Message);
        Assert.Null(admin.LastUpdate);
    }

    [Fact]
    public async Task You_cannot_remove_your_own_admin_role()
    {
        var (handler, admin) = Build("boss",
            User("boss", AppRoles.Admin),
            User("other", AppRoles.Admin));

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            handler.UpdateAsync(new UpdateUserCommand("boss", "Boss", null, AppRoles.Owner, true)));

        Assert.Contains("your own Admin role", error.Message);
        Assert.Null(admin.LastUpdate);
    }

    [Fact]
    public async Task The_last_admin_cannot_be_demoted()
    {
        var (handler, admin) = Build("boss",
            User("boss", AppRoles.Admin),
            User("shopkeeper", AppRoles.Owner));

        // Signed in as the owner, trying to demote the only Admin.
        var (ownerHandler, ownerAdmin) = Build("shopkeeper",
            User("boss", AppRoles.Admin),
            User("shopkeeper", AppRoles.Owner));

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            ownerHandler.UpdateAsync(new UpdateUserCommand("boss", "Boss", null, AppRoles.Cashier, true)));

        Assert.Contains("only Admin account", error.Message);
        Assert.Null(ownerAdmin.LastUpdate);
    }

    [Fact]
    public async Task The_last_admin_cannot_be_disabled()
    {
        var (handler, admin) = Build("shopkeeper",
            User("boss", AppRoles.Admin),
            User("shopkeeper", AppRoles.Owner));

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            handler.UpdateAsync(new UpdateUserCommand("boss", "Boss", null, AppRoles.Admin, false)));

        Assert.Contains("only Admin account", error.Message);
        Assert.Null(admin.LastUpdate);
    }

    [Fact]
    public async Task An_admin_can_be_demoted_while_another_admin_remains()
    {
        var (handler, admin) = Build("boss",
            User("boss", AppRoles.Admin),
            User("deputy", AppRoles.Admin));

        var outcome = await handler.UpdateAsync(
            new UpdateUserCommand("deputy", "Deputy", null, AppRoles.Cashier, true));

        Assert.True(outcome.Succeeded);
        Assert.NotNull(admin.LastUpdate);
        Assert.Equal(AppRoles.Cashier, admin.LastUpdate!.Role);
    }

    [Fact]
    public async Task A_cashier_can_be_disabled_freely()
    {
        var (handler, admin) = Build("boss",
            User("boss", AppRoles.Admin),
            User("till1", AppRoles.Cashier));

        var outcome = await handler.UpdateAsync(
            new UpdateUserCommand("till1", "Till One", null, AppRoles.Cashier, false));

        Assert.True(outcome.Succeeded);
        Assert.False(admin.LastUpdate!.IsActive);
    }

    [Fact]
    public async Task Resetting_the_password_of_a_missing_account_is_rejected()
    {
        var (handler, _) = Build("boss", User("boss", AppRoles.Admin));

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.ResetPasswordAsync(new ResetPasswordCommand("ghost", "Passw0rd!")));
    }

    [Fact]
    public void The_three_roles_are_the_only_assignable_ones()
    {
        Assert.Equal(3, AppRoles.Assignable.Length);
        Assert.Contains(AppRoles.Admin, AppRoles.Assignable);
        Assert.Contains(AppRoles.Owner, AppRoles.Assignable);
        Assert.Contains(AppRoles.Cashier, AppRoles.Assignable);

        // The combined constants drive every [Authorize] attribute, so they must line up.
        Assert.Equal("Admin,Owner", AppRoles.AdminOrOwner);
        Assert.Equal("Admin,Owner,Cashier", AppRoles.AnyStaff);
    }
}
