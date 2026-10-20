using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.ApprovalRequests;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Tests;

public class PaymentApprovalServiceTests
{
    private sealed record Fixture(
        TestDb Db,
        DigitalBanking.Infrastructure.Persistence.BankDbContext Context,
        PaymentApprovalService Service,
        ActorContext Owner,
        ActorContext Delegated100k,
        ActorContext DelegatedTransferOnly,
        ActorContext Customer,
        Guid OwnerId,
        Guid Delegated100kId,
        Guid CustomerId,
        Guid AccountTry,
        Guid AccountUsd,
        Guid AccountB,
        Guid AccountBUsd);

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var db = await TestDb.CreateAsync();
        var context = db.NewContext();
        var (owner, ownerActor) = await TestSupport.AddActorAsync(context, "owner-test", UserRole.Owner);
        var (delegated, delegatedActor) = await TestSupport.AddActorAsync(context, "delegated-100k", UserRole.DelegatedApprover);
        var (transferOnly, transferOnlyActor) = await TestSupport.AddActorAsync(context, "delegated-transfer", UserRole.DelegatedApprover);
        var (customerUser, customerActor) = await TestSupport.AddActorAsync(context, "customer-test", UserRole.Customer);
        var customer = await TestSupport.AddCustomerAsync(context, "Ayşe", "Customer", customerUser.Id);
        var customerB = await TestSupport.AddCustomerAsync(context, "Burak", "Customer");
        var accountTry = await TestSupport.AddAccountAsync(context, customer.Id, 300_000m, "TRY");
        var accountUsd = await TestSupport.AddAccountAsync(context, customer.Id, 10_000m, "USD");
        var accountB = await TestSupport.AddAccountAsync(context, customerB.Id, 50_000m, "TRY");
        var accountBUsd = await TestSupport.AddAccountAsync(context, customerB.Id, 5_000m, "USD");

        foreach (var operation in new[]
                 {
                     ApprovalOperation.Transfer, ApprovalOperation.Deposit,
                     ApprovalOperation.Withdrawal, ApprovalOperation.AccountOpening
                 })
        {
            context.ApprovalDelegations.Add(Delegation(delegated.Id, operation, "TRY", 100_000m, owner.Id));
        }
        context.ApprovalDelegations.Add(Delegation(transferOnly.Id, ApprovalOperation.Transfer, "TRY", 10_000m, owner.Id));
        await context.SaveChangesAsync();

        var mapper = TestSupport.CreateMapper();
        var audit = new AuditService(context);
        var access = new CustomerAccessService(context);
        var authorization = new ApprovalAuthorizationService(context);
        var service = new PaymentApprovalService(context, mapper, audit, access, authorization);

        return new Fixture(db, context, service, ownerActor, delegatedActor, transferOnlyActor, customerActor,
            owner.Id, delegated.Id, customer.Id, accountTry.Id, accountUsd.Id, accountB.Id, accountBUsd.Id);
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
        CreatedByOwnerUsername = "owner-test",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task LowRiskCustomerTransferStaysPendingAndOwnerApprovalMovesMoney()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var payment = await f.Service.CreateCustomerTransferRequestAsync(
            f.AccountTry, f.AccountB, 10m, "Low risk but still approval", f.Customer);

