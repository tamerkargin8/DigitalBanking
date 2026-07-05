using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalBanking.Application.DTOs.Transaction;

public class TransferResultDto
{
    public TransactionDto? OutgoingTransaction { get; set; }
    public TransactionDto? IncomingTransaction { get; set; }
}
