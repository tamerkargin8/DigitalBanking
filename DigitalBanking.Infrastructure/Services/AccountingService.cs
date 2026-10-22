using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Accounting;
using DigitalBanking.Application.DTOs.Common;
using System.Data;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Infrastructure.Services;

/// <summary>
/// Read-only ledger projections and the explicit Owner baseline. Journals are
/// append-only: this service never updates or deletes a posted entry. Account
/// balances are not recalculated from journals here; the trial balance exposes the
/// difference so a pending baseline is visible instead of silently patched.
/// </summary>
public class AccountingService : IAccountingService
{
    public const int MaxPageSize = 100;

    private readonly BankDbContext _context;
    private readonly IApprovalAuthorizationService _authorization;
    private readonly IAuditService _audit;

    public AccountingService(
        BankDbContext context,
        IApprovalAuthorizationService authorization,
        IAuditService audit)
    {
        _context = context;
        _authorization = authorization;
        _audit = audit;
    }

    public async Task<TrialBalanceDto> GetTrialBalanceAsync(
        string? currency, ActorContext actor, CancellationToken cancellationToken = default)
    {
        await RequireReaderAsync(actor, cancellationToken);
        var normalized = string.IsNullOrWhiteSpace(currency) ? null : CurrencyCodes.Normalize(currency);

        var query =
            from line in _context.JournalLines.AsNoTracking()
            join entry in _context.JournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
            select new { Line = line, Entry = entry };

        if (normalized is not null)
            query = query.Where(x => x.Entry.Currency == normalized);

        var grouped = await query
            .GroupBy(x => new { x.Line.Currency, x.Line.AccountType, x.Line.AccountId })
            .Select(g => new
            {
                g.Key.Currency,
                g.Key.AccountType,
                g.Key.AccountId,
                Debit = g.Sum(x => x.Line.Side == JournalSide.Debit ? x.Line.Amount : 0m),
                Credit = g.Sum(x => x.Line.Side == JournalSide.Credit ? x.Line.Amount : 0m)
            })
            .ToListAsync(cancellationToken);

        var accounts = await _context.Accounts.AsNoTracking()
            .Select(x => new { x.Id, x.AccountNumber, x.AccountName, x.Currency, x.Balance })
            .ToListAsync(cancellationToken);
        var accountLookup = accounts.ToDictionary(x => x.Id);

        var currencies = new[] { "TRY", "USD", "EUR", "GBP" }
            .Where(c => normalized is null || c == normalized)
            .Select(code =>
            {
                var rows = grouped
                    .Where(x => x.Currency == code)
                    .OrderBy(x => x.AccountType)
                    .ThenBy(x => x.AccountId)
                    .Select(x =>
                    {
                        accountLookup.TryGetValue(x.AccountId ?? Guid.Empty, out var account);
                        return new TrialBalanceRowDto
                        {
                            AccountType = x.AccountType,
                            AccountTypeLabel = AccountingLabels.AccountType(x.AccountType),
                            AccountId = x.AccountId,
                            AccountNumber = account?.AccountNumber,
                            AccountName = account?.AccountName,
                            Currency = x.Currency,
                            DebitTotal = x.Debit,
                            CreditTotal = x.Credit,
                            NetDebit = x.Debit - x.Credit
                        };
                    })
                    .ToList();

                var journalLiability = rows
                    .Where(x => x.AccountType == JournalAccountType.CustomerLiability)
                    .Sum(x => x.CreditTotal - x.DebitTotal);

                return new TrialBalanceCurrencyDto
                {
                    Currency = code,
                    DebitTotal = rows.Sum(x => x.DebitTotal),
                    CreditTotal = rows.Sum(x => x.CreditTotal),
                    JournalLiabilityTotal = journalLiability,
                    AccountBalanceTotal = accounts.Where(x => x.Currency == code).Sum(x => x.Balance),
                    Rows = rows
                };
            })
            .Where(x => x.Rows.Count > 0 || x.AccountBalanceTotal != 0)
            .ToList();

        return new TrialBalanceDto
        {
            Currency = normalized,
            GeneratedAt = DateTime.UtcNow,
            Currencies = currencies
        };
    }

