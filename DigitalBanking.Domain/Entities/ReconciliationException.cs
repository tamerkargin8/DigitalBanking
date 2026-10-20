using DigitalBanking.Domain.Enums;

namespace DigitalBanking.Domain.Entities;

/// <summary>
/// Operational exception workflow. Closing is a resolution record only; it never
/// adjusts an account balance or ledger row.
/// </summary>
public class ReconciliationException
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public ReconciliationBatch Batch { get; set; } = null!;
    public Guid? RowId { get; set; }
    public ReconciliationRow? Row { get; set; }

    public string ExternalReference { get; set; } = string.Empty;
    public string? InternalReference { get; set; }
    public ReconciliationExceptionStatus Status { get; set; } = ReconciliationExceptionStatus.Open;
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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
