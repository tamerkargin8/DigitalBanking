using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalBanking.Application.DTOs.Transaction;

public class DailyTransactionSummaryResponse
{
    public Guid AccountId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int Days { get; set; }
    public List<DailyTransactionSummaryItem> Summary { get; set; } = new();
}

public class DailyTransactionSummaryItem
{
    public DateTime Date { get; set; }
    public decimal DepositTotal { get; set; }
    public decimal WithdrawTotal { get; set; }
    public int Count { get; set; }
}
