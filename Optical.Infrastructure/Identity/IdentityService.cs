using Microsoft.AspNetCore.Identity;
using Optical.Application.Abstractions.Identity;
using Optical.Domain.Exceptions;

namespace Optical.Infrastructure.Identity;

internal sealed class IdentityService(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) : IIdentityService
{
    public async Task<LoginResult> LoginAsync(string userName, string password, bool rememberMe)
    {
        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            return LoginResult.InvalidCredentials;
        }

        if (!user.IsActive)
        {
            return LoginResult.Disabled;
        }

        var result = await signInManager.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return LoginResult.LockedOut;
        }

        return result.Succeeded ? LoginResult.Success : LoginResult.InvalidCredentials;
    }

    public Task LogoutAsync() => signInManager.SignOutAsync();

    public async Task<ChangePasswordOutcome> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return ChangePasswordOutcome.Failure("Your account could not be found.");
        }

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            return new ChangePasswordOutcome(false, result.Errors.Select(e => e.Description).ToList());
        }

        // Changing the password rotates the security stamp, which would otherwise
        // invalidate the current cookie and sign the user out mid-request.
        await signInManager.RefreshSignInAsync(user);

        return ChangePasswordOutcome.Success;
    }

    public async Task<UserProfile?> GetProfileAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);

        return user is null
            ? null
            : new UserProfile(user.UserName ?? string.Empty, user.FullName, user.Email);
    }

    public async Task UpdateFullNameAsync(string userId, string fullName)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("Your account could not be found.");

        user.FullName = fullName;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new DomainException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        // Re-issue the cookie so the header shows the new name on the very next request.
        await signInManager.RefreshSignInAsync(user);
    }
}
