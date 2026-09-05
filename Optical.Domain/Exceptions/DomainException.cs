namespace Optical.Domain.Exceptions;

/// <summary>
/// A business rule was violated. The message is safe to show to the user.
/// </summary>
public class DomainException(string message) : Exception(message);
