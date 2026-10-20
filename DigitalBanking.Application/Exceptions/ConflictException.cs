namespace DigitalBanking.Application.Exceptions;

/// <summary>Maps to HTTP 409 (duplicate username/email, already-configured owner).</summary>
public class ConflictException : ApplicationException
{
    public ConflictException(string message) : base(message) { }
}
