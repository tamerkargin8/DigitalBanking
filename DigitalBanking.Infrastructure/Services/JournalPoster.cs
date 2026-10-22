using DigitalBanking.Application.Common;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;

namespace DigitalBanking.Infrastructure.Services;

/// <summary>
/// Builds append-only, balanced double-entry journals. Callers own the database
/// transaction and the single SaveChanges, so the journal, the account balance,
/// the legacy transaction row, the approval and the audit record commit together.
///
/// Conventions (one currency per entry, every leg positive):
///   deposit      Debit  CashClearing        / Credit CustomerLiability (account)
///   withdrawal   Debit  CustomerLiability   / Credit CashClearing
///   transfer     Debit  CustomerLiability   / Credit CustomerLiability (target)
///   opening      Debit  OpeningBalanceOffset/ Credit CustomerLiability (account)
/// </summary>
public static class JournalPoster
{
    public const string SourcePrefixApproval = "approval:";
    public const string SourcePrefixBaseline = "baseline:";
    public const string SourcePrefixReversal = "reversal:";

    public static JournalEntry AddDeposit(
        BankDbContext db,
        PaymentApproval approval,
        Account account,
        Guid transactionId,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(account);
        Money.EnsurePositive(approval.Amount);

        var entry = NewEntry(
            SourceKeyForApproval(approval.Id),
            JournalSourceOperation.Deposit,
            approval,
            approval.Description ?? $"Deposit {account.AccountNumber}",
            now,
            approval.Currency);

        entry.Lines.Add(Line(entry, null, JournalAccountType.CashClearing, JournalSide.Debit,
            approval.Amount, transactionId: null));
        entry.Lines.Add(Line(entry, account.Id, JournalAccountType.CustomerLiability, JournalSide.Credit,
            approval.Amount, transactionId: transactionId));

        return Add(db, entry);
    }

    public static JournalEntry AddWithdrawal(
        BankDbContext db,
        PaymentApproval approval,
        Account account,
        Guid transactionId,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(account);
        Money.EnsurePositive(approval.Amount);

        var entry = NewEntry(
            SourceKeyForApproval(approval.Id),
            JournalSourceOperation.Withdrawal,
            approval,
            approval.Description ?? $"Withdrawal {account.AccountNumber}",
            now,
            approval.Currency);

        entry.Lines.Add(Line(entry, account.Id, JournalAccountType.CustomerLiability, JournalSide.Debit,
            approval.Amount, transactionId: transactionId));
        entry.Lines.Add(Line(entry, null, JournalAccountType.CashClearing, JournalSide.Credit,
            approval.Amount, transactionId: null));

        return Add(db, entry);
    }

