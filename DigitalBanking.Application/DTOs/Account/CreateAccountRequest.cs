namespace DigitalBanking.Application.DTOs.Account;

/// <summary>
/// Request to open a new account for a customer.
/// </summary>
public class CreateAccountRequest
{
    /// <summary>
    /// The ID of the customer who owns the account.
    /// </summary>
    public Guid CustomerId { get; set; }
}

/// <summary>
/// Alias for backward compatibility with existing API.
/// </summary>
public class OpenAccountRequest
{
    public Guid CustomerId { get; set; }
}
