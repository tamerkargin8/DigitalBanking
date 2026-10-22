namespace DigitalBanking.Application.DTOs.PaymentApprovals;

public class PaymentApprovalResponse
{
    public int Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }

    public string RequestType { get; set; } = string.Empty;
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }
    public Guid? RequestedCustomerId { get; set; }
    public decimal? RequestedInitialBalance { get; set; }
    public string? RequestedAccountName { get; set; }
    public string? RequestedAccountType { get; set; }
    public Guid? CreatedAccountId { get; set; }

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
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToUsername { get; set; }
    public DateTime? AssignedAt { get; set; }
    public Guid? RelatedTransactionId { get; set; }
    public bool IsDemo { get; set; }
}
