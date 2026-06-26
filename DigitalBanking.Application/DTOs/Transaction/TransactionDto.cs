namespace DigitalBanking.Application.DTOs.Transaction;

/// <summary>
/// Transaction information DTO.
/// </summary>
public class TransactionDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string Type { get; set; } = string.Empty; // "Deposit", "Withdraw"
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedDate { get; set; }
}
