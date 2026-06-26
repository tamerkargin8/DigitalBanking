namespace DigitalBanking.Application.Exceptions;

/// <summary>
/// Thrown when business rule validation fails.
/// Maps to HTTP 400 Bad Request or 422 Unprocessable Entity.
/// </summary>
public class BusinessRuleException : ApplicationException
{
    public BusinessRuleException(string message) : base(message) { }

    public BusinessRuleException(string message, Exception innerException)
        : base(message, innerException) { }
}
