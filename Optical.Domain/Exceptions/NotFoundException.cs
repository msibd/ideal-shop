namespace Optical.Domain.Exceptions;

/// <summary>
/// The requested record does not exist.
/// </summary>
public class NotFoundException(string message) : Exception(message);
