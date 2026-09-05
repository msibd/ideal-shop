using Optical.Application.Abstractions.Identity;
using Optical.Domain.Exceptions;

namespace Optical.Application.Features.Account;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);

public sealed class ChangePasswordHandler(
    ICurrentUserService currentUser,
    IIdentityService identityService)
{
    public async Task<ChangePasswordOutcome> HandleAsync(
        ChangePasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        // A user may only ever change their own password: the id comes from the
        // authentication cookie, never from the request body.
        var userId = currentUser.UserId
            ?? throw new DomainException("You must be signed in to change your password.");

        if (string.Equals(command.CurrentPassword, command.NewPassword, StringComparison.Ordinal))
        {
            return ChangePasswordOutcome.Failure("The new password must be different from the current password.");
        }

        return await identityService.ChangePasswordAsync(userId, command.CurrentPassword, command.NewPassword);
    }
}
