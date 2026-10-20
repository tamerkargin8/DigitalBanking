using DigitalBanking.Domain.Enums;

namespace DigitalBanking.Domain.Entities;

/// <summary>
/// Parsed external statement line plus its deterministic match result.
/// </summary>
public class ReconciliationRow
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public ReconciliationBatch Batch { get; set; } = null!;

    public int LineNumber { get; set; }
    public string ExternalReference { get; set; } = string.Empty;
    public string InternalReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public ReconciliationDirection Direction { get; set; }

    public bool IsValid { get; set; } = true;
    public string? ValidationError { get; set; }

    public ReconciliationMatchStatus MatchStatus { get; set; } = ReconciliationMatchStatus.Unmatched;
    public Guid? MatchedTransactionId { get; set; }
    public decimal? SystemAmount { get; set; }
    public string? SystemCurrency { get; set; }
    public DateTime? SystemDate { get; set; }
    public decimal? DifferenceAmount { get; set; }

    public List<ReconciliationException> Exceptions { get; set; } = new();
}