    public static JournalEntry AddTransfer(
        BankDbContext db,
        PaymentApproval approval,
        Account fromAccount,
        Account toAccount,
        Guid debitTransactionId,
        Guid creditTransactionId,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(fromAccount);
        ArgumentNullException.ThrowIfNull(toAccount);
        Money.EnsurePositive(approval.Amount);

        if (fromAccount.Id == toAccount.Id)
            throw new BusinessRuleException("Kaynak ve hedef hesap aynı olamaz.");
        if (!string.Equals(fromAccount.Currency, toAccount.Currency, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Transfer bacakları aynı para biriminde olmalıdır.");

        var entry = NewEntry(
            SourceKeyForApproval(approval.Id),
            JournalSourceOperation.Transfer,
            approval,
            approval.Description
                ?? $"Transfer {fromAccount.AccountNumber} -> {toAccount.AccountNumber}",
            now,
            approval.Currency);

        entry.Lines.Add(Line(entry, fromAccount.Id, JournalAccountType.CustomerLiability, JournalSide.Debit,
            approval.Amount, transactionId: debitTransactionId));
        entry.Lines.Add(Line(entry, toAccount.Id, JournalAccountType.CustomerLiability, JournalSide.Credit,
            approval.Amount, transactionId: creditTransactionId));

        return Add(db, entry);
    }

    /// <summary>Opening/baseline only: nonzero balance is funded from the opening offset.</summary>
    public static JournalEntry AddAccountOpening(
        BankDbContext db,
        PaymentApproval approval,
        Account account,
        Guid? openingTransactionId,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(account);
        if (approval.Amount <= 0)
            throw new BusinessRuleException("Hesap açılışında journal için pozitif bakiye gerekir.");
        Money.EnsureScale(approval.Amount);

        var entry = NewEntry(
            SourceKeyForApproval(approval.Id),
            JournalSourceOperation.AccountOpening,
            approval,
            $"Account opening {account.AccountNumber}",
            now,
            approval.Currency);

        entry.Lines.Add(Line(entry, null, JournalAccountType.OpeningBalanceOffset, JournalSide.Debit,
            approval.Amount));
        entry.Lines.Add(Line(entry, account.Id, JournalAccountType.CustomerLiability, JournalSide.Credit,
            approval.Amount, transactionId: openingTransactionId));

        return Add(db, entry);
    }

    /// <summary>
    /// Explicit Owner-called baseline adjustment for an account that predates
    /// double-entry. The adjustment is the signed gap between the account balance
    /// and the customer liability already represented by journals:
    ///   gap &gt; 0: debit OpeningBalanceOffset / credit CustomerLiability
    ///   gap &lt; 0: debit CustomerLiability / credit OpeningBalanceOffset
    /// It never overwrites a previous baseline and never changes the account
    /// balance. One currency per entry, positive 2dp legs.
    /// </summary>
    public static JournalEntry AddBaseline(
        BankDbContext db,
        Account account,
        decimal adjustment,
        DateTime now,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (adjustment == 0)
            throw new BusinessRuleException("Sıfır baseline farkı için journal üretilmez.");
        var amount = Math.Abs(adjustment);
        Money.EnsureScale(amount, "Baseline farkı");

        var entry = NewEntry(
            SourcePrefixBaseline + account.Id,
            JournalSourceOperation.Baseline,
            approval: null,
            string.IsNullOrWhiteSpace(description)
                ? $"Baseline adjustment {adjustment:0.00} for {account.AccountNumber}"
                : description!,
            now,
            account.Currency,
            isDemo: account.IsDemo,
            isBaseline: true,
            correlationId: null);

        if (adjustment > 0)
        {
            entry.Lines.Add(Line(entry, null, JournalAccountType.OpeningBalanceOffset, JournalSide.Debit, amount));
            entry.Lines.Add(Line(entry, account.Id, JournalAccountType.CustomerLiability, JournalSide.Credit, amount));
        }
        else
        {
            entry.Lines.Add(Line(entry, account.Id, JournalAccountType.CustomerLiability, JournalSide.Debit, amount));
            entry.Lines.Add(Line(entry, null, JournalAccountType.OpeningBalanceOffset, JournalSide.Credit, amount));
        }

        return Add(db, entry);
    }

    public static string SourceKeyForApproval(int approvalId) => SourcePrefixApproval + approvalId;

    public static string SourceKeyForBaseline(Guid accountId) => SourcePrefixBaseline + accountId;

    public static string SourceKeyForReversal(Guid journalEntryId) => SourcePrefixReversal + journalEntryId;

    public static JournalEntry Add(BankDbContext db, JournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(entry);
        EnsureBalanced(entry);
        db.JournalEntries.Add(entry);
        return entry;
    }

    /// <summary>Invariant check used before persistence and by tests/reporting.</summary>
    public static void EnsureBalanced(JournalEntry entry)
    {
        if (entry is null) throw new ArgumentNullException(nameof(entry));
        if (string.IsNullOrWhiteSpace(entry.SourceKey) || entry.SourceKey.Length > 100)
            throw new BusinessRuleException("Journal SourceKey 1-100 karakter olmalıdır.");
        if (string.IsNullOrWhiteSpace(entry.Currency) || entry.Currency.Length != 3)
            throw new BusinessRuleException("Journal tek bir 3 harfli para birimi taşımalıdır.");
        if (string.IsNullOrWhiteSpace(entry.Description) || entry.Description.Length > 250)
            throw new BusinessRuleException("Journal açıklaması 1-250 karakter olmalıdır.");
        if (entry.Lines is null || entry.Lines.Count < 2)
            throw new BusinessRuleException("Journal en az iki bacak içermelidir.");

        decimal debits = 0m;
        decimal credits = 0m;

        foreach (var line in entry.Lines)
        {
            if (line.Amount <= 0)
                throw new BusinessRuleException("Journal bacak tutarı pozitif olmalıdır.");
            Money.EnsureScale(line.Amount);
            if (!string.Equals(line.Currency, entry.Currency, StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException("Journal bacakları tek para biriminde olmalıdır.");
            if (line.AccountType == JournalAccountType.CustomerLiability && line.AccountId is null)
                throw new BusinessRuleException("Müşteri yükümlülüğü bacağı bir hesaba bağlı olmalıdır.");

            if (line.Side == JournalSide.Debit) debits += line.Amount;
            else credits += line.Amount;
        }

        if (debits != credits)
            throw new BusinessRuleException(
                $"Journal dengesiz: debit {debits:0.00}, credit {credits:0.00}.");
    }

    private static JournalEntry NewEntry(
        string sourceKey,
        JournalSourceOperation operation,
        PaymentApproval? approval,
        string description,
        DateTime now,
        string currency,
        bool isDemo = false,
        bool isBaseline = false,
        Guid? correlationId = null)
    {
        var normalized = (description ?? string.Empty).Trim();
        if (normalized.Length == 0) normalized = operation.ToString();
        if (normalized.Length > 250) normalized = normalized[..250];

        return new JournalEntry
        {
            Id = Guid.NewGuid(),
            SourceKey = sourceKey,
            SourceOperation = operation,
            PaymentApprovalId = approval?.Id,
            CorrelationId = approval?.CorrelationId ?? correlationId,
            Currency = currency.ToUpperInvariant(),
            Description = normalized,
            IsDemo = approval?.IsDemo ?? isDemo,
            IsBaseline = isBaseline,
            CreatedAt = now
        };
    }

    private static JournalLine Line(
        JournalEntry entry,
        Guid? accountId,
        JournalAccountType accountType,
        JournalSide side,
        decimal amount,
        Guid? transactionId = null)
    {
        return new JournalLine
        {
            Id = Guid.NewGuid(),
            JournalEntryId = entry.Id,
            JournalEntry = entry,
            AccountId = accountId,
            AccountType = accountType,
            Side = side,
            Amount = amount,
            Currency = entry.Currency,
            TransactionId = transactionId,
            CorrelationId = entry.CorrelationId
        };
    }
}
