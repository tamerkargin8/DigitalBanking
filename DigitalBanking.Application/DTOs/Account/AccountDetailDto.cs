namespace DigitalBanking.Application.DTOs.Account;

/// <summary>
/// Account details with recent transaction information.
/// Used for account detail endpoint.
/// </summary>
public class AccountDetailDto
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Customer name (first + last name).
    /// </summary>
    public string? CustomerName { get; set; }

    /// <summary>
    /// Recent transactions (last N transactions).
    /// </summary>
    public List<TransactionSummary>? RecentTransactions { get; set; }

    public class TransactionSummary
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty; // "Deposit" or "Withdraw"
        public decimal Amount { get; set; }
        public decimal BalanceAfter { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
