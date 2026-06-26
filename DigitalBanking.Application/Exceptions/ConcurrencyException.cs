namespace DigitalBanking.Application.Exceptions;

/// <summary>
/// Thrown when concurrent modification is detected on a resource.
/// Maps to HTTP 409 Conflict.
/// Used with optimistic concurrency control (e.g., RowVersion).
/// </summary>
public class ConcurrencyException : ApplicationException
{
    public ConcurrencyException(string resourceName, Guid resourceId)
        : base($"Resource '{resourceName}' with ID {resourceId} was modified by another request. Please retry.") { }

    public ConcurrencyException(string message) : base(message) { }
}
