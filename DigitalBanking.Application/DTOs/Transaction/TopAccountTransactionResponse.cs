using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalBanking.Application.DTOs.Transaction;

public class TopAccountTransactionResponse
{
    public Guid AccountId { get; set; }
    public int TransactionCount { get; set; }
    public decimal DepositTotal { get; set; }
    public decimal WithdrawTotal { get; set; }
}
