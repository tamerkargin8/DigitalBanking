using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalBanking.Domain.Enums;

public enum TransactionType
{
    Deposit = 1,
    Withdraw = 2,
    TransferOut = 3,
    TransferIn = 4
}
