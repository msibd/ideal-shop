using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Identity;
using Optical.Application.Common;

namespace Optical.Infrastructure.Identity;

/// <summary>
/// Staff account management on top of ASP.NET Core Identity. Every account holds exactly
/// one role, so assigning a role replaces whatever was there before.
/// </summary>
internal sealed class UserAdminService(UserManager<ApplicationUser> userManager) : IUserAdminService
{
    public async Task<IReadOnlyList<StaffUser>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = await userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .ToListAsync(cancellationToken);

        var staff = new List<StaffUser>(users.Count);

        foreach (var user in users)
        {
            staff.Add(await ToStaffUserAsync(user));
        }

        return staff;
    }

    public async Task<StaffUser?> FindAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);

        return user is null ? null : await ToStaffUserAsync(user);
    }

    public async Task<int> CountInRoleAsync(string role) =>
        (await userManager.GetUsersInRoleAsync(role)).Count;

    public async Task<IdentityOutcome> CreateAsync(
        string userName,
        string fullName,
        string? email,
        string password,
        string role)
    {
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = string.IsNullOrWhiteSpace(email) ? null : email,
            EmailConfirmed = true,
            FullName = fullName,
            IsActive = true
        };

        var created = await userManager.CreateAsync(user, password);

        if (!created.Succeeded)
        {
            return Failed(created);
        }

        var assigned = await userManager.AddToRoleAsync(user, role);

        if (!assigned.Succeeded)
        {
            // The account is useless without its role, so it is not left half-made.
            await userManager.DeleteAsync(user);

            return Failed(assigned);
        }

        return IdentityOutcome.Success();
    }

    public async Task<IdentityOutcome> UpdateAsync(
        string userId,
        string fullName,
        string? email,
        string role,
        bool isActive)
    {
        var user = await userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return IdentityOutcome.Failure("That account no longer exists.");
        }

        user.FullName = fullName;
        user.Email = string.IsNullOrWhiteSpace(email) ? null : email;
        user.IsActive = isActive;

        var updated = await userManager.UpdateAsync(user);

        if (!updated.Succeeded)
        {
            return Failed(updated);
        }

        var current = await userManager.GetRolesAsync(user);

        if (!current.Contains(role))
        {
            var removed = await userManager.RemoveFromRolesAsync(user, current);

            if (!removed.Succeeded)
            {
                return Failed(removed);
            }

            var assigned = await userManager.AddToRoleAsync(user, role);

            if (!assigned.Succeeded)
            {
                return Failed(assigned);
            }
        }

        // A disabled account must not keep working from an existing cookie.
        if (!isActive)
        {
            await userManager.UpdateSecurityStampAsync(user);
        }

        return IdentityOutcome.Success();
    }

    public async Task<IdentityOutcome> SetPasswordAsync(string userId, string newPassword)
    {
        var user = await userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return IdentityOutcome.Failure("That account no longer exists.");
        }

        // Removing and re-adding is how Identity sets a password without the old one.
        var removed = await userManager.RemovePasswordAsync(user);

        if (!removed.Succeeded)
        {
            return Failed(removed);
        }

        var added = await userManager.AddPasswordAsync(user, newPassword);

        return added.Succeeded ? IdentityOutcome.Success() : Failed(added);
    }

    private async Task<StaffUser> ToStaffUserAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);

        return new StaffUser(
            user.Id,
            user.UserName ?? string.Empty,
            user.FullName,
            user.Email,
            roles.FirstOrDefault() ?? AppRoles.Cashier,
            user.IsActive);
    }

    private static IdentityOutcome Failed(IdentityResult result) =>
        new(false, result.Errors.Select(e => e.Description).ToList());
}
