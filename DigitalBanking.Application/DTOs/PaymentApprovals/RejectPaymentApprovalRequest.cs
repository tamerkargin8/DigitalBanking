using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalBanking.Application.DTOs.PaymentApprovals;

public class RejectPaymentApprovalRequest
{
    public int ReviewedByUserId { get; set; }
    public string RejectionReason { get; set; } = string.Empty;
}
