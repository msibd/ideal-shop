using Optical.Application.Abstractions.Identity;
using Optical.Application.Common;
using Optical.Domain.Exceptions;

namespace Optical.Application.Features.Users;

public sealed record CreateUserCommand(
    string UserName,
    string FullName,
    string? Email,
    string Password,
    string Role);

public sealed record UpdateUserCommand(
    string UserId,
    string FullName,
    string? Email,
    string Role,
    bool IsActive);

public sealed record ResetPasswordCommand(string UserId, string NewPassword);

/// <summary>
/// Staff accounts, with the rules that stop an administrator locking everyone out:
/// the last Admin can never be demoted or disabled, and nobody can switch off their
/// own account or take away their own Admin role.
/// </summary>
public sealed class ManageUsersHandler(IUserAdminService users, ICurrentUserService currentUser)
{
    public Task<IReadOnlyList<StaffUser>> GetAllAsync(CancellationToken cancellationToken = default) =>
        users.GetAllAsync(cancellationToken);

    public Task<StaffUser?> FindAsync(string userId) => users.FindAsync(userId);

    public async Task<IdentityOutcome> CreateAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var role = Validated(command.Role);

        if (string.IsNullOrWhiteSpace(command.UserName))
        {
            throw new DomainException("A username is required.");
        }

        if (string.IsNullOrWhiteSpace(command.FullName))
        {
            throw new DomainException("A full name is required.");
        }

        return await users.CreateAsync(
            command.UserName.Trim(),
            command.FullName.Trim(),
            command.Email?.Trim(),
            command.Password,
            role);
    }

    public async Task<IdentityOutcome> UpdateAsync(
        UpdateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var role = Validated(command.Role);

        var target = await users.FindAsync(command.UserId)
            ?? throw new DomainException("That account no longer exists.");

        var isSelf = string.Equals(command.UserId, currentUser.UserId, StringComparison.Ordinal);

        if (isSelf && !command.IsActive)
        {
            throw new DomainException("You cannot disable your own account.");
        }

        if (isSelf && target.Role == AppRoles.Admin && role != AppRoles.Admin)
        {
            throw new DomainException("You cannot remove your own Admin role.");
        }

        // Losing the last Admin would leave nobody able to manage staff at all.
        var losingAdmin = target.Role == AppRoles.Admin && (role != AppRoles.Admin || !command.IsActive);

        if (losingAdmin && await users.CountInRoleAsync(AppRoles.Admin) <= 1)
        {
            throw new DomainException(
                "This is the only Admin account. Give another account the Admin role first.");
        }

        if (string.IsNullOrWhiteSpace(command.FullName))
        {
            throw new DomainException("A full name is required.");
        }

        return await users.UpdateAsync(
            command.UserId,
            command.FullName.Trim(),
            command.Email?.Trim(),
            role,
            command.IsActive);
    }

    public async Task<IdentityOutcome> ResetPasswordAsync(
        ResetPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        _ = await users.FindAsync(command.UserId)
            ?? throw new DomainException("That account no longer exists.");

        return await users.SetPasswordAsync(command.UserId, command.NewPassword);
    }

    private static string Validated(string role) =>
        AppRoles.Assignable.Contains(role, StringComparer.Ordinal)
            ? role
            : throw new DomainException("Select a valid role.");
}
