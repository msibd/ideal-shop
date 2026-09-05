namespace Optical.Application.Abstractions.Identity;

/// <summary>A staff account as the management screens see it.</summary>
public sealed record StaffUser(
    string Id,
    string UserName,
    string FullName,
    string? Email,
    string Role,
    bool IsActive);

/// <summary>The result of an Identity operation, with the reasons when it fails.</summary>
public sealed record IdentityOutcome(bool Succeeded, IReadOnlyList<string> Errors)
{
    public static IdentityOutcome Success() => new(true, []);

    public static IdentityOutcome Failure(params string[] errors) => new(false, errors);
}

/// <summary>
/// Staff account management. Separate from <see cref="IIdentityService"/>, which is about
/// signing in; this is about administering other people's accounts. Implemented by
/// Infrastructure so the Application layer never sees ASP.NET Core Identity types.
/// </summary>
public interface IUserAdminService
{
    Task<IReadOnlyList<StaffUser>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<StaffUser?> FindAsync(string userId);

    /// <summary>How many accounts currently hold the given role.</summary>
    Task<int> CountInRoleAsync(string role);

    Task<IdentityOutcome> CreateAsync(
        string userName,
        string fullName,
        string? email,
        string password,
        string role);

    Task<IdentityOutcome> UpdateAsync(
        string userId,
        string fullName,
        string? email,
        string role,
        bool isActive);

    /// <summary>Sets a new password without knowing the old one. Administrator action.</summary>
    Task<IdentityOutcome> SetPasswordAsync(string userId, string newPassword);
}
