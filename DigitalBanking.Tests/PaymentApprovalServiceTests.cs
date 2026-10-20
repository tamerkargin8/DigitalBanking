using DigitalBanking.Application.Common;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Tests;

public class PaymentApprovalServiceTests
{
    private static PaymentApprovalService CreateService(DigitalBanking.Infrastructure.Persistence.BankDbContext context) =>
        new(context, TestSupport.CreateMapper(), new AuditService(context), new CustomerAccessService(context));

    [Fact]
    public async Task LowRiskRequestIsAutoApprovedAndMovesMoney()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var customer = await TestSupport.AddCustomerAsync(context);
        var from = await TestSupport.AddAccountAsync(context, customer.Id, 500m);
        var to = await TestSupport.AddAccountAsync(context, customer.Id, 0m);
        var service = CreateService(context);

        var approval = await service.CreateApprovalRequestAsync(
            from.Id, to.Id, 120.50m, "Synthetic low risk", TestSupport.AdminActor);

        Assert.Equal(PaymentApprovalStatus.AutoApproved.ToString(), approval.Status);
        Assert.Equal(PaymentRiskLevel.Low.ToString(), approval.RiskLevel);
        Assert.Equal("TRY", approval.Currency);
        Assert.Equal(TestSupport.AdminActor.UserId, approval.RequestedByUserId);
        Assert.NotNull(approval.RelatedTransactionId);