    public async Task<PaginatedResponse<JournalEntrySummaryDto>> GetEntriesAsync(
        int page, int pageSize, string? currency, Guid? accountId, string? sourceOperation,
        ActorContext actor, CancellationToken cancellationToken = default)
    {
        await RequireReaderAsync(actor, cancellationToken);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _context.JournalEntries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var normalized = CurrencyCodes.Normalize(currency);
            query = query.Where(x => x.Currency == normalized);
        }

        if (accountId is not null)
            query = query.Where(x => x.Lines.Any(l => l.AccountId == accountId.Value));

        if (!string.IsNullOrWhiteSpace(sourceOperation))
        {
            if (!Enum.TryParse<JournalSourceOperation>(sourceOperation, ignoreCase: true, out var operation))
                throw new BusinessRuleException("Geçersiz journal işlem türü.");
            query = query.Where(x => x.SourceOperation == operation);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new JournalEntrySummaryDto
            {
                Id = x.Id,
                SourceKey = x.SourceKey,
                SourceOperation = x.SourceOperation,
                PaymentApprovalId = x.PaymentApprovalId,
                CorrelationId = x.CorrelationId,
                Currency = x.Currency,
                Description = x.Description,
                IsDemo = x.IsDemo,
                IsBaseline = x.IsBaseline,
                CreatedAt = x.CreatedAt,
                LineCount = x.Lines.Count,
                DebitTotal = x.Lines.Sum(l => l.Side == JournalSide.Debit ? l.Amount : 0m),
                CreditTotal = x.Lines.Sum(l => l.Side == JournalSide.Credit ? l.Amount : 0m)
            })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
            item.SourceOperationLabel = AccountingLabels.Operation(item.SourceOperation);

