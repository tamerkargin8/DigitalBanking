using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Operations;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using DigitalBanking.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigitalBanking.Tests;

/// <summary>
/// Deterministic SLA/assignment tests against SQLite. SQL Server behavior is
/// compiled but not proven here; see docs/STAGE_04_REPORT.md.
/// </summary>
public class OperationsCenterTests
{
    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed record Fixture(
        TestDb Db,
        BankDbContext Context,
        OperationsService Operations,
        PaymentApprovalService Payments,
        FixedClock Clock,
        ActorContext Owner,
        ActorContext Delegated,
        ActorContext LowDelegated,
        ActorContext Customer,
        Guid CustomerUserId,
        Guid OwnerId,
        Guid DelegatedId,
        Guid LowDelegatedId,
        Guid InactiveApproverId);

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var db = await TestDb.CreateAsync();
        var context = db.NewContext();
        var (owner, ownerActor) = await TestSupport.AddActorAsync(context, "owner-ops", UserRole.Owner);
        var (delegated, delegatedActor) = await TestSupport.AddActorAsync(context, "delegated-ops", UserRole.DelegatedApprover);
        var (low, lowActor) = await TestSupport.AddActorAsync(context, "delegated-low", UserRole.DelegatedApprover);
        var (customerUser, customerActor) = await TestSupport.AddActorAsync(context, "customer-ops", UserRole.Customer);
        var (inactive, _) = await TestSupport.AddActorAsync(
            context, "delegated-inactive", UserRole.DelegatedApprover, UserStatus.Disabled);

        context.ApprovalDelegations.AddRange(
            Delegation(delegated.Id, ApprovalOperation.Transfer, "TRY", 1_000m, owner.Id),
            Delegation(delegated.Id, ApprovalOperation.ReconciliationException, null, null, owner.Id),
            Delegation(low.Id, ApprovalOperation.Transfer, "TRY", 50m, owner.Id));
        await context.SaveChangesAsync();

        var clock = new FixedClock();
        var authorization = new ApprovalAuthorizationService(context);
        var audit = new AuditService(context);
        var operations = new OperationsService(
            context, authorization, audit, clock, Options.Create(new OperationsOptions { PendingSlaMinutes = 30 }));
        var payments = new PaymentApprovalService(
            context, TestSupport.CreateMapper(), audit, new CustomerAccessService(context), authorization);

