using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalBanking.Domain.Enums;

public enum PaymentApprovalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    AutoApproved = 4,
    Cancelled = 5
}