        return new PaginatedResponse<JournalEntrySummaryDto>
        {
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = items
        };
    }

    public async Task<JournalEntryDetailDto> GetEntryAsync(
        Guid id, ActorContext actor, CancellationToken cancellationToken = default)
    {
        await RequireReaderAsync(actor, cancellationToken);

        var entry = await _context.JournalEntries.AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("JournalEntry", id.ToString());

        var accountIds = entry.Lines
            .Where(x => x.AccountId is not null)
            .Select(x => x.AccountId!.Value)
            .Distinct()
            .ToList();
        var accounts = await _context.Accounts.AsNoTracking()
            .Where(x => accountIds.Contains(x.Id))
            .Select(x => new { x.Id, x.AccountNumber, x.AccountName })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return new JournalEntryDetailDto
        {
            Entry = new JournalEntrySummaryDto
            {
                Id = entry.Id,
                SourceKey = entry.SourceKey,
                SourceOperation = entry.SourceOperation,
                SourceOperationLabel = AccountingLabels.Operation(entry.SourceOperation),
                PaymentApprovalId = entry.PaymentApprovalId,
                CorrelationId = entry.CorrelationId,
                Currency = entry.Currency,
                Description = entry.Description,
                IsDemo = entry.IsDemo,
                IsBaseline = entry.IsBaseline,
                CreatedAt = entry.CreatedAt,
                LineCount = entry.Lines.Count,
                DebitTotal = entry.Lines.Where(x => x.Side == JournalSide.Debit).Sum(x => x.Amount),
                CreditTotal = entry.Lines.Where(x => x.Side == JournalSide.Credit).Sum(x => x.Amount)
            },
            Lines = entry.Lines
                .OrderBy(x => x.Side)
                .ThenBy(x => x.Amount)
                .Select(x =>
                {
                    accounts.TryGetValue(x.AccountId ?? Guid.Empty, out var account);
                    return new JournalLineDto
                    {
                        Id = x.Id,
                        AccountId = x.AccountId,
                        AccountNumber = account?.AccountNumber,
                        AccountName = account?.AccountName,
                        AccountType = x.AccountType,
                        AccountTypeLabel = AccountingLabels.AccountType(x.AccountType),
                        Side = x.Side,
                        SideLabel = AccountingLabels.Side(x.Side),
                        Amount = x.Amount,
                        Currency = x.Currency,
                        TransactionId = x.TransactionId,
                        Description = x.Description
                    };
                })
                .ToList()
        };
    }

    public async Task<BaselinePreviewDto> PreviewBaselineAsync(
        ActorContext actor, CancellationToken cancellationToken = default)
    {
        await RequireOwnerAsync(actor, cancellationToken);

        // One statement reads each account balance together with its net customer
        // liability. This avoids a preview based on a half-updated balance/journal
        // pair if an approval commits concurrently.
        var snapshots = await _context.Accounts.AsNoTracking()
            .OrderBy(x => x.AccountNumber)
            .Select(x => new
            {
                Account = x,
                JournalLiability = _context.JournalLines
                    .Where(l => l.AccountId == x.Id
                                && l.AccountType == JournalAccountType.CustomerLiability)
                    .Sum(l => (decimal?)(l.Side == JournalSide.Credit ? l.Amount : -l.Amount)) ?? 0m
            })
            .ToListAsync(cancellationToken);

        var baselineKeys = await _context.JournalEntries.AsNoTracking()
            .Where(x => x.SourceOperation == JournalSourceOperation.Baseline)
            .Select(x => x.SourceKey)
            .ToListAsync(cancellationToken);
        var baselineSet = baselineKeys.ToHashSet(StringComparer.Ordinal);

        var rows = snapshots.Select(snapshot =>
        {
            var account = snapshot.Account;
            var gap = account.Balance - snapshot.JournalLiability;
            var already = baselineSet.Contains(JournalPoster.SourceKeyForBaseline(account.Id));
            return new BaselineAccountDto
            {
                AccountId = account.Id,
                AccountNumber = account.AccountNumber,
                AccountName = account.AccountName,
                Currency = account.Currency,
                Balance = account.Balance,
                JournalLiability = snapshot.JournalLiability,
                AdjustmentAmount = gap,
                AdjustmentDirection = gap > 0 ? "IncreaseLiability"
                    : gap < 0 ? "DecreaseLiability" : "None",
                AlreadyBaselined = already,
                Eligible = !already && gap != 0
            };
        }).ToList();

        var currencyTotals = rows
            .GroupBy(x => x.Currency, StringComparer.Ordinal)
            .Select(group =>
            {
                var eligible = group.Where(x => x.Eligible).ToList();
                return new BaselineCurrencyTotalDto
                {
                    Currency = group.Key,
                    EligibleCount = eligible.Count,
                    AlreadyBaselinedCount = group.Count(x => x.AlreadyBaselined),
                    FullyJournaledCount = group.Count(x => !x.AlreadyBaselined && x.AdjustmentAmount == 0),
                    ExistingBaselineGapCount = group.Count(x => x.AlreadyBaselined && x.AdjustmentAmount != 0),
                    IncreaseTotal = eligible.Where(x => x.AdjustmentAmount > 0).Sum(x => x.AdjustmentAmount),
                    DecreaseTotal = eligible.Where(x => x.AdjustmentAmount < 0).Sum(x => -x.AdjustmentAmount),
                    NetAdjustment = eligible.Sum(x => x.AdjustmentAmount)
                };
            })
            .OrderBy(x => x.Currency)
            .ToList();

        return new BaselinePreviewDto
        {
            GeneratedAt = DateTime.UtcNow,
            EligibleCount = currencyTotals.Sum(x => x.EligibleCount),
            AlreadyBaselinedCount = currencyTotals.Sum(x => x.AlreadyBaselinedCount),
            FullyJournaledCount = currencyTotals.Sum(x => x.FullyJournaledCount),
            ExistingBaselineGapCount = currencyTotals.Sum(x => x.ExistingBaselineGapCount),
            CurrencyTotals = currencyTotals,
            Accounts = rows
        };
    }

    public async Task<BaselinePostResultDto> PostBaselineAsync(
        ActorContext actor, CancellationToken cancellationToken = default)
    {
        var dbActor = await RequireOwnerAsync(actor, cancellationToken);

        // Serializable keeps the account-balance + journal-liability snapshot and
        // the baseline writes atomic against an approval transaction. SQLite may
        // surface a busy-snapshot failure instead of a partial write; the whole
        // transaction rolls back and the Owner can re-preview.
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        try
        {
            var snapshots = await _context.Accounts
                .OrderBy(x => x.AccountNumber)
                .Select(x => new
                {
                    Account = x,
                    JournalLiability = _context.JournalLines
                        .Where(l => l.AccountId == x.Id
                                    && l.AccountType == JournalAccountType.CustomerLiability)
                        .Sum(l => (decimal?)(l.Side == JournalSide.Credit ? l.Amount : -l.Amount)) ?? 0m
                })
                .ToListAsync(cancellationToken);

            var baselineKeys = await _context.JournalEntries
                .Where(x => x.SourceOperation == JournalSourceOperation.Baseline)
                .Select(x => x.SourceKey)
                .ToListAsync(cancellationToken);
            var baselineSet = baselineKeys.ToHashSet(StringComparer.Ordinal);

            var now = DateTime.UtcNow;
            var created = new List<(JournalEntry Entry, decimal Adjustment)>();
            var skipped = 0;

            foreach (var snapshot in snapshots)
            {
                var account = snapshot.Account;
                if (baselineSet.Contains(JournalPoster.SourceKeyForBaseline(account.Id)))
                {
                    // An existing baseline is never rewritten, even if a later
                    // manual/legacy change leaves a gap.
                    skipped++;
                    continue;
                }

                var gap = account.Balance - snapshot.JournalLiability;
                if (gap == 0)
                {
                    skipped++;
                    continue;
                }

                created.Add((JournalPoster.AddBaseline(_context, account, gap, now), gap));
            }

            if (created.Count == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new BaselinePostResultDto
                {
                    CreatedCount = 0,
                    SkippedCount = skipped,
                    Message = "Yeni baseline üretilecek hesap yok; mevcut baseline'lar ve bakiyeler değiştirilmedi."
                };
            }

            var currencyTotals = created
                .GroupBy(x => x.Entry.Currency, StringComparer.Ordinal)
                .Select(group => new BaselineCurrencyPostTotalDto
                {
                    Currency = group.Key,
                    CreatedCount = group.Count(),
                    IncreaseTotal = group.Where(x => x.Adjustment > 0).Sum(x => x.Adjustment),
                    DecreaseTotal = group.Where(x => x.Adjustment < 0).Sum(x => -x.Adjustment),
                    NetAdjustment = group.Sum(x => x.Adjustment)
                })
                .OrderBy(x => x.Currency)
                .ToList();

            var auditTotals = string.Join("; ", currencyTotals.Select(x =>
                $"{x.Currency}: created={x.CreatedCount}, increase={x.IncreaseTotal:0.00}, " +
                $"decrease={x.DecreaseTotal:0.00}, net={x.NetAdjustment:0.00}"));
            _audit.Add("ACCOUNTING_BASELINE_POSTED", "JournalEntry", null, dbActor.Username, true,
                $"Created: {created.Count}, Skipped: {skipped}, ByCurrency: [{auditTotals}]. " +
                "No account balance was modified.");
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new BaselinePostResultDto
            {
                CreatedCount = created.Count,
                SkippedCount = skipped,
                CurrencyTotals = currencyTotals,
                JournalEntryIds = created.Select(x => x.Entry.Id).ToList(),
                Message = "Baseline yalnız açıkça onaylanan hesaplar için üretildi; hesap bakiyeleri değiştirilmedi."
            };
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ConcurrencyException(
                "Baseline başka bir işlemle aynı anda üretildi; tekrar önizleyip deneyin.");
        }
        catch
        {
            await SafeRollbackAsync(transaction, cancellationToken);
            throw;
        }
    }

    private async Task<ActorContext> RequireReaderAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        var dbActor = await _authorization.RequireActiveActorAsync(actor.UserId, cancellationToken);
        if (dbActor.Role is not (UserRole.Owner or UserRole.DelegatedApprover))
            throw new ForbiddenException("Muhasebe kayıtları yalnız Owner veya DelegatedApprover tarafından görülebilir.");
        return dbActor;
    }

    private async Task<ActorContext> RequireOwnerAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        var dbActor = await _authorization.RequireActiveActorAsync(actor.UserId, cancellationToken);
        if (dbActor.Role != UserRole.Owner)
            throw new ForbiddenException("Baseline yalnız Owner tarafından üretilebilir.");
        return dbActor;
    }

    private static async Task SafeRollbackAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch
        {
            // Preserve the original failure; rollback is best-effort here.
        }
    }
}
