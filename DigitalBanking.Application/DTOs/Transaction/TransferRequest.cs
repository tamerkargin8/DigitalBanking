namespace DigitalBanking.Application.DTOs.Transaction;

/// <summary>
/// Request to transfer money between two accounts.
/// </summary>
public class TransferRequest
{
    /// <summary>
    /// Source account ID (money is withdrawn from this account).
    /// </summary>
    public Guid FromAccountId { get; set; }

    /// <summary>
    /// Destination account ID (money is deposited into this account).
    /// </summary>
    public Guid ToAccountId { get; set; }

    /// <summary>
    /// Amount to transfer (must be > 0 and <= source account balance).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Optional description of the transfer.
    /// </summary>
    public string? Description { get; set; }
}
