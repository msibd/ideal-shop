namespace Optical.Application.Abstractions.Identity;

public enum LoginResult
{
    Success,
    InvalidCredentials,
    LockedOut,
    Disabled
}
