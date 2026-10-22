using DigitalBanking.Domain.Enums;

namespace DigitalBanking.Domain.Entities;

/// <summary>Append-only balanced journal header. One currency per entry.</summary>
public class JournalEntry
{
    public Guid Id { get; set; }
    public string SourceKey { get; set; } = string.Empty;
    public JournalSourceOperation SourceOperation { get; set; }
    public int? PaymentApprovalId { get; set; }
    public Guid? CorrelationId { get; set; }
    public string Currency { get; set; } = "TRY";
    public string Description { get; set; } = string.Empty;
    public bool IsDemo { get; set; }
    public bool IsBaseline { get; set; }
    public Guid? ReversalOfJournalEntryId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<JournalLine> Lines { get; set; } = new();
}
