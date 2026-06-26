using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalBanking.Application.DTOs.PaymentApprovals;

public class CreatePaymentApprovalRequest
{
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public int RequestedByUserId { get; set; }
}
