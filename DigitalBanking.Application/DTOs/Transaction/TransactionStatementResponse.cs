namespace DigitalBanking.Application.DTOs.Transaction;

public class TransactionStatementResponse
{
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string Currency { get; set; } = "TRY";
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int Count { get; set; }
    public List<TransactionDto> Transactions { get; set; } = new();
}
