using DigitalBanking.Domain.Enums;

namespace DigitalBanking.Domain.Entities;

/// <summary>A positive debit or credit leg; customer lines carry AccountId.</summary>
public class JournalLine
{
    public Guid Id { get; set; }
    public Guid JournalEntryId { get; set; }
    public JournalEntry JournalEntry { get; set; } = null!;
    public Guid? AccountId { get; set; }
    public JournalAccountType AccountType { get; set; }
    public JournalSide Side { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public Guid? TransactionId { get; set; }
    public Guid? CorrelationId { get; set; }
    public string? Description { get; set; }
}
