namespace DigitalBanking.Application.DTOs.PaymentApprovals;

public class PaymentApprovalResponse
{
    public int Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }

    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string Status { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;

    public string? ApprovalReason { get; set; }

    public Guid RequestedByUserId { get; set; }
    public Guid? ReviewedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public string? RejectionReason { get; set; }
    public Guid? RelatedTransactionId { get; set; }
    public bool IsDemo { get; set; }
}
