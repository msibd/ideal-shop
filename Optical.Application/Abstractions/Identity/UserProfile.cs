namespace Optical.Application.Abstractions.Identity;

/// <summary>The signed-in user's own account details.</summary>
public sealed record UserProfile(string UserName, string FullName, string? Email);
