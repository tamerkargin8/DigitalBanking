using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.ApprovalRequests;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Tests;

public class CustomerPortalTests
{
    private sealed record PortalFixture(
        TestDb Db,
        DigitalBanking.Infrastructure.Persistence.BankDbContext Context,
        CustomerPortalService Portal,
        PaymentApprovalService Payments,
        PaymentTimelineService Timeline,
        CustomerAccessService Access,
        ActorContext Owner,
        ActorContext ActorA,
        ActorContext ActorB,
        Guid CustomerA,
        Guid CustomerB,
        Guid AccountA,
        Guid AccountB);

    private static async Task<PortalFixture> CreateFixtureAsync()
    {
        var db = await TestDb.CreateAsync();
        var context = db.NewContext();
        var (owner, ownerActor) = await TestSupport.AddActorAsync(context, "owner-test", UserRole.Owner);
        var (userA, actorA) = await TestSupport.AddActorAsync(context, "customer-a-test", UserRole.Customer);
        var (userB, actorB) = await TestSupport.AddActorAsync(context, "customer-b-test", UserRole.Customer);
        var customerA = await TestSupport.AddCustomerAsync(context, "Ayşe", "Test", userA.Id);
        var customerB = await TestSupport.AddCustomerAsync(context, "Burak", "Test", userB.Id);
        var accountA = await TestSupport.AddAccountAsync(context, customerA.Id, 250_000m, "TRY");
        var accountB = await TestSupport.AddAccountAsync(context, customerB.Id, 25_000m, "TRY");

        var mapper = TestSupport.CreateMapper();
        var audit = new AuditService(context);
        var access = new CustomerAccessService(context);
        var authorization = new ApprovalAuthorizationService(context);
        var payments = new PaymentApprovalService(context, mapper, audit, access, authorization);
        var portal = new CustomerPortalService(
            context,
            access,
            new AccountService(context, audit),
            new TransactionService(context, audit, TestSupport.CreateConfiguration()),
            payments,
            mapper);
        var timeline = new PaymentTimelineService(context);

        return new PortalFixture(
            db, context, portal, payments, timeline, access, ownerActor, actorA, actorB,
            customerA.Id, customerB.Id, accountA.Id, accountB.Id);
    }

    [Fact]
    public async Task ProfileAndAccountsAreScopedToTheOwningUser()
    {
        var fixture = await CreateFixtureAsync();
        await using var _ = fixture.Db;

        var profile = await fixture.Portal.GetProfileAsync(fixture.ActorA);
        Assert.Equal(fixture.CustomerA, profile.CustomerId);
        Assert.Contains("Ayşe", profile.DisplayName);
        Assert.DoesNotContain("DEMO", profile.MaskedIdentityNumber);

        var accounts = await fixture.Portal.GetAccountsAsync(fixture.ActorA);
        Assert.Single(accounts);
        Assert.Equal(fixture.AccountA, accounts[0].Id);

        var bAccounts = await fixture.Portal.GetAccountsAsync(fixture.ActorB);
        Assert.Single(bAccounts);
        Assert.Equal(fixture.AccountB, bAccounts[0].Id);
        Assert.DoesNotContain(bAccounts, x => x.Id == fixture.AccountA);
    }

    [Fact]
    public async Task IdorAttemptsOnAccountAndPaymentReturnNotFound()
    {
        var fixture = await CreateFixtureAsync();
        await using var _ = fixture.Db;

        await Assert.ThrowsAsync<NotFoundException>(() =>
            fixture.Portal.GetAccountAsync(fixture.AccountB, fixture.ActorA));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            fixture.Portal.GetTransactionsAsync(fixture.AccountB, 1, 10, fixture.ActorA));