        Assert.Equal("Pending", payment.Status);
        Assert.Equal("Transfer", payment.RequestType);
        Assert.Equal(300_000m, (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountTry)).Balance);
        Assert.Equal(50_000m, (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountB)).Balance);

        var timelineService = new PaymentTimelineService(f.Context);
        var timeline = await timelineService.GetTimelineAsync(payment.Id);
        Assert.Equal(new[] { "Created", "Pending" }, timeline.Events.Select(x => x.Type));
        Assert.False((await timelineService.GetReceiptAsync(payment.Id)).IsCompleted);

        await f.Service.ApprovePaymentAsync(payment.Id, f.Owner);

        Assert.Equal(299_990m, (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountTry)).Balance);
        Assert.Equal(50_010m, (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountB)).Balance);
        var stored = await f.Context.PaymentApprovals.SingleAsync(x => x.Id == payment.Id);
        Assert.Equal(PaymentApprovalStatus.Approved, stored.Status);
        Assert.Equal(f.OwnerId, stored.ReviewedByUserId);
    }

    [Fact]
    public async Task DelegatedLimitIsInclusiveAt100000AndExclusiveAbove()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var atLimit = await f.Service.CreateCustomerTransferRequestAsync(
            f.AccountTry, f.AccountB, 100_000m, "At limit", f.Customer);
        await f.Service.ApprovePaymentAsync(atLimit.Id, f.Delegated100k);
        Assert.Equal(200_000m, (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountTry)).Balance);

        var overLimit = await f.Service.CreateCustomerTransferRequestAsync(
            f.AccountTry, f.AccountB, 100_000.01m, "One kurus over", f.Customer);
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Service.ApprovePaymentAsync(overLimit.Id, f.Delegated100k));
        Assert.Contains("Owner", exception.Message);
        Assert.Equal(PaymentApprovalStatus.Pending,
            (await f.Context.PaymentApprovals.SingleAsync(x => x.Id == overLimit.Id)).Status);

        await f.Service.ApprovePaymentAsync(overLimit.Id, f.Owner);
        Assert.Equal(PaymentApprovalStatus.Approved,
            (await f.Context.PaymentApprovals.SingleAsync(x => x.Id == overLimit.Id)).Status);
    }

    [Fact]
    public async Task CurrencyAndOperationScopesAreEnforcedWithoutFx()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var usdTransfer = await f.Service.CreateCustomerTransferRequestAsync(
            f.AccountUsd, f.AccountBUsd, 100m, "USD transfer", f.Customer);

        var currencyError = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Service.ApprovePaymentAsync(usdTransfer.Id, f.Delegated100k));
        Assert.Contains("para birimi", currencyError.Message, StringComparison.OrdinalIgnoreCase);
        await f.Service.ApprovePaymentAsync(usdTransfer.Id, f.Owner);

        var deposit = await f.Service.CreateCustomerOperationRequestAsync(new CreateCustomerOperationRequest
        {
            RequestType = "Deposit",
            AccountId = f.AccountTry,
            Amount = 10m,
            Description = "Deposit request"
        }, f.Customer);
        var operationError = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Service.ApprovePaymentAsync(deposit.Id, f.DelegatedTransferOnly));
        Assert.Contains("yetki", operationError.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SelfApprovalIsBlockedEvenForOwner()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var request = await f.Service.CreateApprovalRequestAsync(
            f.AccountTry, f.AccountB, 50m, "Owner-created", f.Owner);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Service.ApprovePaymentAsync(request.Id, f.Owner));
        Assert.Contains("Kendi", exception.Message);
        Assert.Equal(300_000m, (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountTry)).Balance);

        await f.Service.ApprovePaymentAsync(request.Id, f.Delegated100k);
        Assert.Equal(299_950m, (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountTry)).Balance);
    }

    [Fact]
    public async Task RevokedOrExpiredDelegationIsImmediatelyIneffective()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var first = await f.Service.CreateCustomerTransferRequestAsync(
            f.AccountTry, f.AccountB, 10m, "Before revoke", f.Customer);
        var delegation = await f.Context.ApprovalDelegations
            .FirstAsync(x => x.UserId == f.Delegated100kId && x.Operation == ApprovalOperation.Transfer);
        delegation.IsActive = false;
        delegation.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await f.Context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Service.ApprovePaymentAsync(first.Id, f.Delegated100k));
        Assert.Contains("aktif", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(PaymentApprovalStatus.Pending,
            (await f.Context.PaymentApprovals.SingleAsync(x => x.Id == first.Id)).Status);
    }

    [Fact]
    public async Task StaleJwtRoleCannotCreateOwnerOperationalRequestsAfterRoleChange()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var user = await f.Context.Users.SingleAsync(x => x.Id == f.Delegated100kId);
        user.Role = UserRole.Customer;
        await f.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            f.Service.CreateApprovalRequestAsync(f.AccountTry, f.AccountB, 1m, "stale", f.Delegated100k));
    }

    [Fact]
    public async Task PendingAndCustomerUsersCannotApprove()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var pending = await TestSupport.AddUserAsync(f.Context, "pending-user", "hash", UserRole.Customer, UserStatus.PendingActivation);
        var pendingActor = new ActorContext(pending.Id, pending.Username, pending.Role);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            f.Service.ApprovePaymentAsync(1, pendingActor));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            f.Service.ApprovePaymentAsync(1, f.Customer));
    }

    [Fact]
    public async Task DoubleApprovalOnlyMovesMoneyOnce()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var payment = await f.Service.CreateCustomerTransferRequestAsync(
            f.AccountTry, f.AccountB, 25m, "Double approve", f.Customer);
        await f.Service.ApprovePaymentAsync(payment.Id, f.Delegated100k);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Service.ApprovePaymentAsync(payment.Id, f.Owner));
        Assert.Equal(299_975m, (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountTry)).Balance);
        Assert.Equal(2, await f.Context.Transactions.CountAsync(x => x.CorrelationId == payment.CorrelationId));
    }

    [Fact]
    public async Task CustomerRequestIsRejectedIfLoginNoLongerOwnsCustomerProfile()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var payment = await f.Service.CreateCustomerTransferRequestAsync(
            f.AccountTry, f.AccountB, 10m, "Ownership changed", f.Customer);

        var customer = await f.Context.Customers.SingleAsync(x => x.Id == f.CustomerId);
        customer.UserId = null;
        await f.Context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Service.ApprovePaymentAsync(payment.Id, f.Owner));

        Assert.Contains("bağlı değil", exception.Message);
        Assert.Equal(PaymentApprovalStatus.Pending,
            (await f.Context.PaymentApprovals.SingleAsync(x => x.Id == payment.Id)).Status);
        Assert.Equal(300_000m, (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountTry)).Balance);
    }

    [Fact]
    public async Task CustomerRequestIsRejectedIfAccountOwnershipChangedBeforeApproval()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var payment = await f.Service.CreateCustomerTransferRequestAsync(
            f.AccountTry, f.AccountB, 10m, "Account ownership changed", f.Customer);

        var otherCustomerId = await f.Context.Customers
            .Where(x => x.Id != f.CustomerId)
            .Select(x => x.Id)
            .FirstAsync();
        var account = await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountTry);
        account.CustomerId = otherCustomerId;
        await f.Context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Service.ApprovePaymentAsync(payment.Id, f.Owner));

        Assert.Contains("Hesap sahipliği", exception.Message);
        Assert.Equal(PaymentApprovalStatus.Pending,
            (await f.Context.PaymentApprovals.SingleAsync(x => x.Id == payment.Id)).Status);
    }

    [Fact]
    public async Task ConcurrentApproveAndRejectMapsTheLoserToConcurrencyConflict()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var payment = await f.Service.CreateCustomerTransferRequestAsync(
            f.AccountTry, f.AccountB, 10m, "Concurrent decision", f.Customer);

        await using var staleContext = f.Db.NewContext();
        var staleService = new PaymentApprovalService(
            staleContext, TestSupport.CreateMapper(), new AuditService(staleContext),
            new CustomerAccessService(staleContext), new ApprovalAuthorizationService(staleContext));
        var stalePayment = await staleContext.PaymentApprovals.SingleAsync(x => x.Id == payment.Id);
        Assert.Equal(PaymentApprovalStatus.Pending, stalePayment.Status);

        await f.Service.ApprovePaymentAsync(payment.Id, f.Owner);

        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            staleService.RejectPaymentAsync(payment.Id, f.Owner, "Concurrent reject"));

        Assert.Equal(PaymentApprovalStatus.Approved,
            (await f.Context.PaymentApprovals.SingleAsync(x => x.Id == payment.Id)).Status);
        Assert.Equal(299_990m, (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountTry)).Balance);
    }

    [Fact]
    public async Task PendingOutgoingRequestsReduceAvailableBalance()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        await f.Service.CreateCustomerTransferRequestAsync(f.AccountTry, f.AccountB, 250_000m, "First", f.Customer);
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Service.CreateCustomerTransferRequestAsync(f.AccountTry, f.AccountB, 60_000m, "Second", f.Customer));

        Assert.Contains("Yetersiz kullanılabilir", exception.Message);
        Assert.Equal(1, await f.Context.PaymentApprovals.CountAsync());
    }

    [Fact]
    public async Task InsufficientBalanceAtApprovalFailsWithoutLedgerChange()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var payment = await f.Service.CreateCustomerOperationRequestAsync(new CreateCustomerOperationRequest
        {
            RequestType = "Withdrawal",
            AccountId = f.AccountTry,
            Amount = 150_000m,
            Description = "Pending withdrawal"
        }, f.Customer);

        var account = await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountTry);
        account.Balance = 100m;
        await f.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Service.ApprovePaymentAsync(payment.Id, f.Delegated100k));
        Assert.Equal(PaymentApprovalStatus.Pending,
            (await f.Context.PaymentApprovals.SingleAsync(x => x.Id == payment.Id)).Status);
        Assert.Equal(100m, (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountTry)).Balance);
        Assert.False(await f.Context.Transactions.AnyAsync(x => x.CorrelationId == payment.CorrelationId));
    }

    [Fact]
    public async Task DepositAndAccountOpeningAreRequestsAppliedOnApproval()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var deposit = await f.Service.CreateCustomerOperationRequestAsync(new CreateCustomerOperationRequest
        {
            RequestType = "Deposit",
            AccountId = f.AccountTry,
            Amount = 1_000m,
            Description = "Deposit demo"
        }, f.Customer);
        Assert.Equal("Pending", deposit.Status);
        Assert.Equal(300_000m, (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountTry)).Balance);
        await f.Service.ApprovePaymentAsync(deposit.Id, f.Delegated100k);
        Assert.Equal(301_000m, (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountTry)).Balance);

        var opening = await f.Service.CreateCustomerOperationRequestAsync(new CreateCustomerOperationRequest
        {
            RequestType = "AccountOpening",
            Currency = "TRY",
            Amount = 5_000m,
            Description = "Open new account"
        }, f.Customer);
        Assert.Equal("Pending", opening.Status);
        Assert.Equal(2, await f.Context.Accounts.CountAsync(x => x.CustomerId == f.CustomerId));

        await f.Service.ApprovePaymentAsync(opening.Id, f.Delegated100k);
        Assert.Equal(3, await f.Context.Accounts.CountAsync(x => x.CustomerId == f.CustomerId));
        var storedOpening = await f.Context.PaymentApprovals.SingleAsync(x => x.Id == opening.Id);
        Assert.NotNull(storedOpening.CreatedAccountId);
        Assert.Equal(5_000m, (await f.Context.Accounts.SingleAsync(x => x.Id == storedOpening.CreatedAccountId)).Balance);
    }

    [Fact]
    public async Task RejectionAndCancellationDoNotChangeBalances()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var rejected = await f.Service.CreateCustomerTransferRequestAsync(
            f.AccountTry, f.AccountB, 100m, "Reject me", f.Customer);
        await f.Service.RejectPaymentAsync(rejected.Id, f.Delegated100k, "Synthetic rejection");
        Assert.Equal(PaymentApprovalStatus.Rejected,
            (await f.Context.PaymentApprovals.SingleAsync(x => x.Id == rejected.Id)).Status);

        var cancelled = await f.Service.CreateCustomerTransferRequestAsync(
            f.AccountTry, f.AccountB, 200m, "Cancel me", f.Customer);
        await f.Service.CancelAsync(cancelled.Id, f.Customer);
        Assert.Equal(PaymentApprovalStatus.Cancelled,
            (await f.Context.PaymentApprovals.SingleAsync(x => x.Id == cancelled.Id)).Status);

        Assert.Equal(300_000m, (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountTry)).Balance);
        Assert.Equal(50_000m, (await f.Context.Accounts.SingleAsync(x => x.Id == f.AccountB)).Balance);
    }

    [Fact]
    public async Task DailyLimitIsOptionalButEnforcedWhenConfigured()
    {
        var f = await CreateFixtureAsync();
        await using var _ = f.Db;

        var delegation = await f.Context.ApprovalDelegations
            .FirstAsync(x => x.UserId == f.Delegated100kId && x.Operation == ApprovalOperation.Transfer);
        delegation.DailyLimit = 100_000m;
        await f.Context.SaveChangesAsync();

        var first = await f.Service.CreateCustomerTransferRequestAsync(
            f.AccountTry, f.AccountB, 60_000m, "First daily", f.Customer);
        await f.Service.ApprovePaymentAsync(first.Id, f.Delegated100k);

        var second = await f.Service.CreateCustomerTransferRequestAsync(
            f.AccountTry, f.AccountB, 60_000m, "Second daily", f.Customer);
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Service.ApprovePaymentAsync(second.Id, f.Delegated100k));
        Assert.Contains("günlük", exception.Message, StringComparison.OrdinalIgnoreCase);
        await f.Service.ApprovePaymentAsync(second.Id, f.Owner);
    }
}
