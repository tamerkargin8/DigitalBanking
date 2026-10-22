using System.Text.Json.Serialization;
using DigitalBanking.Domain.Enums;

namespace DigitalBanking.Application.DTOs.Accounting;

/// <summary>Read-only accounting projections. Money stays decimal; no FX conversion.</summary>
public class JournalLineDto
{
    public Guid Id { get; set; }
    public Guid? AccountId { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public JournalAccountType AccountType { get; set; }
    public string AccountTypeLabel { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public JournalSide Side { get; set; }
    public string SideLabel { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public Guid? TransactionId { get; set; }
    public string? Description { get; set; }
}

public class JournalEntrySummaryDto
{
    public Guid Id { get; set; }
    public string SourceKey { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public JournalSourceOperation SourceOperation { get; set; }
    public string SourceOperationLabel { get; set; } = string.Empty;
    public int? PaymentApprovalId { get; set; }
    public Guid? CorrelationId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDemo { get; set; }
    public bool IsBaseline { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal DebitTotal { get; set; }
    public decimal CreditTotal { get; set; }
    public int LineCount { get; set; }
}

public class JournalEntryDetailDto
{
    public JournalEntrySummaryDto Entry { get; set; } = new();
    public List<JournalLineDto> Lines { get; set; } = new();
}

public class TrialBalanceRowDto
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public JournalAccountType AccountType { get; set; }
    public string AccountTypeLabel { get; set; } = string.Empty;
    public Guid? AccountId { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal DebitTotal { get; set; }
    public decimal CreditTotal { get; set; }
    /// <summary>Debit minus credit; liability rows are normally negative.</summary>
    public decimal NetDebit { get; set; }
}

public class TrialBalanceCurrencyDto
{
    public string Currency { get; set; } = string.Empty;
    public decimal DebitTotal { get; set; }
    public decimal CreditTotal { get; set; }
    public bool IsBalanced => DebitTotal == CreditTotal;
    /// <summary>CustomerLiability credit minus debit from the journal.</summary>
    public decimal JournalLiabilityTotal { get; set; }
    /// <summary>Sum of Account.Balance for the currency, including pre-journal accounts.</summary>
    public decimal AccountBalanceTotal { get; set; }
    /// <summary>Journal liability minus account balances; nonzero means a baseline is pending.</summary>
    public decimal LiabilityDifference => JournalLiabilityTotal - AccountBalanceTotal;
    public List<TrialBalanceRowDto> Rows { get; set; } = new();
}

public class TrialBalanceDto
{
    public string? Currency { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<TrialBalanceCurrencyDto> Currencies { get; set; } = new();
    public bool IsBalanced => Currencies.All(x => x.IsBalanced);
    public decimal DebitTotal => Currencies.Sum(x => x.DebitTotal);
    public decimal CreditTotal => Currencies.Sum(x => x.CreditTotal);
}

public class BaselineAccountDto
{
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    /// <summary>CustomerLiability credit minus debit currently represented by journals.</summary>
    public decimal JournalLiability { get; set; }
    /// <summary>Balance minus JournalLiability; the one-time baseline adjustment.</summary>
    public decimal AdjustmentAmount { get; set; }
    /// <summary>IncreaseLiability, DecreaseLiability or None.</summary>
    public string AdjustmentDirection { get; set; } = "None";
    public bool AlreadyBaselined { get; set; }
    public bool Eligible { get; set; }
}

public class BaselineCurrencyTotalDto
{
    public string Currency { get; set; } = string.Empty;
    public int EligibleCount { get; set; }
    public int AlreadyBaselinedCount { get; set; }
    public int FullyJournaledCount { get; set; }
    public int ExistingBaselineGapCount { get; set; }
    public decimal IncreaseTotal { get; set; }
    public decimal DecreaseTotal { get; set; }
    public decimal NetAdjustment { get; set; }
}

public class BaselinePreviewDto
{
    public DateTime GeneratedAt { get; set; }
    public int EligibleCount { get; set; }
    public int AlreadyBaselinedCount { get; set; }
    public int FullyJournaledCount { get; set; }
    public int ExistingBaselineGapCount { get; set; }
    public List<BaselineCurrencyTotalDto> CurrencyTotals { get; set; } = new();
    public List<BaselineAccountDto> Accounts { get; set; } = new();
}

public class BaselineCurrencyPostTotalDto
{
    public string Currency { get; set; } = string.Empty;
    public int CreatedCount { get; set; }
    public decimal IncreaseTotal { get; set; }
    public decimal DecreaseTotal { get; set; }
    public decimal NetAdjustment { get; set; }
}

public class BaselinePostResultDto
{
    public int CreatedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<BaselineCurrencyPostTotalDto> CurrencyTotals { get; set; } = new();
    public List<Guid> JournalEntryIds { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}
