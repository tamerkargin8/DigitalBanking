using System.Text;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Reconciliation;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Tests;

public class ReconciliationServiceTests
{
    private sealed record Fixture(
        TestDb Db,
        DigitalBanking.Infrastructure.Persistence.BankDbContext Context,
        ReconciliationService Service,
        PaymentApprovalService Payments,
        ActorContext Owner,
        ActorContext Approver,
        Guid OwnerId,
        Guid ApproverId,
        Guid AccountA,
        Guid AccountB,
        Guid UsdA,
        Guid UsdB);

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var db = await TestDb.CreateAsync();
        var context = db.NewContext();
        var (owner, ownerActor) = await TestSupport.AddActorAsync(context, "owner-recon", UserRole.Owner);
        var (approver, approverActor) = await TestSupport.AddActorAsync(context, "approver-recon", UserRole.DelegatedApprover);
        var customerA = await TestSupport.AddCustomerAsync(context, "Ayşe", "Recon");
        var customerB = await TestSupport.AddCustomerAsync(context, "Burak", "Recon");
        var accountA = await TestSupport.AddAccountAsync(context, customerA.Id, 1_000_000m, "TRY");
        var accountB = await TestSupport.AddAccountAsync(context, customerB.Id, 25_000m, "TRY");
        var usdA = await TestSupport.AddAccountAsync(context, customerA.Id, 100_000m, "USD");
        var usdB = await TestSupport.AddAccountAsync(context, customerB.Id, 1_000m, "USD");

        foreach (var (operation, currency, max) in new (ApprovalOperation, string?, decimal?)[]
                 {
                     (ApprovalOperation.Transfer, "TRY", 1_000_000m),
                     (ApprovalOperation.Deposit, "TRY", 1_000_000m),
                     (ApprovalOperation.Withdrawal, "TRY", 1_000_000m),
                     (ApprovalOperation.AccountOpening, "TRY", 1_000_000m),
                     (ApprovalOperation.Transfer, "USD", 1_000_000m),
                     (ApprovalOperation.ReconciliationException, null, null)
                 })
        {
            context.ApprovalDelegations.Add(new ApprovalDelegation
            {
                Id = Guid.NewGuid(),
                UserId = approver.Id,
                Operation = operation,
                Currency = currency,
                MaxAmount = max,
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
        var service = new ReconciliationService(context, audit, authorization);

        return new Fixture(db, context, service, payments, ownerActor, approverActor, owner.Id, approver.Id,
            accountA.Id, accountB.Id, usdA.Id, usdB.Id);
    }

    private static async Task<DigitalBanking.Application.DTOs.PaymentApprovals.PaymentApprovalResponse> CreateApprovedAsync(
        Fixture f, Guid from, Guid to, decimal amount, string description)
    {
        var payment = await f.Payments.CreateApprovalRequestAsync(from, to, amount, description, f.Owner);
        await f.Payments.ApprovePaymentAsync(payment.Id, f.Approver);
        return payment;
    }

    private static string Csv(params (string External, Guid Internal, decimal Amount, string Currency, string Date, string Direction)[] rows)
    {
        var builder = new StringBuilder("external_reference,internal_reference,amount,currency,transaction_date,direction\n");
        foreach (var row in rows)
        {
            builder.Append($"{row.External},{row.Internal},{row.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)},{row.Currency},{row.Date},{row.Direction}\n");
        }
        return builder.ToString();
    }

    private static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");

    [Fact]
    public async Task ValidPreviewAndImportMatchBothSidesAndAreIdempotent()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var payment = await CreateApprovedAsync(f, f.AccountA, f.AccountB, 100m, "Synthetic recon");
        var correlation = payment.CorrelationId;
        var bytes = Encoding.UTF8.GetBytes(Csv(
            ("EXT-1", correlation, 100m, "TRY", Today, "Debit"),
            ("EXT-2", correlation, 100m, "TRY", Today, "Credit")));

        var preview = await f.Service.PreviewAsync(bytes, "statement.csv", null, null, null, null, false, f.Owner);
        Assert.Equal("Previewed", preview.Batch.Status);
        Assert.False(preview.Batch.HasErrors);
        Assert.Equal(2, preview.Batch.RowCount);

        var imported = await f.Service.ImportAsync(preview.Batch.Id, f.Owner);
        Assert.Equal(2, imported.Batch.MatchedCount);
        Assert.Equal(0, imported.Batch.OnlyExternalCount);
        Assert.Equal(0, imported.Batch.OnlySystemCount);
        Assert.All(imported.Rows, r => Assert.Equal("Matched", r.MatchStatus));

        var again = await f.Service.ImportAsync(preview.Batch.Id, f.Owner);
        Assert.Equal(imported.Batch.Id, again.Batch.Id);
        Assert.Equal(1, await f.Context.ReconciliationBatches.CountAsync());
        Assert.Equal(2, await f.Context.ReconciliationRows.CountAsync());

        var replay = await f.Service.PreviewAsync(bytes, "statement-renamed.csv", null, null, null, null, false, f.Owner);
        Assert.True(replay.Replayed);
        Assert.Equal(preview.Batch.Id, replay.Batch.Id);
        Assert.Equal(1, await f.Context.ReconciliationBatches.CountAsync());
    }

    [Fact]
    public async Task MismatchOnlySystemAndDuplicateReferencesAreClassified()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var tryPayment = await CreateApprovedAsync(f, f.AccountA, f.AccountB, 100m, "TRY recon");
        var usdPaymentAmount = await CreateApprovedAsync(f, f.UsdA, f.UsdB, 50m, "USD amount recon");
        var usdPaymentCurrency = await CreateApprovedAsync(f, f.UsdA, f.UsdB, 60m, "USD currency recon");
        var pending = await f.Payments.CreateApprovalRequestAsync(
            f.AccountA, f.AccountB, 75_000m, "Pending recon", f.Owner);

        var bytes = Encoding.UTF8.GetBytes(Csv(
            ("EXT-AMOUNT", usdPaymentAmount.CorrelationId, 999m, "USD", Today, "Debit"),
            ("EXT-CURRENCY", usdPaymentCurrency.CorrelationId, 60m, "TRY", Today, "Debit"),
            ("EXT-ONLY", Guid.NewGuid(), 10m, "TRY", Today, "Debit"),
            ("EXT-DUP", Guid.NewGuid(), 1m, "TRY", Today, "Debit"),
            ("EXT-DUP", Guid.NewGuid(), 2m, "TRY", Today, "Debit"),
            ("EXT-INTERNAL-DUP", tryPayment.CorrelationId, 100m, "TRY", Today, "Debit"),
            ("EXT-INTERNAL-DUP-2", tryPayment.CorrelationId, 100m, "TRY", Today, "Debit")));

        var preview = await f.Service.PreviewAsync(bytes, "mismatch.csv", null, null, null, null, false, f.Owner);
        var imported = await f.Service.ImportAsync(preview.Batch.Id, f.Owner);

        Assert.Equal(1, imported.Batch.AmountMismatchCount);
        Assert.Equal(1, imported.Batch.CurrencyMismatchCount);
        Assert.Equal(1, imported.Batch.OnlyExternalCount);
        Assert.Equal(4, imported.Batch.DuplicateReferenceCount);
        Assert.Equal(1, imported.Batch.PendingApprovalCount);
        Assert.True(imported.Batch.OnlySystemCount > 0);
        Assert.DoesNotContain(imported.Rows, r => r.InternalReference == pending.CorrelationId.ToString("D"));
        Assert.Contains(imported.Rows, r => r.MatchStatus == "AmountMismatch" && r.DifferenceAmount == 949m);
        Assert.Contains(imported.Rows, r => r.MatchStatus == "CurrencyMismatch");
        Assert.Contains(imported.Rows, r => r.MatchStatus == "OnlySystem");
    }

