namespace DigitalBanking.Application.DTOs.Transaction;

/// <summary>
/// Request to withdraw money from an account.
/// </summary>
public class WithdrawRequest
{
    /// <summary>
    /// The account ID to withdraw from.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Amount to withdraw (must be > 0 and <= account balance).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Optional description of the withdrawal.
    /// </summary>
    public string? Description { get; set; }
}
