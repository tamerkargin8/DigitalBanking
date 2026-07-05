using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalBanking.Application.DTOs.PaymentApprovals;

public class PaymentApprovalResponse
{
    public int Id { get; set; }

    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string Status { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;

    public string? ApprovalReason { get; set; }

    public int RequestedByUserId { get; set; }
    public int? ReviewedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public string? RejectionReason { get; set; }
    public int? RelatedTransactionId { get; set; }
}
