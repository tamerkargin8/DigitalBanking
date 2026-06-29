using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalBanking.Application.DTOs.Transaction;

public class TransactionStatementResponse
{
    public Guid AccountId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int Count { get; set; }
    public List<TransactionDto> Transactions { get; set; } = new();
}