        return new Fixture(db, context, operations, payments, clock,
            ownerActor, delegatedActor, lowActor, customerActor,
            customerUser.Id, owner.Id, delegated.Id, low.Id, inactive.Id);
    }

    private static ApprovalDelegation Delegation(
        Guid userId, ApprovalOperation operation, string? currency, decimal? max, Guid ownerId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Operation = operation,
        Currency = currency,
        MaxAmount = max,
        IsActive = true,
        CreatedByOwnerId = ownerId,
        CreatedByOwnerUsername = "owner-ops",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static PaymentApproval Approval(
        Fixture f,
        decimal amount = 100m,
        string currency = "TRY",
        ApprovalRequestType type = ApprovalRequestType.Transfer,
        PaymentApprovalStatus status = PaymentApprovalStatus.Pending,
        int ageMinutes = 0,
        Guid? requester = null,
        string? description = null)
    {
        return new PaymentApproval
        {
            FromAccountId = Guid.NewGuid(),
            ToAccountId = Guid.NewGuid(),
            RequestType = type,
            Amount = amount,
            Currency = currency,
            Description = description ?? "Synthetic operations request",
            Status = status,
            RiskLevel = PaymentRiskLevel.Low,
            RequestedByUserId = requester ?? f.CustomerUserId,
            CorrelationId = Guid.NewGuid(),
            CreatedAt = f.Clock.UtcNow.AddMinutes(-ageMinutes)
        };
    }

    private static async Task<PaymentApproval> AddApprovalAsync(Fixture f, params PaymentApproval[] approvals)
    {
        f.Context.PaymentApprovals.AddRange(approvals);
        await f.Context.SaveChangesAsync();
        return approvals[0];
    }

    [Fact]
    public async Task SlaBoundaryAtThirtyMinutesIsOverdueAndClosedRowsAreExcluded()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        await AddApprovalAsync(f,
            Approval(f, ageMinutes: 29, description: "within"),
            Approval(f, ageMinutes: 30, description: "at boundary"),
            Approval(f, ageMinutes: 31, description: "after boundary"),
            Approval(f, ageMinutes: 100, status: PaymentApprovalStatus.Approved, description: "closed"));

        var summary = await f.Operations.GetSummaryAsync(f.Owner);
        Assert.Equal(3, summary.PendingCount);
        Assert.Equal(2, summary.OverdueCount);
        Assert.Equal(31, summary.OldestPendingAgeMinutes);
        Assert.Equal(30, summary.PendingSlaMinutes);

        var overdue = await f.Operations.GetApprovalsAsync(
            1, 10, "Pending", null, null, "overdue", null, f.Owner);
        Assert.Equal(2, overdue.TotalCount);
        Assert.All(overdue.Items, row => Assert.True(row.IsOverdue));

        var within = await f.Operations.GetApprovalsAsync(
            1, 10, "Pending", null, null, "within", null, f.Owner);
        Assert.Single(within.Items);
        Assert.Equal(29, within.Items[0].AgeMinutes);

        var closed = await f.Operations.GetApprovalsAsync(
            1, 10, "Approved", null, null, null, null, f.Owner);
        Assert.Single(closed.Items);
        Assert.False(closed.Items[0].IsOverdue);
    }

    [Fact]
    public async Task DelegatedSeesOnlyPermittedRequestsAndNeverOwnRequest()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        await AddApprovalAsync(f,
            Approval(f, amount: 100m, description: "permitted"),
            Approval(f, amount: 2_000m, description: "over delegated limit"),
            Approval(f, amount: 100m, requester: f.DelegatedId, description: "own request"));

        var delegated = await f.Operations.GetSummaryAsync(f.Delegated);
        Assert.Equal(1, delegated.PendingCount);

        var owner = await f.Operations.GetSummaryAsync(f.Owner);
        Assert.Equal(3, owner.PendingCount);

        var rows = await f.Operations.GetApprovalsAsync(
            1, 25, "Pending", null, null, null, null, f.Delegated);
        var row = Assert.Single(rows.Items);
        Assert.Equal(100m, row.Amount);
        Assert.Contains(row.Assignees, x => x.UserId == f.DelegatedId && x.IsCurrentUser);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            f.Operations.GetApprovalsAsync(1, 25, "Approved", null, null, null, null, f.Delegated));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            f.Operations.GetSummaryAsync(f.Customer));
    }

    [Fact]
    public async Task AssignmentRejectsUnauthorizedInactiveIneligibleAndRequesterTargets()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var allowed = await AddApprovalAsync(f, Approval(f, amount: 40m));
        var high = await AddApprovalAsync(f, Approval(f, amount: 500m));
        var own = await AddApprovalAsync(f, Approval(f, amount: 10m, requester: f.DelegatedId));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            f.Operations.AssignApprovalAsync(allowed.Id, f.DelegatedId, f.Customer));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            f.Operations.AssignApprovalAsync(allowed.Id, f.OwnerId, f.Delegated));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Operations.AssignApprovalAsync(allowed.Id, f.InactiveApproverId, f.Owner));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Operations.AssignApprovalAsync(allowed.Id, f.CustomerUserId, f.Owner));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Operations.AssignApprovalAsync(allowed.Id, f.CustomerUserId, f.Owner));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Operations.AssignApprovalAsync(high.Id, f.LowDelegatedId, f.Owner));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            f.Operations.AssignApprovalAsync(own.Id, f.DelegatedId, f.Delegated));

        var first = await f.Operations.AssignApprovalAsync(allowed.Id, f.LowDelegatedId, f.Owner);
        Assert.True(first.Changed);
        Assert.Equal(f.LowDelegatedId, first.AssignedToUserId);

        var replay = await f.Operations.AssignApprovalAsync(allowed.Id, f.LowDelegatedId, f.Owner);
        Assert.False(replay.Changed);
        Assert.Contains("değişiklik", replay.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(await f.Context.AuditLogs.ToListAsync(),
            x => x.Action == "APPROVAL_ASSIGNED" && x.EntityId == allowed.Id.ToString());

        var cleared = await f.Operations.AssignApprovalAsync(allowed.Id, null, f.Owner);
        Assert.True(cleared.Changed);
        Assert.Null(cleared.AssignedToUserId);
    }

    [Fact]
    public async Task StaleRoleAndDisabledUserAreRejectedByLiveState()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var delegated = await f.Context.Users.SingleAsync(x => x.Id == f.DelegatedId);
        delegated.Role = UserRole.Customer;
        await f.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() => f.Operations.GetSummaryAsync(f.Delegated));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            f.Operations.AssignApprovalAsync(1, f.DelegatedId, f.Delegated));

        var owner = await f.Context.Users.SingleAsync(x => x.Id == f.OwnerId);
        owner.Status = UserStatus.Disabled;
        await f.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Operations.GetSummaryAsync(f.Owner));
    }

    [Fact]
    public async Task AssignmentDoesNotBypassLiveDelegationLimit()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var high = await AddApprovalAsync(f, Approval(f, amount: 500m));
        high.AssignedToUserId = f.LowDelegatedId;
        high.AssignedToUsername = "delegated-low";
        await f.Context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Payments.ApprovePaymentAsync(high.Id, f.LowDelegated));
        Assert.Contains("yetkinizin", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(PaymentApprovalStatus.Pending,
            (await f.Context.PaymentApprovals.SingleAsync(x => x.Id == high.Id)).Status);
    }

    [Fact]
    public async Task FiltersAndPaginationAreBoundedAndCombine()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        for (var i = 0; i < 30; i++)
        {
            var approval = Approval(f,
                amount: 10m + i,
                currency: i % 3 == 0 ? "USD" : "TRY",
                type: i % 2 == 0 ? ApprovalRequestType.Transfer : ApprovalRequestType.Deposit,
                ageMinutes: i % 4 == 0 ? 40 : 5);
            if (i % 5 == 0) approval.Description = "assigned";
            f.Context.PaymentApprovals.Add(approval);
        }
        await f.Context.SaveChangesAsync();

        var page = await f.Operations.GetApprovalsAsync(
            2, 5, "Pending", "Deposit", null, null, null, f.Owner);
        Assert.Equal(15, page.TotalCount);
        Assert.Equal(5, page.Items.Count);
        Assert.Equal(2, page.PageNumber);
        Assert.All(page.Items, row => Assert.Equal("Deposit", row.RequestType));

        var usd = await f.Operations.GetApprovalsAsync(
            1, 1000, "Pending", null, "USD", null, null, f.Owner);
        Assert.Equal(10, usd.TotalCount);
        Assert.Equal(50, usd.PageSize);
        Assert.All(usd.Items, row => Assert.Equal("USD", row.Currency));

        var overdue = await f.Operations.GetApprovalsAsync(
            1, 50, "Pending", null, null, "overdue", null, f.Owner);
        Assert.Equal(8, overdue.TotalCount);
        Assert.All(overdue.Items, row => Assert.True(row.IsOverdue));

        var unassigned = await f.Operations.GetApprovalsAsync(
            1, 50, "Pending", null, null, null, "unassigned", f.Owner);
        Assert.Equal(30, unassigned.TotalCount);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Operations.GetApprovalsAsync(1, 10, "Pending", null, null, "bogus", null, f.Owner));
    }

    [Fact]
    public async Task ReconciliationDifferencesExcludeResolvedBatchesAndHonorDelegation()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var noException = Batch(f, "no-exception.csv", PaymentApprovalStatus.Approved, onlyExternal: 2);
        var openException = Batch(f, "open-exception.csv", PaymentApprovalStatus.Approved, amountMismatch: 1);
        var closedException = Batch(f, "closed-exception.csv", PaymentApprovalStatus.Approved, onlySystem: 1);
        var noDifference = Batch(f, "no-difference.csv", PaymentApprovalStatus.Approved, onlyExternal: 0);
        f.Context.ReconciliationBatches.AddRange(noException, openException, closedException, noDifference);
        await f.Context.SaveChangesAsync();

        f.Context.ReconciliationExceptions.AddRange(
            new ReconciliationException
            {
                Id = Guid.NewGuid(), BatchId = openException.Id, ExternalReference = "EXT-OPEN",
                Status = ReconciliationExceptionStatus.Open, Description = "Synthetic open",
                CreatedAt = f.Clock.UtcNow.AddHours(-1), UpdatedAt = f.Clock.UtcNow.AddHours(-1)
            },
            new ReconciliationException
            {
                Id = Guid.NewGuid(), BatchId = closedException.Id, ExternalReference = "EXT-CLOSED",
                Status = ReconciliationExceptionStatus.Closed, Description = "Synthetic closed",
                CreatedAt = f.Clock.UtcNow.AddHours(-2), UpdatedAt = f.Clock.UtcNow.AddHours(-1)
            });
        await f.Context.SaveChangesAsync();

        var summary = await f.Operations.GetSummaryAsync(f.Owner);
        Assert.True(summary.CanViewReconciliation);
        Assert.Equal(2, summary.UnresolvedReconciliationBatchCount);
        Assert.Equal(1, summary.OpenReconciliationExceptionCount);

        var all = await f.Operations.GetReconciliationAsync(1, 25, null, "all", f.Owner);
        Assert.Equal(3, all.TotalCount);
        Assert.DoesNotContain(all.Items, x => x.BatchId == noDifference.Id);

        var open = await f.Operations.GetReconciliationAsync(1, 25, null, "open", f.Owner);
        Assert.Equal(2, open.TotalCount);
        Assert.Contains(open.Items, x => x.BatchId == noException.Id && x.ResolutionState == "NoException");
        Assert.Contains(open.Items, x => x.BatchId == openException.Id && x.UnresolvedExceptionCount == 1);

        var without = await f.Operations.GetReconciliationAsync(1, 25, null, "none", f.Owner);
        Assert.Single(without.Items);
        Assert.Equal(noException.Id, without.Items[0].BatchId);

        var delegated = await f.Operations.GetSummaryAsync(f.Delegated);
        Assert.True(delegated.CanViewReconciliation);
        Assert.Equal(2, delegated.UnresolvedReconciliationBatchCount);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            f.Operations.GetReconciliationAsync(1, 25, null, "open", f.Customer));
    }

    [Fact]
    public async Task DelegatedWithoutReconciliationPermissionCannotViewDifferences()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        f.Context.ReconciliationBatches.Add(Batch(f, "diff.csv", PaymentApprovalStatus.Approved, onlyExternal: 1));
        await f.Context.SaveChangesAsync();

        var summary = await f.Operations.GetSummaryAsync(f.LowDelegated);
        Assert.False(summary.CanViewReconciliation);
        Assert.Equal(0, summary.UnresolvedReconciliationBatchCount);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            f.Operations.GetReconciliationAsync(1, 25, null, "open", f.LowDelegated));
    }

    private static ReconciliationBatch Batch(
        Fixture f, string fileName, PaymentApprovalStatus _, int onlyExternal = 0,
        int onlySystem = 0, int amountMismatch = 0) => new()
    {
        Id = Guid.NewGuid(),
        FileName = fileName,
        FileHash = Guid.NewGuid().ToString("N"),
        ScopeKey = Guid.NewGuid().ToString("N"),
        RowCount = 10,
        Status = ReconciliationBatchStatus.Imported,
        CurrencyScope = "TRY",
        IsDemo = true,
        ImportedByUserId = f.OwnerId,
        ImportedByUsername = "owner-ops",
        CreatedAt = f.Clock.UtcNow.AddHours(-1),
        ImportedAt = f.Clock.UtcNow.AddHours(-1),
        OnlyExternalCount = onlyExternal,
        OnlySystemCount = onlySystem,
        AmountMismatchCount = amountMismatch
    };
}
