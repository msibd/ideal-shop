namespace Optical.Application.Abstractions.Identity;

/// <summary>
/// Authentication operations. Implemented by Infrastructure using ASP.NET Core Identity
/// so the Web layer never depends on Identity or EF Core types.
/// </summary>
public interface IIdentityService
{
    Task<LoginResult> LoginAsync(string userName, string password, bool rememberMe);

    Task LogoutAsync();

    /// <summary>
    /// Changes the password of the given user after verifying the current one, and keeps
    /// the session signed in (changing a password rotates the security stamp).
    /// </summary>
    Task<ChangePasswordOutcome> ChangePasswordAsync(string userId, string currentPassword, string newPassword);

    /// <summary>The given user's own account details, or null when the account is gone.</summary>
    Task<UserProfile?> GetProfileAsync(string userId);

    /// <summary>
    /// Changes the user's display name and refreshes the signed-in principal so the
    /// new name is visible immediately.
    /// </summary>
    Task UpdateFullNameAsync(string userId, string fullName);
}
