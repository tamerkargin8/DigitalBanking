using DigitalBanking.Application.Common;
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
        Guid UserA,
        Guid UserB,
        Guid CustomerA,
        Guid CustomerB,
        Guid AccountA,
        Guid AccountB,
        ActorContext ActorA,
        ActorContext ActorB);

    private static async Task<PortalFixture> CreateFixtureAsync()
    {
        var db = await TestDb.CreateAsync();
        var context = db.NewContext();
        var userA = await TestSupport.AddUserAsync(context, "customer-a-test", "synthetic", UserRole.User);
        var userB = await TestSupport.AddUserAsync(context, "customer-b-test", "synthetic", UserRole.User);
        var customerA = await TestSupport.AddCustomerAsync(context, "Ayşe", "Test", userA.Id);
        var customerB = await TestSupport.AddCustomerAsync(context, "Burak", "Test", userB.Id);
        var accountA = await TestSupport.AddAccountAsync(context, customerA.Id, 250_000m, "TRY");
        var accountB = await TestSupport.AddAccountAsync(context, customerB.Id, 25_000m, "TRY");

        var mapper = TestSupport.CreateMapper();
        var audit = new AuditService(context);
        var access = new CustomerAccessService(context);
        var payments = new PaymentApprovalService(context, mapper, audit, access);
        var portal = new CustomerPortalService(
            context,
            access,
            new AccountService(context, audit),
            new TransactionService(context, audit, TestSupport.CreateConfiguration()),
            payments,
            mapper);
        var timeline = new PaymentTimelineService(context);

        return new PortalFixture(
            db, context, portal, payments, timeline, access,
            userA.Id, userB.Id, customerA.Id, customerB.Id, accountA.Id, accountB.Id,
            new ActorContext(userA.Id, userA.Username, UserRole.User),
            new ActorContext(userB.Id, userB.Username, UserRole.User));
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
        Assert.False(await fixture.Access.UserOwnsPaymentAsync(fixture.UserA, payment.Id));
        Assert.True(await fixture.Access.UserOwnsPaymentAsync(fixture.UserB, payment.Id));
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
    public async Task CustomerCanCreateOwnTransferButNotFromAnotherCustomersAccount()
    {
        var fixture = await CreateFixtureAsync();
        await using var _ = fixture.Db;

        var own = await fixture.Portal.CreateTransferRequestAsync(
            new DigitalBanking.Application.DTOs.CustomerPortal.CreateCustomerTransferRequest
            {
                FromAccountId = fixture.AccountA,
                ToAccountId = fixture.AccountB,
                Amount = 125.50m,
                Description = "Synthetic own transfer"
            },
            fixture.ActorA);

        Assert.Equal("AutoApproved", own.Status);
        Assert.Equal(fixture.UserA, own.RequestedByUserId);
        Assert.Equal(249_874.50m, (await fixture.Context.Accounts.SingleAsync(x => x.Id == fixture.AccountA)).Balance);
        Assert.Equal(25_125.50m, (await fixture.Context.Accounts.SingleAsync(x => x.Id == fixture.AccountB)).Balance);

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
    public async Task CompletedTimelineAndReceiptAreDerivedFromRealEvents()
    {
        var fixture = await CreateFixtureAsync();
        await using var _ = fixture.Db;

        var payment = await fixture.Payments.CreateCustomerTransferRequestAsync(
            fixture.AccountA, fixture.AccountB, 100m, "Synthetic receipt", fixture.ActorA);

        var timeline = await fixture.Timeline.GetTimelineAsync(payment.Id);
        Assert.Equal(payment.Id, timeline.PaymentId);
        Assert.Equal("AutoApproved", timeline.Status);
        Assert.Equal(new[] { "Created", "AutoApproved", "Completed" }, timeline.Events.Select(x => x.Type));
        Assert.All(timeline.Events, e => Assert.True(e.OccurredAtUtc <= DateTime.UtcNow.AddSeconds(1)));
        Assert.Equal("Europe/Istanbul", timeline.TimeZoneId);

        var receipt = await fixture.Timeline.GetReceiptAsync(payment.Id);
        Assert.True(receipt.IsCompleted);
        Assert.StartsWith("PAY-", receipt.Reference);
        Assert.Equal(100m, receipt.Amount);
        Assert.Equal("TRY", receipt.Currency);
        Assert.StartsWith("••••", receipt.FromAccountMasked);
        Assert.StartsWith("••••", receipt.ToAccountMasked);
        Assert.Contains("DEMO", receipt.Disclaimer);
        Assert.NotNull(receipt.CompletedAtUtc);
        Assert.NotNull(receipt.CompletedAtLocal);
    }

    [Fact]
    public async Task PendingAndRejectedPaymentsDoNotProduceCompletedReceipts()
    {
        var fixture = await CreateFixtureAsync();
        await using var _ = fixture.Db;

        var pending = await fixture.Payments.CreateCustomerTransferRequestAsync(
            fixture.AccountA, fixture.AccountB, 75_000m, "Synthetic pending", fixture.ActorA);

        var pendingTimeline = await fixture.Timeline.GetTimelineAsync(pending.Id);
        Assert.Equal(new[] { "Created", "Pending" }, pendingTimeline.Events.Select(x => x.Type));

        var pendingReceipt = await fixture.Timeline.GetReceiptAsync(pending.Id);
        Assert.False(pendingReceipt.IsCompleted);
        Assert.Null(pendingReceipt.CompletedAtUtc);
        Assert.Contains("tamamlanmadı", pendingReceipt.StatusSummary);

        var checker = new ActorContext(Guid.NewGuid(), "checker-test", UserRole.Admin);
        await fixture.Payments.RejectPaymentAsync(pending.Id, checker, "Synthetic reject");

        var rejectedTimeline = await fixture.Timeline.GetTimelineAsync(pending.Id);
        Assert.Equal(new[] { "Created", "Pending", "Rejected" }, rejectedTimeline.Events.Select(x => x.Type));
        var rejectedReceipt = await fixture.Timeline.GetReceiptAsync(pending.Id);
        Assert.False(rejectedReceipt.IsCompleted);
        Assert.Contains("reddedildi", rejectedReceipt.StatusSummary);
    }

    [Fact]
    public async Task AdminWithoutCustomerProfileIsForbiddenFromCustomerPortal()
    {
        var fixture = await CreateFixtureAsync();
        await using var _ = fixture.Db;

        var admin = new ActorContext(Guid.NewGuid(), "admin-test", UserRole.Admin);
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Portal.GetProfileAsync(admin));
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Portal.GetAccountsAsync(admin));
    }
}
