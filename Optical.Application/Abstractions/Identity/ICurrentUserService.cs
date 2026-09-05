namespace Optical.Application.Abstractions.Identity;

/// <summary>
/// The user behind the current request.
/// </summary>
public interface ICurrentUserService
{
    string? UserId { get; }

    string? UserName { get; }

    bool IsAuthenticated { get; }
}
