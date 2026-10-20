namespace DigitalBanking.Application.DTOs.Transaction;

public class TopAccountTransactionResponse
{
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string Currency { get; set; } = "TRY";
    public int TransactionCount { get; set; }
    public decimal DepositTotal { get; set; }
    public decimal WithdrawTotal { get; set; }
    public decimal TransferInTotal { get; set; }
    public decimal TransferOutTotal { get; set; }
    public decimal NetFlow { get; set; }
}