        var payment = await fixture.Payments.CreateCustomerTransferRequestAsync(
            fixture.AccountB, fixture.AccountA, 10m, "B to A", fixture.ActorB);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            fixture.Portal.GetPaymentAsync(payment.Id, fixture.ActorA));
        Assert.False(await fixture.Access.UserOwnsPaymentAsync(fixture.ActorA.UserId, payment.Id));
        Assert.True(await fixture.Access.UserOwnsPaymentAsync(fixture.ActorB.UserId, payment.Id));
    }

    [Fact]
    public async Task BeneficiariesAreMaskedAndDoNotExposeBalances()
    {
        var fixture = await CreateFixtureAsync();
        await using var _ = fixture.Db;

        var beneficiaries = await fixture.Portal.GetBeneficiariesAsync(fixture.ActorA);
        var b = Assert.Single(beneficiaries);
        Assert.Equal(fixture.AccountB, b.AccountId);
        Assert.StartsWith("••••", b.MaskedAccountNumber);
        Assert.Equal("TRY", b.Currency);
        Assert.DoesNotContain("Balance", b.GetType().GetProperties().Select(p => p.Name));
    }

    [Fact]
    public async Task CustomerTransferIsPendingUntilOwnerApproves()
    {
        var fixture = await CreateFixtureAsync();
        await using var _ = fixture.Db;

        var payment = await fixture.Portal.CreateTransferRequestAsync(
            new DigitalBanking.Application.DTOs.CustomerPortal.CreateCustomerTransferRequest
            {
                FromAccountId = fixture.AccountA,
                ToAccountId = fixture.AccountB,
                Amount = 125.50m,
                Description = "Synthetic own transfer"
            },
            fixture.ActorA);

        Assert.Equal("Pending", payment.Status);
        Assert.Equal(fixture.ActorA.UserId, payment.RequestedByUserId);
        Assert.Equal(250_000m, (await fixture.Context.Accounts.SingleAsync(x => x.Id == fixture.AccountA)).Balance);
        Assert.Equal(25_000m, (await fixture.Context.Accounts.SingleAsync(x => x.Id == fixture.AccountB)).Balance);

        await fixture.Payments.ApprovePaymentAsync(payment.Id, fixture.Owner);
        Assert.Equal(249_874.50m, (await fixture.Context.Accounts.SingleAsync(x => x.Id == fixture.AccountA)).Balance);
        Assert.Equal(25_125.50m, (await fixture.Context.Accounts.SingleAsync(x => x.Id == fixture.AccountB)).Balance);
    }

    [Fact]
    public async Task CustomerCannotCreateTransferFromAnotherCustomersAccount()
    {
        var fixture = await CreateFixtureAsync();
        await using var _ = fixture.Db;

        await Assert.ThrowsAsync<NotFoundException>(() =>
            fixture.Portal.CreateTransferRequestAsync(
                new DigitalBanking.Application.DTOs.CustomerPortal.CreateCustomerTransferRequest
                {
                    FromAccountId = fixture.AccountB,
                    ToAccountId = fixture.AccountA,
                    Amount = 1m
                },
                fixture.ActorA));
    }

    [Fact]
    public async Task TimelineAndReceiptOnlyShowCompletedAfterApproval()
    {
        var fixture = await CreateFixtureAsync();
        await using var _ = fixture.Db;

        var payment = await fixture.Payments.CreateCustomerTransferRequestAsync(
            fixture.AccountA, fixture.AccountB, 100m, "Synthetic receipt", fixture.ActorA);

        var pendingTimeline = await fixture.Timeline.GetTimelineAsync(payment.Id);
        Assert.Equal(new[] { "Created", "Pending" }, pendingTimeline.Events.Select(x => x.Type));
        var pendingReceipt = await fixture.Timeline.GetReceiptAsync(payment.Id);
        Assert.False(pendingReceipt.IsCompleted);
        Assert.Contains("tamamlanmadı", pendingReceipt.StatusSummary);

        await fixture.Payments.ApprovePaymentAsync(payment.Id, fixture.Owner);

        var timeline = await fixture.Timeline.GetTimelineAsync(payment.Id);
        Assert.Equal(new[] { "Created", "Pending", "Approved", "Completed" }, timeline.Events.Select(x => x.Type));
        var receipt = await fixture.Timeline.GetReceiptAsync(payment.Id);
        Assert.True(receipt.IsCompleted);
        Assert.StartsWith("PAY-", receipt.Reference);
        Assert.Equal(100m, receipt.Amount);
        Assert.Equal("TRY", receipt.Currency);
        Assert.StartsWith("••••", receipt.FromAccountMasked);
        Assert.StartsWith("••••", receipt.ToAccountMasked);
        Assert.Contains("DEMO", receipt.Disclaimer);
    }

    [Fact]
    public async Task RejectedPaymentHasNoCompletedReceipt()
    {
        var fixture = await CreateFixtureAsync();
        await using var _ = fixture.Db;

        var payment = await fixture.Payments.CreateCustomerTransferRequestAsync(
            fixture.AccountA, fixture.AccountB, 75_000m, "Synthetic reject", fixture.ActorA);
        await fixture.Payments.RejectPaymentAsync(payment.Id, fixture.Owner, "Synthetic reject");

        var timeline = await fixture.Timeline.GetTimelineAsync(payment.Id);
        Assert.Equal(new[] { "Created", "Pending", "Rejected" }, timeline.Events.Select(x => x.Type));
        var receipt = await fixture.Timeline.GetReceiptAsync(payment.Id);
        Assert.False(receipt.IsCompleted);
        Assert.Contains("reddedildi", receipt.StatusSummary);
    }

    [Fact]
    public async Task CustomerDepositAndAccountOpeningRequestsArePending()
    {
        var fixture = await CreateFixtureAsync();
        await using var _ = fixture.Db;

        var deposit = await fixture.Portal.CreateOperationRequestAsync(new CreateCustomerOperationRequest
        {
            RequestType = "Deposit",
            AccountId = fixture.AccountA,
            Amount = 500m,
            Description = "Demo deposit request"
        }, fixture.ActorA);
        Assert.Equal("Pending", deposit.Status);
        Assert.Equal(250_000m, (await fixture.Context.Accounts.SingleAsync(x => x.Id == fixture.AccountA)).Balance);

        var opening = await fixture.Portal.CreateOperationRequestAsync(new CreateCustomerOperationRequest
        {
            RequestType = "AccountOpening",
            Currency = "USD",
            Amount = 100m,
            Description = "New USD account request"
        }, fixture.ActorA);
        Assert.Equal("Pending", opening.Status);
        Assert.Equal("AccountOpening", opening.RequestType);
        Assert.Equal(1, await fixture.Context.Accounts.CountAsync(x => x.CustomerId == fixture.CustomerA));
    }

    [Fact]
    public async Task FirstAccountOpeningPersistsTypeNameAndOwnershipThenAllowsRename()
    {
        var fixture = await CreateFixtureAsync();
        await using var _ = fixture.Db;
        var (userC, actorC) = await TestSupport.AddActorAsync(fixture.Context, "customer-c-test", UserRole.Customer);
        var customerC = await TestSupport.AddCustomerAsync(fixture.Context, "Cem", "Test", userC.Id);

        Assert.Empty(await fixture.Portal.GetAccountsAsync(actorC));
        var opening = await fixture.Portal.CreateOperationRequestAsync(new CreateCustomerOperationRequest
        {
            RequestType = "AccountOpening",
            AccountType = "TimeDeposit",
            AccountName = "  Birikim Hesabım  ",
            Currency = "TRY",
            Amount = 0m,
            Description = "First account"
        }, actorC);

        Assert.Equal("Pending", opening.Status);
        Assert.Equal("Birikim Hesabım", opening.RequestedAccountName);
        Assert.Equal("TimeDeposit", opening.RequestedAccountType);
        await fixture.Payments.ApprovePaymentAsync(opening.Id, fixture.Owner);

        var created = await fixture.Context.Accounts.SingleAsync(x => x.CustomerId == customerC.Id);
        Assert.Equal("Birikim Hesabım", created.AccountName);
        Assert.Equal(AccountType.TimeDeposit, created.AccountType);
        Assert.Equal(0m, created.Balance);
        Assert.Contains((await fixture.Portal.GetAccountsAsync(actorC)), x => x.Id == created.Id && x.AccountType == "TimeDeposit");

        var renamed = await fixture.Portal.RenameAccountAsync(created.Id, "  Yeni Hesap Adı  ", actorC);
        Assert.Equal("Yeni Hesap Adı", renamed.AccountName);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            fixture.Portal.RenameAccountAsync(created.Id, "IDOR", fixture.ActorA));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            fixture.Portal.RenameAccountAsync(created.Id, "   ", actorC));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            fixture.Portal.RenameAccountAsync(created.Id, "<script>alert(1)</script>", actorC));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            fixture.Portal.RenameAccountAsync(created.Id, new string('A', 101), actorC));
    }

    [Fact]
    public async Task DepositAndWithdrawalRequestsMoveMoneyOnlyAtOwnerApproval()
    {
        var fixture = await CreateFixtureAsync();
        await using var _ = fixture.Db;

        var deposit = await fixture.Portal.CreateOperationRequestAsync(new CreateCustomerOperationRequest
        {
            RequestType = "Deposit",
            AccountId = fixture.AccountA,
            Amount = 500m,
            Description = "Synthetic deposit"
        }, fixture.ActorA);
        Assert.Equal("Pending", deposit.Status);
        Assert.Equal(250_000m, (await fixture.Context.Accounts.SingleAsync(x => x.Id == fixture.AccountA)).Balance);
        await fixture.Payments.ApprovePaymentAsync(deposit.Id, fixture.Owner);
        Assert.Equal(250_500m, (await fixture.Context.Accounts.SingleAsync(x => x.Id == fixture.AccountA)).Balance);

        var withdrawal = await fixture.Portal.CreateOperationRequestAsync(new CreateCustomerOperationRequest
        {
            RequestType = "Withdrawal",
            AccountId = fixture.AccountA,
            Amount = 500m,
            Description = "Synthetic withdrawal"
        }, fixture.ActorA);
        Assert.Equal("Pending", withdrawal.Status);
        await fixture.Payments.ApprovePaymentAsync(withdrawal.Id, fixture.Owner);
        Assert.Equal(250_000m, (await fixture.Context.Accounts.SingleAsync(x => x.Id == fixture.AccountA)).Balance);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            fixture.Portal.CreateOperationRequestAsync(new CreateCustomerOperationRequest
            {
                RequestType = "Withdrawal",
                AccountId = fixture.AccountA,
                Amount = 999_999m
            }, fixture.ActorA));
    }

    [Fact]
    public async Task AdminWithoutCustomerProfileIsForbiddenFromCustomerPortal()
    {
        var fixture = await CreateFixtureAsync();
        await using var _ = fixture.Db;

        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Portal.GetProfileAsync(fixture.Owner));
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Portal.GetAccountsAsync(fixture.Owner));
    }

    [Fact]
    public async Task PendingActivationCustomerIsForbidden()
    {
        var fixture = await CreateFixtureAsync();
        await using var _ = fixture.Db;

        var pending = await TestSupport.AddUserAsync(
            fixture.Context, "pending-member", "hash", UserRole.Customer, UserStatus.PendingActivation);
        var actor = new ActorContext(pending.Id, pending.Username, pending.Role);
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Portal.GetAccountsAsync(actor));
    }
}
