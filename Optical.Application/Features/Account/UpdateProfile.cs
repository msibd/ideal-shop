using Optical.Application.Abstractions.Identity;
using Optical.Domain.Exceptions;

namespace Optical.Application.Features.Account;

public sealed record UpdateProfileCommand(string FullName);

public sealed class UpdateProfileHandler(
    ICurrentUserService currentUser,
    IIdentityService identityService)
{
    public async Task HandleAsync(UpdateProfileCommand command, CancellationToken cancellationToken = default)
    {
        // A user may only ever change their own name: the id comes from the
        // authentication cookie, never from the request body.
        var userId = currentUser.UserId
            ?? throw new DomainException("You must be signed in to change your name.");

        await identityService.UpdateFullNameAsync(userId, command.FullName.Trim());
    }
}