    [Fact]
    public async Task InvalidFileIsPersistedAsInvalidAndCannotBeImported()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var badHeader = Encoding.UTF8.GetBytes("wrong,internal_reference,amount,currency,transaction_date,direction\n");
        var preview = await f.Service.PreviewAsync(badHeader, "bad.csv", null, null, null, null, false, f.Owner);
        Assert.Equal("Invalid", preview.Batch.Status);
        Assert.True(preview.Batch.HasErrors);
        Assert.False(string.IsNullOrWhiteSpace(preview.Batch.ValidationSummary));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Service.ImportAsync(preview.Batch.Id, f.Owner));
        Assert.Equal(1, await f.Context.ReconciliationBatches.CountAsync());

        var replay = await f.Service.PreviewAsync(badHeader, "bad.csv", null, null, null, null, false, f.Owner);
        Assert.True(replay.Replayed);
        Assert.Equal(preview.Batch.Id, replay.Batch.Id);
    }

    [Fact]
    public async Task RowValidationErrorsAreShownBeforeImport()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var bytes = Encoding.UTF8.GetBytes("external_reference,internal_reference,amount,currency,transaction_date,direction\n" +
            "EXT-1,not-a-guid,10,TRY,2026-10-20,Debit\n");
        var preview = await f.Service.PreviewAsync(bytes, "row-error.csv", null, null, null, null, false, f.Owner);

        Assert.Equal("Invalid", preview.Batch.Status);
        Assert.Equal(1, preview.InvalidRowCount);
        var row = Assert.Single(preview.Rows);
        Assert.False(row.IsValid);
        Assert.Contains("GUID", row.ValidationError);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.ImportAsync(preview.Batch.Id, f.Owner));
    }

    [Fact]
    public async Task ExportNeutralizesFormulaInjection()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var bytes = Encoding.UTF8.GetBytes(Csv(
            ("\"=SUM(1,1)\"", Guid.NewGuid(), 1m, "TRY", Today, "Debit")));
        var preview = await f.Service.PreviewAsync(bytes, "formula.csv", null, null, null, null, false, f.Owner);
        await f.Service.ImportAsync(preview.Batch.Id, f.Owner);

        var csv = await f.Service.ExportCsvAsync(preview.Batch.Id);
        Assert.StartsWith("\uFEFF", csv);
        Assert.Contains("\"'=SUM(1,1)\"", csv);
        Assert.DoesNotContain(",=SUM", csv);
    }

    [Fact]
    public async Task ExceptionWorkflowRequiresDifferentAdminAndDoesNotChangeBalances()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var bytes = Encoding.UTF8.GetBytes(Csv(("EXT-ONLY", Guid.NewGuid(), 10m, "TRY", Today, "Debit")));
        var preview = await f.Service.PreviewAsync(bytes, "exception.csv", null, null, null, null, false, f.Owner);
        var imported = await f.Service.ImportAsync(preview.Batch.Id, f.Owner);
        var row = Assert.Single(imported.Rows);
        Assert.Equal("OnlyExternal", row.MatchStatus);

        var beforeA = (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountA)).Balance;
        var beforeB = (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountB)).Balance;

        var exception = await f.Service.CreateExceptionAsync(new CreateReconciliationExceptionRequest
        {
            RowId = row.Id,
            Description = "Synthetic external-only line"
        }, f.Owner);
        Assert.Equal("Open", exception.Status);

        await f.Service.ReviewAsync(exception.Id, f.Owner);
        await f.Service.ProposeAsync(exception.Id, "Operational note only; no ledger correction.", f.Owner);

        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.ApproveAsync(exception.Id, f.Owner));
        var closed = await f.Service.ApproveAsync(exception.Id, f.Approver);
        Assert.Equal("Closed", closed.Status);
        Assert.Equal(f.Approver.UserId, closed.ReviewedByUserId);

        Assert.Equal(beforeA, (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountA)).Balance);
        Assert.Equal(beforeB, (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountB)).Balance);
    }

    [Fact]
    public async Task RejectedExceptionCanBeReopened()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var bytes = Encoding.UTF8.GetBytes(Csv(("EXT-ONLY-2", Guid.NewGuid(), 5m, "TRY", Today, "Debit")));
        var preview = await f.Service.PreviewAsync(bytes, "reopen.csv", null, null, null, null, false, f.Owner);
        var imported = await f.Service.ImportAsync(preview.Batch.Id, f.Owner);
        var row = Assert.Single(imported.Rows);

        var exception = await f.Service.CreateExceptionAsync(new CreateReconciliationExceptionRequest
        {
            RowId = row.Id,
            Description = "Synthetic reopen case"
        }, f.Owner);
        await f.Service.ReviewAsync(exception.Id, f.Owner);
        await f.Service.ProposeAsync(exception.Id, "Proposed operational resolution.", f.Owner);
        var rejected = await f.Service.RejectAsync(exception.Id, "Insufficient evidence.", f.Approver);
        Assert.Equal("Rejected", rejected.Status);

        var reopened = await f.Service.ReopenAsync(exception.Id, "New evidence received.", f.Owner);
        Assert.Equal("Open", reopened.Status);
        Assert.Null(reopened.ResolutionNote);
    }

    [Fact]
    public async Task NonAdminCannotUseReconciliationOrExceptions()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;
        var customerUser = await TestSupport.AddActorAsync(f.Context, "plain-customer", UserRole.Customer);
        var user = customerUser.Actor;

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            f.Service.PreviewAsync(Encoding.UTF8.GetBytes(Csv()), "x.csv", null, null, null, null, false, user));
    }

    [Fact]
    public async Task ConcurrentExceptionApprovalsOnlyApplyOnce()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var bytes = Encoding.UTF8.GetBytes(Csv(("EXT-CONC", Guid.NewGuid(), 7m, "TRY", Today, "Debit")));
        var preview = await f.Service.PreviewAsync(bytes, "concurrency.csv", null, null, null, null, false, f.Owner);
        var imported = await f.Service.ImportAsync(preview.Batch.Id, f.Owner);
        var row = Assert.Single(imported.Rows);
        var exception = await f.Service.CreateExceptionAsync(new CreateReconciliationExceptionRequest
        {
            RowId = row.Id,
            Description = "Synthetic concurrency case"
        }, f.Owner);
        await f.Service.ReviewAsync(exception.Id, f.Owner);
        await f.Service.ProposeAsync(exception.Id, "Operational resolution.", f.Owner);

        await using var first = f.Db.NewContext();
        await using var second = f.Db.NewContext();
        var firstEntity = await first.ReconciliationExceptions.SingleAsync(x => x.Id == exception.Id);
        var secondEntity = await second.ReconciliationExceptions.SingleAsync(x => x.Id == exception.Id);

        firstEntity.Status = ReconciliationExceptionStatus.Closed;
        await first.SaveChangesAsync();
        secondEntity.Status = ReconciliationExceptionStatus.Closed;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }
}
