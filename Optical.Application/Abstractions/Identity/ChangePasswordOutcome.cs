namespace Optical.Application.Abstractions.Identity;

/// <summary>
/// Result of a password change. Errors are user-safe messages produced by the
/// identity provider (wrong current password, policy violations) and can be shown as-is.
/// </summary>
public sealed record ChangePasswordOutcome(bool Succeeded, IReadOnlyList<string> Errors)
{
    public static ChangePasswordOutcome Success { get; } = new(true, []);

    public static ChangePasswordOutcome Failure(params string[] errors) => new(false, errors);
}
