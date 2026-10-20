using DigitalBanking.Domain.Enums;

namespace DigitalBanking.Application.DTOs.Reconciliation;

public class ReconciliationBatchSummaryDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int RowCount { get; set; }
    public string ScopeKey { get; set; } = string.Empty;
    public string? CurrencyScope { get; set; }
    public string? DirectionScope { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasErrors { get; set; }
    public string? ValidationSummary { get; set; }
    public bool IsDemo { get; set; }
    public string ImportedByUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ImportedAt { get; set; }

    public int MatchedCount { get; set; }
    public int OnlyExternalCount { get; set; }
    public int OnlySystemCount { get; set; }
    public int AmountMismatchCount { get; set; }
    public int CurrencyMismatchCount { get; set; }
    public int DuplicateReferenceCount { get; set; }
    public int PendingApprovalCount { get; set; }
}

public class ReconciliationRowDto
{
    public Guid Id { get; set; }
    public int LineNumber { get; set; }
    public string ExternalReference { get; set; } = string.Empty;
    public string InternalReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public string Direction { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string? ValidationError { get; set; }
    public string MatchStatus { get; set; } = string.Empty;
    public Guid? MatchedTransactionId { get; set; }
    public decimal? SystemAmount { get; set; }
    public string? SystemCurrency { get; set; }
    public DateTime? SystemDate { get; set; }
    public decimal? DifferenceAmount { get; set; }
}

public class ReconciliationBatchDetailDto
{
    public ReconciliationBatchSummaryDto Batch { get; set; } = new();
    public List<ReconciliationRowDto> Rows { get; set; } = new();
    public List<ReconciliationExceptionDto> Exceptions { get; set; } = new();
}

public class ReconciliationPreviewDto
{
    public ReconciliationBatchSummaryDto Batch { get; set; } = new();
    public bool Replayed { get; set; }
    public int InvalidRowCount { get; set; }
    public List<ReconciliationRowDto> Rows { get; set; } = new();
}

public class ReconciliationExceptionDto
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public string BatchFileName { get; set; } = string.Empty;
    public Guid? RowId { get; set; }
    public string ExternalReference { get; set; } = string.Empty;
    public string? InternalReference { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToUsername { get; set; }
    public string? ResolutionNote { get; set; }
    public Guid? ProposedByUserId { get; set; }
    public string? ProposedByUsername { get; set; }
    public DateTime? ProposedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewedByUsername { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateReconciliationExceptionRequest
{
    public Guid RowId { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? AssignedToUserId { get; set; }
}

public class AssignReconciliationExceptionRequest
{
    public Guid? AssignedToUserId { get; set; }
}

public class ProposeReconciliationResolutionRequest
{
    public string ResolutionNote { get; set; } = string.Empty;
}

public class RejectReconciliationExceptionRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class ReopenReconciliationExceptionRequest
{
    public string? Reason { get; set; }
}
