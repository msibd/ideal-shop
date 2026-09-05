using Optical.Application.Abstractions.Identity;
using Optical.Domain.Exceptions;

namespace Optical.Application.Features.Account;

public sealed class GetProfileHandler(
    ICurrentUserService currentUser,
    IIdentityService identityService)
{
    /// <summary>
    /// The signed-in user's own profile. The id comes from the authentication cookie,
    /// so a user can never read another account through this path.
    /// </summary>
    public async Task<UserProfile?> HandleAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId
            ?? throw new DomainException("You must be signed in to view your profile.");

        return await identityService.GetProfileAsync(userId);
    }
}
