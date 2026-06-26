namespace DigitalBanking.Application.Exceptions;

/// <summary>
/// Thrown when a requested resource is not found.
/// Maps to HTTP 404 Not Found.
/// </summary>
public class NotFoundException : ApplicationException
{
    public NotFoundException(string resourceName, Guid id)
        : base($"{resourceName} with ID {id} not found.") { }

    public NotFoundException(string resourceName, string identifier)
        : base($"{resourceName} '{identifier}' not found.") { }

    public NotFoundException(string message) : base(message) { }
}
