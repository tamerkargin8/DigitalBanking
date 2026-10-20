namespace DigitalBanking.Application.Exceptions;

/// <summary>
/// The caller is authenticated but not allowed to access the requested resource.
/// Maps to HTTP 403. Ownership failures use NotFound instead to avoid leaking ids.
/// </summary>
public class ForbiddenException : ApplicationException
{
    public ForbiddenException(string message) : base(message) { }
}
