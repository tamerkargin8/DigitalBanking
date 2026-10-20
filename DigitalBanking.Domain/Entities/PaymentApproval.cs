using DigitalBanking.Domain.Enums;

namespace DigitalBanking.Domain.Entities;

/// <summary>
/// A maker-checker payment request. User identifiers are Guid values that match
/// <see cref="User.Id"/>; amounts stay decimal and the currency is copied from
/// the source account when the request is created.
/// </summary>
public class PaymentApproval
{
    public int Id { get; set; }
    /// <summary>Transfer source, deposit/withdraw account, or Guid.Empty for account opening.</summary>
    public Guid FromAccountId { get; set; }

    /// <summary>Transfer destination, same account for deposit/withdraw, or Guid.Empty for opening.</summary>
    public Guid ToAccountId { get; set; }

    public ApprovalRequestType RequestType { get; set; } = ApprovalRequestType.Transfer;
    public Guid? RequestedCustomerId { get; set; }
    public decimal? RequestedInitialBalance { get; set; }
    public Guid? CreatedAccountId { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public string? Description { get; set; }

    public PaymentApprovalStatus Status { get; set; } = PaymentApprovalStatus.Pending;
    public PaymentRiskLevel RiskLevel { get; set; } = PaymentRiskLevel.Low;

    public string? ApprovalReason { get; set; }
    public Guid RequestedByUserId { get; set; }
    public Guid? ReviewedByUserId { get; set; }

    /// <summary>Stable correlation id used by timeline, receipt and reconciliation.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Marks deterministic synthetic demo data for scoped reset.</summary>
    public bool IsDemo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }

    public string? RejectionReason { get; set; }

    /// <summary>
    /// Outgoing (debit) ledger transaction created when the request is applied
    /// (auto-approved or approved). Null while the request is pending.
    /// </summary>
    public Guid? RelatedTransactionId { get; set; }

    /// <summary>
    /// Optimistic concurrency token. SQL Server uses rowversion; the SQLite
    /// development database receives a generated value from BankDbContext.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
