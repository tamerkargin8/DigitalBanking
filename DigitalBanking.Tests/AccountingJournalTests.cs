using System.Reflection;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Accounting;
using DigitalBanking.Application.DTOs.ApprovalRequests;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using DigitalBanking.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Tests;

/// <summary>
/// Double-entry invariants on SQLite. SQL Server-specific DDL is compiled but not
/// proven here; see docs/STAGE_03_REPORT.md for that limitation.
/// </summary>
public class AccountingJournalTests
{
    private sealed record Fixture(
        TestDb Db,
        BankDbContext Context,
        PaymentApprovalService Payments,
        AccountingService Accounting,
        ActorContext Owner,
        ActorContext Delegated,
        ActorContext Customer,
        Guid CustomerId,
        Guid AccountTry,
        Guid AccountB);

    private static async Task<Fixture> CreateFixtureAsync(decimal accountABalance = 0m)
    {
        var db = await TestDb.CreateAsync();
        var context = db.NewContext();
        var (owner, ownerActor) = await TestSupport.AddActorAsync(context, "owner-acct", UserRole.Owner);
        var (delegated, delegatedActor) = await TestSupport.AddActorAsync(context, "delegated-acct", UserRole.DelegatedApprover);
        var (customerUser, customerActor) = await TestSupport.AddActorAsync(context, "customer-acct", UserRole.Customer);
        var customer = await TestSupport.AddCustomerAsync(context, "Muhasebe", "Müşterisi", customerUser.Id);
        var accountA = await TestSupport.AddAccountAsync(context, customer.Id, accountABalance, "TRY");
        var accountB = await TestSupport.AddAccountAsync(context, customer.Id, 0m, "TRY");

        foreach (var operation in new[]
                 {
                     ApprovalOperation.Transfer, ApprovalOperation.Deposit,
                     ApprovalOperation.Withdrawal, ApprovalOperation.AccountOpening
                 })
        {
            context.ApprovalDelegations.Add(new ApprovalDelegation
            {
                Id = Guid.NewGuid(),
                UserId = delegated.Id,
                Operation = operation,
                Currency = "TRY",
                MaxAmount = 100_000m,
                IsActive = true,
                CreatedByOwnerId = owner.Id,
                CreatedByOwnerUsername = owner.Username,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await context.SaveChangesAsync();

        var mapper = TestSupport.CreateMapper();
        var audit = new AuditService(context);
        var access = new CustomerAccessService(context);
        var authorization = new ApprovalAuthorizationService(context);
        var payments = new PaymentApprovalService(context, mapper, audit, access, authorization);
        var accounting = new AccountingService(context, authorization, audit);

        return new Fixture(db, context, payments, accounting, ownerActor, delegatedActor, customerActor,
            customer.Id, accountA.Id, accountB.Id);
    }

    private static async Task<int> ApproveDepositAsync(Fixture f, decimal amount, Guid? accountId = null)
    {
        var request = await f.Payments.CreateCustomerOperationRequestAsync(new CreateCustomerOperationRequest
        {
            RequestType = "Deposit",
            AccountId = accountId ?? f.AccountTry,
            Amount = amount,
            Description = "Synthetic deposit"
        }, f.Customer);
        await f.Payments.ApprovePaymentAsync(request.Id, f.Owner);
        return request.Id;
    }

    [Fact]
    public async Task ApprovedDepositPostsBalancedTwoLegJournalAndMovesBalanceOnce()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var approvalId = await ApproveDepositAsync(f, 125.50m);

        var entry = await f.Context.JournalEntries.Include(x => x.Lines).SingleAsync();
        Assert.Equal(JournalPoster.SourceKeyForApproval(approvalId), entry.SourceKey);
        Assert.Equal(JournalSourceOperation.Deposit, entry.SourceOperation);
        Assert.Equal(2, entry.Lines.Count);
        Assert.Equal(entry.Lines.Sum(x => x.Side == JournalSide.Debit ? x.Amount : 0m),
            entry.Lines.Sum(x => x.Side == JournalSide.Credit ? x.Amount : 0m));
        Assert.Contains(entry.Lines, x => x.AccountType == JournalAccountType.CashClearing && x.Side == JournalSide.Debit);
        Assert.Contains(entry.Lines, x => x.AccountType == JournalAccountType.CustomerLiability
                                         && x.Side == JournalSide.Credit && x.AccountId == f.AccountTry);
        Assert.Equal(125.50m, (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountTry)).Balance);

        var trial = await f.Accounting.GetTrialBalanceAsync("TRY", f.Owner);
        Assert.True(trial.IsBalanced);
        var cur = Assert.Single(trial.Currencies);
        Assert.Equal(125.50m, cur.DebitTotal);
        Assert.Equal(125.50m, cur.CreditTotal);
        Assert.Equal(0m, cur.LiabilityDifference);
    }

    [Fact]
    public async Task WithdrawalAndTransferKeepEveryEntryBalancedAtOneCentAndLargeAmounts()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        await ApproveDepositAsync(f, 100_000m);

        var withdraw = await f.Payments.CreateCustomerOperationRequestAsync(new CreateCustomerOperationRequest
        {
            RequestType = "Withdrawal",
            AccountId = f.AccountTry,
            Amount = 0.01m,
            Description = "One kurus"
        }, f.Customer);
        await f.Payments.ApprovePaymentAsync(withdraw.Id, f.Owner);

        var transfer = await f.Payments.CreateCustomerTransferRequestAsync(
            f.AccountTry, f.AccountB, 99_999.99m, "Large transfer", f.Customer);
        await f.Payments.ApprovePaymentAsync(transfer.Id, f.Delegated);

        var entries = await f.Context.JournalEntries.Include(x => x.Lines).OrderBy(x => x.CreatedAt).ToListAsync();
        Assert.Equal(3, entries.Count);
        foreach (var entry in entries)
        {
            Assert.Equal(entry.Lines.Sum(x => x.Side == JournalSide.Debit ? x.Amount : 0m),
                entry.Lines.Sum(x => x.Side == JournalSide.Credit ? x.Amount : 0m));
            Assert.Single(entry.Lines.Select(x => x.Currency).Distinct());
        }

        var transferEntry = entries.Single(x => x.SourceOperation == JournalSourceOperation.Transfer);
        Assert.Equal(2, transferEntry.Lines.Count);
        Assert.All(transferEntry.Lines, line => Assert.Equal(JournalAccountType.CustomerLiability, line.AccountType));
        Assert.Contains(transferEntry.Lines, x => x.AccountId == f.AccountTry && x.Side == JournalSide.Debit);
        Assert.Contains(transferEntry.Lines, x => x.AccountId == f.AccountB && x.Side == JournalSide.Credit);

        var trial = await f.Accounting.GetTrialBalanceAsync("TRY", f.Owner);
        Assert.True(trial.IsBalanced);
        Assert.Equal(0m, trial.Currencies.Single().LiabilityDifference);
        Assert.Equal(0m, (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountTry)).Balance);
    }

    [Fact]
    public async Task ReplayApprovalIsRejectedAndDoesNotDuplicateTheJournal()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var approvalId = await ApproveDepositAsync(f, 500m);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Payments.ApprovePaymentAsync(approvalId, f.Owner));

        await using var verify = f.Db.NewContext();
        Assert.Equal(1, await verify.JournalEntries.CountAsync());
        Assert.Equal(1, await verify.Transactions.CountAsync());
        Assert.Equal(500m, (await verify.Accounts.SingleAsync(x => x.Id == f.AccountTry)).Balance);
        Assert.Equal(PaymentApprovalStatus.Approved,
            (await verify.PaymentApprovals.SingleAsync(x => x.Id == approvalId)).Status);
    }

    [Fact]
    public async Task PendingRequestCreatesNoJournalAndNoBalanceChange()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var pending = await f.Payments.CreateCustomerOperationRequestAsync(new CreateCustomerOperationRequest
        {
            RequestType = "Deposit",
            AccountId = f.AccountTry,
            Amount = 50m,
            Description = "Still pending"
        }, f.Customer);

        Assert.Equal("Pending", pending.Status);
        Assert.Equal(0, await f.Context.JournalEntries.CountAsync());
        Assert.Equal(0m, (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountTry)).Balance);
    }

    [Fact]
    public async Task AccountOpeningWithInitialBalanceUsesOpeningJournalAndZeroOpeningCreatesNone()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var opening = await f.Payments.CreateOperationalRequestAsync(new CreateOperationalRequest
        {
            RequestType = "AccountOpening",
            CustomerId = f.CustomerId,
            Amount = 1_000m,
            Currency = "TRY",
            AccountName = "Synthetic Savings",
            AccountType = "TimeDeposit",
            Description = "Opening with balance"
        }, f.Owner);
        await f.Payments.ApprovePaymentAsync(opening.Id, f.Delegated);

        var entry = await f.Context.JournalEntries.Include(x => x.Lines).SingleAsync();
        Assert.Equal(JournalSourceOperation.AccountOpening, entry.SourceOperation);
        Assert.Contains(entry.Lines, x => x.AccountType == JournalAccountType.OpeningBalanceOffset && x.Side == JournalSide.Debit);
        Assert.Contains(entry.Lines, x => x.AccountType == JournalAccountType.CustomerLiability && x.Side == JournalSide.Credit);
        Assert.Equal(entry.Lines.Sum(x => x.Side == JournalSide.Debit ? x.Amount : 0m),
            entry.Lines.Sum(x => x.Side == JournalSide.Credit ? x.Amount : 0m));

        var zeroOpening = await f.Payments.CreateOperationalRequestAsync(new CreateOperationalRequest
        {
            RequestType = "AccountOpening",
            CustomerId = f.CustomerId,
            Amount = 0m,
            Currency = "TRY",
            AccountName = "Synthetic Zero",
            AccountType = "Checking"
        }, f.Owner);
        await f.Payments.ApprovePaymentAsync(zeroOpening.Id, f.Delegated);

        Assert.Equal(1, await f.Context.JournalEntries.CountAsync());
    }

    [Fact]
    public async Task DuplicateJournalSourceKeyMapsToControlledConflictAndRollsBackBalanceLegacyRowApprovalAndAudit()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var pending = await f.Payments.CreateCustomerOperationRequestAsync(new CreateCustomerOperationRequest
        {
            RequestType = "Deposit",
            AccountId = f.AccountTry,
            Amount = 77m,
            Description = "Rollback candidate"
        }, f.Customer);

        var duplicate = new JournalEntry
        {
            Id = Guid.NewGuid(),
            SourceKey = JournalPoster.SourceKeyForApproval(pending.Id),
            SourceOperation = JournalSourceOperation.Deposit,
            PaymentApprovalId = pending.Id,
            Currency = "TRY",
            Description = "Pre-existing duplicate",
            CreatedAt = DateTime.UtcNow
        };
        duplicate.Lines.Add(new JournalLine
        {
            Id = Guid.NewGuid(), JournalEntryId = duplicate.Id, JournalEntry = duplicate,
            AccountType = JournalAccountType.CashClearing, Side = JournalSide.Debit,
            Amount = 77m, Currency = "TRY"
        });
        duplicate.Lines.Add(new JournalLine
        {
            Id = Guid.NewGuid(), JournalEntryId = duplicate.Id, JournalEntry = duplicate,
            AccountType = JournalAccountType.CustomerLiability, Side = JournalSide.Credit,
            Amount = 77m, Currency = "TRY", AccountId = f.AccountTry
        });
        f.Context.JournalEntries.Add(duplicate);
        await f.Context.SaveChangesAsync();

        var conflict = await Assert.ThrowsAsync<ConcurrencyException>(() =>
            f.Payments.ApprovePaymentAsync(pending.Id, f.Owner));
        Assert.Contains("başka bir incelemeci", conflict.Message, StringComparison.OrdinalIgnoreCase);

        await using var verify = f.Db.NewContext();
        Assert.Equal(0m, (await verify.Accounts.SingleAsync(x => x.Id == f.AccountTry)).Balance);
        Assert.Equal(PaymentApprovalStatus.Pending,
            (await verify.PaymentApprovals.SingleAsync(x => x.Id == pending.Id)).Status);
        Assert.Empty(await verify.Transactions.ToListAsync());
        Assert.Equal(1, await verify.JournalEntries.CountAsync());
        Assert.False(await verify.AuditLogs.AnyAsync(x =>
            x.Action == "APPROVAL_REQUEST_APPROVED" && x.EntityId == pending.Id.ToString()));
    }

    [Fact]
    public async Task BaselinePreviewAndPostAreExplicitIdempotentAndDoNotChangeBalances()
    {
        var f = await CreateFixtureAsync(accountABalance: 2_500m);
        await using var _ = f.Db;

        var preview = await f.Accounting.PreviewBaselineAsync(f.Owner);
        Assert.Equal(1, preview.EligibleCount);
        Assert.Equal(0, preview.AlreadyBaselinedCount);
        Assert.Equal(2_500m, preview.CurrencyTotals.Single().IncreaseTotal);
        Assert.Equal(2_500m, preview.Accounts.Single(x => x.AccountId == f.AccountTry).AdjustmentAmount);
        Assert.Equal(0m, preview.Accounts.Single(x => x.AccountId == f.AccountB).AdjustmentAmount);

        var trialBefore = await f.Accounting.GetTrialBalanceAsync("TRY", f.Owner);
        Assert.Equal(-2_500m, trialBefore.Currencies.Single().LiabilityDifference);

        var posted = await f.Accounting.PostBaselineAsync(f.Owner);
        Assert.Equal(1, posted.CreatedCount);
        Assert.Equal(2_500m, posted.CurrencyTotals.Single().IncreaseTotal);
        Assert.Equal(2_500m, posted.CurrencyTotals.Single().NetAdjustment);
        Assert.Equal(0m, posted.CurrencyTotals.Single().DecreaseTotal);

        await using var verify = f.Db.NewContext();
        Assert.Equal(2_500m, (await verify.Accounts.SingleAsync(x => x.Id == f.AccountTry)).Balance);
        var entry = await verify.JournalEntries.Include(x => x.Lines).SingleAsync();
        Assert.True(entry.IsBaseline);
        Assert.Equal(JournalSourceOperation.Baseline, entry.SourceOperation);
        Assert.Equal(2_500m, entry.Lines.Sum(x => x.Side == JournalSide.Debit ? x.Amount : 0m));
        Assert.Equal(2_500m, entry.Lines.Sum(x => x.Side == JournalSide.Credit ? x.Amount : 0m));

        var secondPreview = await f.Accounting.PreviewBaselineAsync(f.Owner);
        Assert.Equal(0, secondPreview.EligibleCount);
        Assert.Equal(1, secondPreview.AlreadyBaselinedCount);

        var secondPost = await f.Accounting.PostBaselineAsync(f.Owner);
        Assert.Equal(0, secondPost.CreatedCount);
    }


    [Fact]
    public async Task LegacyBalanceThenPostMigrationDepositBaselineCoversOnlyTheLegacyGap()
    {
        var f = await CreateFixtureAsync(accountABalance: 2_500m);
        await using var _ = f.Db;

        await ApproveDepositAsync(f, 500m);

        var preview = await f.Accounting.PreviewBaselineAsync(f.Owner);
        var row = preview.Accounts.Single(x => x.AccountId == f.AccountTry);
        Assert.Equal(3_000m, row.Balance);
        Assert.Equal(500m, row.JournalLiability);
        Assert.Equal(2_500m, row.AdjustmentAmount);
        Assert.True(row.Eligible);
        Assert.Equal(2_500m, preview.CurrencyTotals.Single().IncreaseTotal);

        var posted = await f.Accounting.PostBaselineAsync(f.Owner);
        Assert.Equal(1, posted.CreatedCount);
        Assert.Equal(2_500m, posted.CurrencyTotals.Single().IncreaseTotal);

        await using var verify = f.Db.NewContext();
        var baseline = await verify.JournalEntries.Include(x => x.Lines)
            .SingleAsync(x => x.SourceOperation == JournalSourceOperation.Baseline);
        Assert.Equal(2_500m, baseline.Lines.Sum(x => x.Side == JournalSide.Debit ? x.Amount : 0m));
        Assert.Equal(2_500m, baseline.Lines.Sum(x => x.Side == JournalSide.Credit ? x.Amount : 0m));

        var trial = await f.Accounting.GetTrialBalanceAsync("TRY", f.Owner);
        Assert.Equal(0m, trial.Currencies.Single().LiabilityDifference);
        Assert.Equal(3_000m, (await verify.Accounts.SingleAsync(x => x.Id == f.AccountTry)).Balance);

        // The existing baseline is immutable; a later deposit does not rewrite it.
        await ApproveDepositAsync(f, 100m);
        var afterDepositPreview = await f.Accounting.PreviewBaselineAsync(f.Owner);
        Assert.Equal(0, afterDepositPreview.EligibleCount);
        Assert.Equal(1, afterDepositPreview.AlreadyBaselinedCount);
        var afterDepositPost = await f.Accounting.PostBaselineAsync(f.Owner);
        Assert.Equal(0, afterDepositPost.CreatedCount);

        await using var final = f.Db.NewContext();
        var unchanged = await final.JournalEntries.Include(x => x.Lines)
            .SingleAsync(x => x.SourceOperation == JournalSourceOperation.Baseline);
        Assert.Equal(2_500m, unchanged.Lines.Sum(x => x.Side == JournalSide.Credit ? x.Amount : 0m));
        Assert.Equal(1, await final.JournalEntries.CountAsync(x => x.SourceOperation == JournalSourceOperation.Baseline));
    }

    [Fact]
    public async Task BaselineCanExceedCurrentBalanceAfterPostMigrationDebit()
    {
        var f = await CreateFixtureAsync(accountABalance: 2_500m);
        await using var _ = f.Db;

        var transfer = await f.Payments.CreateCustomerTransferRequestAsync(
            f.AccountTry, f.AccountB, 500m, "post-migration debit", f.Customer);
        await f.Payments.ApprovePaymentAsync(transfer.Id, f.Owner);

        var preview = await f.Accounting.PreviewBaselineAsync(f.Owner);
        var row = preview.Accounts.Single(x => x.AccountId == f.AccountTry);
        Assert.Equal(2_000m, row.Balance);
        Assert.Equal(-500m, row.JournalLiability);
        Assert.Equal(2_500m, row.AdjustmentAmount);
        Assert.True(row.AdjustmentAmount > row.Balance);
        Assert.Equal(2_500m, preview.CurrencyTotals.Single().IncreaseTotal);

        var posted = await f.Accounting.PostBaselineAsync(f.Owner);
        Assert.Equal(1, posted.CreatedCount);
        Assert.Equal(2_500m, posted.CurrencyTotals.Single().IncreaseTotal);

        await using var verify = f.Db.NewContext();
        var baseline = await verify.JournalEntries.Include(x => x.Lines)
            .SingleAsync(x => x.SourceOperation == JournalSourceOperation.Baseline);
        Assert.Equal(2_500m, baseline.Lines
            .Where(x => x.AccountType == JournalAccountType.CustomerLiability)
            .Sum(x => x.Side == JournalSide.Credit ? x.Amount : -x.Amount));
        Assert.Equal(0m, (await f.Accounting.GetTrialBalanceAsync("TRY", f.Owner))
            .Currencies.Single().LiabilityDifference);
    }

    [Fact]
    public async Task NewlyOpenedFundedAccountThenDepositNeedsNoBaseline()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var opening = await f.Payments.CreateOperationalRequestAsync(new CreateOperationalRequest
        {
            RequestType = "AccountOpening",
            CustomerId = f.CustomerId,
            Amount = 1_000m,
            Currency = "TRY",
            AccountName = "Yeni Funded Hesap",
            AccountType = "Checking",
            Description = "new account with opening journal"
        }, f.Owner);
        await f.Payments.ApprovePaymentAsync(opening.Id, f.Delegated);

        var newAccountId = (await f.Context.PaymentApprovals.SingleAsync(x => x.Id == opening.Id))
            .CreatedAccountId!.Value;
        var deposit = await f.Payments.CreateCustomerOperationRequestAsync(new CreateCustomerOperationRequest
        {
            RequestType = "Deposit",
            AccountId = newAccountId,
            Amount = 100m,
            Description = "post-opening deposit"
        }, f.Customer);
        await f.Payments.ApprovePaymentAsync(deposit.Id, f.Owner);

        var preview = await f.Accounting.PreviewBaselineAsync(f.Owner);
        var row = preview.Accounts.Single(x => x.AccountId == newAccountId);
        Assert.Equal(1_100m, row.Balance);
        Assert.Equal(1_100m, row.JournalLiability);
        Assert.Equal(0m, row.AdjustmentAmount);
        Assert.False(row.Eligible);
        Assert.Equal(0, preview.EligibleCount);
        Assert.Equal(0, (await f.Accounting.PostBaselineAsync(f.Owner)).CreatedCount);
        await using var verify = f.Db.NewContext();
        Assert.Equal(0, await verify.JournalEntries.CountAsync(x => x.SourceOperation == JournalSourceOperation.Baseline));
    }

    [Fact]
    public async Task NewAccountDepositIsFullyJournaledAndBaselineCreatesNothing()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        await ApproveDepositAsync(f, 100m);

        var preview = await f.Accounting.PreviewBaselineAsync(f.Owner);
        Assert.Equal(0, preview.EligibleCount);
        Assert.Equal(0, preview.CurrencyTotals.Single().IncreaseTotal);
        Assert.Equal(2, preview.FullyJournaledCount);
        Assert.Equal(100m, preview.Accounts.Single(x => x.AccountId == f.AccountTry).Balance);
        Assert.Equal(0m, preview.Accounts.Single(x => x.AccountId == f.AccountTry).AdjustmentAmount);

        var posted = await f.Accounting.PostBaselineAsync(f.Owner);
        Assert.Equal(0, posted.CreatedCount);
        await using var verify = f.Db.NewContext();
        Assert.Equal(0, await verify.JournalEntries.CountAsync(x => x.SourceOperation == JournalSourceOperation.Baseline));
        Assert.Equal(1, await verify.JournalEntries.CountAsync());
    }

    [Fact]
    public async Task BaselineDecreasesLiabilityWhenJournalExceedsAccountBalance()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        await ApproveDepositAsync(f, 100m);
        var account = await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountTry);
        account.Balance = 0m;
        await f.Context.SaveChangesAsync();

        var preview = await f.Accounting.PreviewBaselineAsync(f.Owner);
        var row = preview.Accounts.Single(x => x.AccountId == f.AccountTry);
        Assert.Equal(-100m, row.AdjustmentAmount);
        Assert.Equal("DecreaseLiability", row.AdjustmentDirection);
        Assert.Equal(100m, preview.CurrencyTotals.Single().DecreaseTotal);

        var posted = await f.Accounting.PostBaselineAsync(f.Owner);
        Assert.Equal(1, posted.CreatedCount);
        Assert.Equal(100m, posted.CurrencyTotals.Single().DecreaseTotal);

        await using var verify = f.Db.NewContext();
        var baseline = await verify.JournalEntries.Include(x => x.Lines)
            .SingleAsync(x => x.SourceOperation == JournalSourceOperation.Baseline);
        Assert.Contains(baseline.Lines, x => x.AccountType == JournalAccountType.CustomerLiability
                                            && x.Side == JournalSide.Debit && x.Amount == 100m);
        Assert.Contains(baseline.Lines, x => x.AccountType == JournalAccountType.OpeningBalanceOffset
                                            && x.Side == JournalSide.Credit && x.Amount == 100m);
        Assert.Equal(0m, (await f.Accounting.GetTrialBalanceAsync("TRY", f.Owner))
            .Currencies.Single().LiabilityDifference);
    }

    [Fact]
    public async Task BaselineTotalsArePerCurrencyAndNeverMixCurrencies()
    {
        var f = await CreateFixtureAsync(accountABalance: 1_000m);
        await using var _ = f.Db;

        await TestSupport.AddAccountAsync(f.Context, f.CustomerId, 200m, "USD");

        var preview = await f.Accounting.PreviewBaselineAsync(f.Owner);
        Assert.Equal(2, preview.EligibleCount);
        Assert.Equal(2, preview.CurrencyTotals.Count);
        Assert.Equal(1_000m, preview.CurrencyTotals.Single(x => x.Currency == "TRY").IncreaseTotal);
        Assert.Equal(200m, preview.CurrencyTotals.Single(x => x.Currency == "USD").IncreaseTotal);

        var posted = await f.Accounting.PostBaselineAsync(f.Owner);
        Assert.Equal(2, posted.CreatedCount);
        Assert.Equal(2, posted.CurrencyTotals.Count);
        Assert.Equal(1_000m, posted.CurrencyTotals.Single(x => x.Currency == "TRY").IncreaseTotal);
        Assert.Equal(200m, posted.CurrencyTotals.Single(x => x.Currency == "USD").IncreaseTotal);

        await using var verify = f.Db.NewContext();
        Assert.Equal(2, await verify.JournalEntries.CountAsync(x => x.SourceOperation == JournalSourceOperation.Baseline));
        var audit = await verify.AuditLogs.OrderByDescending(x => x.CreatedDate)
            .FirstAsync(x => x.Action == "ACCOUNTING_BASELINE_POSTED");
        Assert.Contains("TRY:", audit.Details);
        Assert.Contains("USD:", audit.Details);
    }

    [Fact]
    public async Task AccountingReadsAreRoleScopedAndBaselineIsOwnerOnly()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        await ApproveDepositAsync(f, 10m);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            f.Accounting.GetTrialBalanceAsync("TRY", f.Customer));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            f.Accounting.GetEntriesAsync(1, 25, null, null, null, f.Customer));

        var delegatedView = await f.Accounting.GetTrialBalanceAsync("TRY", f.Delegated);
        Assert.True(delegatedView.IsBalanced);

        await Assert.ThrowsAsync<ForbiddenException>(() => f.Accounting.PreviewBaselineAsync(f.Delegated));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Accounting.PostBaselineAsync(f.Delegated));
    }

    [Fact]
    public async Task EntryListingIsBoundedAndFilterableByAccountAndCurrency()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        await ApproveDepositAsync(f, 20m);

        var byAccount = await f.Accounting.GetEntriesAsync(1, 1, "TRY", f.AccountTry, null, f.Owner);
        Assert.Equal(1, byAccount.TotalCount);
        Assert.Single(byAccount.Items);

        var byUsd = await f.Accounting.GetEntriesAsync(1, 25, "USD", null, null, f.Owner);
        Assert.Equal(0, byUsd.TotalCount);

        var detail = await f.Accounting.GetEntryAsync(byAccount.Items[0].Id, f.Owner);
        Assert.Equal(2, detail.Lines.Count);
        Assert.Equal(detail.Entry.DebitTotal, detail.Entry.CreditTotal);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            f.Accounting.GetEntryAsync(Guid.NewGuid(), f.Owner));
    }

    [Fact]
    public void JournalInvariantsRejectUnbalancedCrossCurrencyAndNonPositiveLegs()
    {
        static JournalEntry Entry(string currency, params (JournalAccountType Type, JournalSide Side, decimal Amount, string Currency)[] legs)
        {
            var entry = new JournalEntry
            {
                Id = Guid.NewGuid(), SourceKey = "test:invariant", Currency = currency,
                Description = "invariant", CreatedAt = DateTime.UtcNow
            };
            foreach (var leg in legs)
            {
                entry.Lines.Add(new JournalLine
                {
                    Id = Guid.NewGuid(), JournalEntryId = entry.Id, JournalEntry = entry,
                    AccountType = leg.Type, Side = leg.Side, Amount = leg.Amount, Currency = leg.Currency,
                    AccountId = leg.Type == JournalAccountType.CustomerLiability ? Guid.NewGuid() : null
                });
            }
            return entry;
        }

        JournalPoster.EnsureBalanced(Entry("TRY",
            (JournalAccountType.CashClearing, JournalSide.Debit, 10m, "TRY"),
            (JournalAccountType.CustomerLiability, JournalSide.Credit, 10m, "TRY")));

        Assert.Throws<BusinessRuleException>(() => JournalPoster.EnsureBalanced(Entry("TRY",
            (JournalAccountType.CashClearing, JournalSide.Debit, 10m, "TRY"),
            (JournalAccountType.CustomerLiability, JournalSide.Credit, 9.99m, "TRY"))));
        Assert.Throws<BusinessRuleException>(() => JournalPoster.EnsureBalanced(Entry("TRY",
            (JournalAccountType.CashClearing, JournalSide.Debit, 10m, "TRY"),
            (JournalAccountType.CustomerLiability, JournalSide.Credit, 10m, "USD"))));
        Assert.Throws<BusinessRuleException>(() => JournalPoster.EnsureBalanced(Entry("TRY",
            (JournalAccountType.CashClearing, JournalSide.Debit, 0m, "TRY"),
            (JournalAccountType.CustomerLiability, JournalSide.Credit, 0m, "TRY"))));
        Assert.Throws<BusinessRuleException>(() => JournalPoster.EnsureBalanced(Entry("TRY",
            (JournalAccountType.CashClearing, JournalSide.Debit, 0.001m, "TRY"),
            (JournalAccountType.CustomerLiability, JournalSide.Credit, 0.001m, "TRY"))));
    }

    [Fact]
    public async Task DatabaseRejectsDuplicateJournalSourceKey()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        await ApproveDepositAsync(f, 30m);
        var existing = await f.Context.JournalEntries.Include(x => x.Lines).SingleAsync();
        var duplicate = new JournalEntry
        {
            Id = Guid.NewGuid(),
            SourceKey = existing.SourceKey,
            SourceOperation = JournalSourceOperation.Deposit,
            Currency = existing.Currency,
            Description = existing.Description,
            CreatedAt = DateTime.UtcNow
        };
        duplicate.Lines.Add(new JournalLine
        {
            Id = Guid.NewGuid(), JournalEntryId = duplicate.Id, JournalEntry = duplicate,
            AccountType = JournalAccountType.CashClearing, Side = JournalSide.Debit,
            Amount = 30m, Currency = "TRY"
        });
        duplicate.Lines.Add(new JournalLine
        {
            Id = Guid.NewGuid(), JournalEntryId = duplicate.Id, JournalEntry = duplicate,
            AccountType = JournalAccountType.CustomerLiability, Side = JournalSide.Credit,
            Amount = 30m, Currency = "TRY", AccountId = f.AccountTry
        });
        f.Context.JournalEntries.Add(duplicate);

        await Assert.ThrowsAsync<DbUpdateException>(() => f.Context.SaveChangesAsync());
    }

    [Fact]
    public void AccountingSurfaceExposesNoUpdateOrDeleteOfPostedEntries()
    {
        var interfaceMethods = typeof(IAccountingService).GetMethods().Select(x => x.Name).ToList();
        Assert.DoesNotContain(interfaceMethods, name => name.Contains("Update", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(interfaceMethods, name => name.Contains("Delete", StringComparison.OrdinalIgnoreCase));

        var controller = typeof(DigitalBanking.API.Controllers.AccountingController);
        var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.DoesNotContain(actions, method => method.GetCustomAttributes<HttpPutAttribute>().Any());
        Assert.DoesNotContain(actions, method => method.GetCustomAttributes<HttpDeleteAttribute>().Any());
        Assert.DoesNotContain(actions, method => method.GetCustomAttributes<HttpPatchAttribute>().Any());
        Assert.Contains(actions, method => method.GetCustomAttributes<HttpPostAttribute>().Any()
                                           && method.Name == "PostBaseline");
    }
}
