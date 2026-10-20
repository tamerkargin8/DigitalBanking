namespace DigitalBanking.Application.Exceptions;

/// <summary>
/// Thrown when a caller exceeds the login or request rate limit. Maps to HTTP 429.
/// </summary>
public class TooManyRequestsException : ApplicationException
{
    public TooManyRequestsException(string message) : base(message) { }
}
