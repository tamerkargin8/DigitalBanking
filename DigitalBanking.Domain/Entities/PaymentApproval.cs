using DigitalBanking.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalBanking.Domain.Entities
{
    public class PaymentApproval
    {
        public int Id { get; set; }
        public Guid FromAccountId { get; set; }
        public Guid ToAccountId { get; set; }

        public decimal Amount { get; set; }
        public string Currency { get; set; } = "TRY";
        public string? Description { get; set; }

        public PaymentApprovalStatus Status { get; set; } = PaymentApprovalStatus.Pending;
        public PaymentRiskLevel RiskLevel { get; set; } = PaymentRiskLevel.Low;

        public string? ApprovalReason { get; set; }
        public int RequestedByUserId { get; set; }
        public int? ReviewedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }

        public string? RejectionReason { get; set; }
        public int? RelatedTransactionId { get; set; }
    }
}
