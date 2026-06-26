namespace DigitalBanking.Application.DTOs.Transaction;

/// <summary>
/// Request to deposit money into an account.
/// </summary>
public class DepositRequest
{
    /// <summary>
    /// The account ID to deposit into.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Amount to deposit (must be > 0).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Optional description of the deposit.
    /// </summary>
    public string? Description { get; set; }
}
