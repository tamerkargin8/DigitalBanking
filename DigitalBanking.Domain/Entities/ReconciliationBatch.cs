using DigitalBanking.Domain.Enums;

namespace DigitalBanking.Domain.Entities;

/// <summary>
/// One uploaded synthetic external statement. Results are persisted and immutable
/// after import; exceptions are managed separately.
/// </summary>
public class ReconciliationBatch
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int RowCount { get; set; }

    /// <summary>Normalized scope key used together with FileHash for idempotency.</summary>
    public string ScopeKey { get; set; } = string.Empty;

    public string? CurrencyScope { get; set; }
    public ReconciliationDirection? DirectionScope { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    public ReconciliationBatchStatus Status { get; set; } = ReconciliationBatchStatus.Previewed;
    public bool HasErrors { get; set; }

    /// <summary>Fatal parsing/validation message, or a short summary of row errors.</summary>
    public string? ValidationSummary { get; set; }

    public bool IsDemo { get; set; }

    public Guid ImportedByUserId { get; set; }
    public string ImportedByUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ImportedAt { get; set; }

    public int MatchedCount { get; set; }
    public int OnlyExternalCount { get; set; }
    public int OnlySystemCount { get; set; }
    public int AmountMismatchCount { get; set; }
    public int CurrencyMismatchCount { get; set; }
    public int DuplicateReferenceCount { get; set; }

    /// <summary>Pending approvals in scope are excluded from "only system" and counted here.</summary>
    public int PendingApprovalCount { get; set; }

    public List<ReconciliationRow> Rows { get; set; } = new();
    public List<ReconciliationException> Exceptions { get; set; } = new();
}