        Assert.Equal(379.50m, (await context.Accounts.SingleAsync(a => a.Id == from.Id)).Balance);
        Assert.Equal(120.50m, (await context.Accounts.SingleAsync(a => a.Id == to.Id)).Balance);
        Assert.Equal(2, await context.Transactions.CountAsync());
        Assert.True(await context.AuditLogs.AnyAsync(a => a.Action == "PAYMENT_AUTO_APPROVED"));
    }

    [Fact]
    public async Task MediumRiskRequestStaysPendingAndMovesNoMoney()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var customer = await TestSupport.AddCustomerAsync(context);
        var from = await TestSupport.AddAccountAsync(context, customer.Id, 100_000m);
        var to = await TestSupport.AddAccountAsync(context, customer.Id);
        var service = CreateService(context);

        var approval = await service.CreateApprovalRequestAsync(
            from.Id, to.Id, 75_000m, "Synthetic medium", TestSupport.AdminActor);

        Assert.Equal(PaymentApprovalStatus.Pending.ToString(), approval.Status);
        Assert.Equal(PaymentRiskLevel.Medium.ToString(), approval.RiskLevel);
        Assert.Equal(100_000m, (await context.Accounts.SingleAsync(a => a.Id == from.Id)).Balance);
        Assert.Equal(0, await context.Transactions.CountAsync());
        Assert.Single(await service.GetPendingApprovalsAsync());
        Assert.True(await context.AuditLogs.AnyAsync(a => a.Action == "PAYMENT_CREATED"));
    }

    [Fact]
    public async Task DifferentAdminApprovesPendingPaymentOnce()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var customer = await TestSupport.AddCustomerAsync(context);
        var from = await TestSupport.AddAccountAsync(context, customer.Id, 100_000m);
        var to = await TestSupport.AddAccountAsync(context, customer.Id);
        var service = CreateService(context);
        var reviewer = new ActorContext(Guid.NewGuid(), "checker-test", UserRole.Admin);

        var created = await service.CreateApprovalRequestAsync(
            from.Id, to.Id, 75_000m, "Synthetic medium", TestSupport.AdminActor);

        await service.ApprovePaymentAsync(created.Id, reviewer);

        var stored = await context.PaymentApprovals.AsNoTracking().SingleAsync(x => x.Id == created.Id);
        Assert.Equal(PaymentApprovalStatus.Approved, stored.Status);
        Assert.Equal(reviewer.UserId, stored.ReviewedByUserId);
        Assert.NotNull(stored.ReviewedAt);
        Assert.NotNull(stored.RelatedTransactionId);
        Assert.Equal(25_000m, (await context.Accounts.SingleAsync(a => a.Id == from.Id)).Balance);
        Assert.Equal(75_000m, (await context.Accounts.SingleAsync(a => a.Id == to.Id)).Balance);
        Assert.Equal(2, await context.Transactions.CountAsync());
        Assert.True(await context.AuditLogs.AnyAsync(a => a.Action == "PAYMENT_APPROVED"));

        // A second approval must not move money again.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ApprovePaymentAsync(created.Id, reviewer));
        Assert.Equal(25_000m, (await context.Accounts.SingleAsync(a => a.Id == from.Id)).Balance);
        Assert.Equal(2, await context.Transactions.CountAsync());
    }

    [Fact]
    public async Task RequesterCannotApproveOrRejectOwnPendingPayment()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var customer = await TestSupport.AddCustomerAsync(context);
        var from = await TestSupport.AddAccountAsync(context, customer.Id, 100_000m);
        var to = await TestSupport.AddAccountAsync(context, customer.Id);
        var service = CreateService(context);

        var created = await service.CreateApprovalRequestAsync(
            from.Id, to.Id, 75_000m, "Synthetic medium", TestSupport.AdminActor);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ApprovePaymentAsync(created.Id, TestSupport.AdminActor));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.RejectPaymentAsync(created.Id, TestSupport.AdminActor, "self review"));

        Assert.Equal(100_000m, (await context.Accounts.SingleAsync(a => a.Id == from.Id)).Balance);
        Assert.Equal(0, await context.Transactions.CountAsync());
    }

    [Fact]
    public async Task RejectRequiresReasonAndLeavesBalancesUntouched()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var customer = await TestSupport.AddCustomerAsync(context);
        var from = await TestSupport.AddAccountAsync(context, customer.Id, 100_000m);
        var to = await TestSupport.AddAccountAsync(context, customer.Id);
        var service = CreateService(context);
        var reviewer = new ActorContext(Guid.NewGuid(), "checker-test", UserRole.Admin);

        var created = await service.CreateApprovalRequestAsync(
            from.Id, to.Id, 75_000m, "Synthetic medium", TestSupport.AdminActor);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.RejectPaymentAsync(created.Id, reviewer, "  "));
        await service.RejectPaymentAsync(created.Id, reviewer, "Synthetic rejection");

        var stored = await context.PaymentApprovals.AsNoTracking().SingleAsync(x => x.Id == created.Id);
        Assert.Equal(PaymentApprovalStatus.Rejected, stored.Status);
        Assert.Equal("Synthetic rejection", stored.RejectionReason);
        Assert.Equal(100_000m, (await context.Accounts.SingleAsync(a => a.Id == from.Id)).Balance);
        Assert.Equal(0, await context.Transactions.CountAsync());
        Assert.True(await context.AuditLogs.AnyAsync(a => a.Action == "PAYMENT_REJECTED"));
    }

    [Theory]
    [InlineData("same-account")]
    [InlineData("cross-currency")]
    [InlineData("insufficient")]
    [InlineData("missing")]
    [InlineData("scale")]
    public async Task InvalidRequestsAreRejectedWithoutSideEffects(string scenario)
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var customer = await TestSupport.AddCustomerAsync(context);
        var from = await TestSupport.AddAccountAsync(context, customer.Id, 100m, "TRY");
        var to = await TestSupport.AddAccountAsync(context, customer.Id, 0m, scenario == "cross-currency" ? "USD" : "TRY");
        var service = CreateService(context);

        var toId = to.Id;
        var amount = 75m;
        if (scenario == "same-account") toId = from.Id;
        if (scenario == "insufficient") amount = 101m;
        if (scenario == "missing") toId = Guid.NewGuid();
        if (scenario == "scale") amount = 1.001m;

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            service.CreateApprovalRequestAsync(from.Id, toId, amount, "synthetic", TestSupport.AdminActor));

        if (scenario == "missing")
            Assert.IsType<NotFoundException>(exception);
        else
            Assert.IsType<BusinessRuleException>(exception);

        Assert.Equal(100m, (await context.Accounts.SingleAsync(a => a.Id == from.Id)).Balance);
        Assert.Equal(0, await context.PaymentApprovals.CountAsync());
        Assert.Equal(0, await context.Transactions.CountAsync());
    }

    [Fact]
    public async Task StandardUserCannotCreateOrReviewPaymentRequests()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var customer = await TestSupport.AddCustomerAsync(context);
        var from = await TestSupport.AddAccountAsync(context, customer.Id, 1000m);
        var to = await TestSupport.AddAccountAsync(context, customer.Id);
        var service = CreateService(context);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateApprovalRequestAsync(from.Id, to.Id, 10m, null, TestSupport.StandardActor));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ApprovePaymentAsync(1, TestSupport.StandardActor));
    }

    [Fact]
    public async Task StaleApprovalRowIsRejectedByRowVersion()
    {
        await using var db = await TestDb.CreateAsync();
        var approvalId = 0;

        await using (var setup = db.NewContext())
        {
            var customer = await TestSupport.AddCustomerAsync(setup);
            var from = await TestSupport.AddAccountAsync(setup, customer.Id, 100_000m);
            var to = await TestSupport.AddAccountAsync(setup, customer.Id);
            var service = CreateService(setup);
            var created = await service.CreateApprovalRequestAsync(
                from.Id, to.Id, 75_000m, "Synthetic medium", TestSupport.AdminActor);
            approvalId = created.Id;
        }

        await using var first = db.NewContext();
        await using var second = db.NewContext();
        var firstApproval = await first.PaymentApprovals.SingleAsync(x => x.Id == approvalId);
        var secondApproval = await second.PaymentApprovals.SingleAsync(x => x.Id == approvalId);

        firstApproval.Status = PaymentApprovalStatus.Approved;
        firstApproval.ReviewedAt = DateTime.UtcNow;
        await first.SaveChangesAsync();

        secondApproval.Status = PaymentApprovalStatus.Approved;
        secondApproval.ReviewedAt = DateTime.UtcNow;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }
}
