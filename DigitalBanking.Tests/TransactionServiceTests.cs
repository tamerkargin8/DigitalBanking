using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Transaction;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using DigitalBanking.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Tests;

public class TransactionServiceTests
{
    private static TransactionService CreateService(BankDbContext context) =>
        new(context, new AuditService(context), TestSupport.CreateConfiguration());

    [Fact]
    public async Task DepositAndWithdrawKeepDecimalPrecisionAndAudit()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var customer = await TestSupport.AddCustomerAsync(context);
        var account = await TestSupport.AddAccountAsync(context, customer.Id);
        var service = CreateService(context);

        await service.DepositAsync(new DepositRequest { AccountId = account.Id, Amount = 100.25m }, TestSupport.AdminActor);
        var withdrawal = await service.WithdrawAsync(new WithdrawRequest { AccountId = account.Id, Amount = 10.10m }, TestSupport.AdminActor);

        Assert.Equal(90.15m, withdrawal.BalanceAfter);
        Assert.Equal(90.15m, (await context.Accounts.SingleAsync(a => a.Id == account.Id)).Balance);
        Assert.Equal(2, await context.Transactions.CountAsync(t => t.AccountId == account.Id));
        Assert.Equal(2, await context.AuditLogs.CountAsync(a => a.Action == "DEPOSIT_SUCCESS" || a.Action == "WITHDRAW_SUCCESS"));
    }

    [Fact]
    public async Task StandardUserCannotMoveMoney()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var customer = await TestSupport.AddCustomerAsync(context);
        var from = await TestSupport.AddAccountAsync(context, customer.Id, 100m);
        var to = await TestSupport.AddAccountAsync(context, customer.Id);
        var service = CreateService(context);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.DepositAsync(new DepositRequest { AccountId = from.Id, Amount = 1m }, TestSupport.StandardActor));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.WithdrawAsync(new WithdrawRequest { AccountId = from.Id, Amount = 1m }, TestSupport.StandardActor));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.TransferAsync(
                new TransferRequest { FromAccountId = from.Id, ToAccountId = to.Id, Amount = 1m },
                TestSupport.StandardActor));

        Assert.Equal(100m, (await context.Accounts.SingleAsync(a => a.Id == from.Id)).Balance);
        Assert.Equal(0, await context.Transactions.CountAsync());
    }

    [Fact]
    public async Task LowValueTransferMovesBothBalancesAndUsesTransferTypes()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var customer = await TestSupport.AddCustomerAsync(context);
        var from = await TestSupport.AddAccountAsync(context, customer.Id, 100.25m);
        var to = await TestSupport.AddAccountAsync(context, customer.Id, 5.05m);
        var service = CreateService(context);

        var result = await service.TransferAsync(
            new TransferRequest { FromAccountId = from.Id, ToAccountId = to.Id, Amount = 20.05m },
            TestSupport.AdminActor);

        Assert.Equal(80.20m, result.OutgoingTransaction.BalanceAfter);
        Assert.Equal(25.10m, result.IncomingTransaction.BalanceAfter);
        Assert.Equal(TransactionType.TransferOut.ToString(), result.OutgoingTransaction.Type);
        Assert.Equal(TransactionType.TransferIn.ToString(), result.IncomingTransaction.Type);
        Assert.Equal(80.20m, (await context.Accounts.SingleAsync(a => a.Id == from.Id)).Balance);
        Assert.Equal(25.10m, (await context.Accounts.SingleAsync(a => a.Id == to.Id)).Balance);
    }

    [Fact]
    public async Task DirectTransferAtOrAboveApprovalLimitIsRejected()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var customer = await TestSupport.AddCustomerAsync(context);
        var from = await TestSupport.AddAccountAsync(context, customer.Id, 100_000m);
        var to = await TestSupport.AddAccountAsync(context, customer.Id);
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.TransferAsync(
                new TransferRequest { FromAccountId = from.Id, ToAccountId = to.Id, Amount = PaymentPolicy.AutoApprovalLimit },
                TestSupport.AdminActor));

        Assert.Contains("approval", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(100_000m, (await context.Accounts.SingleAsync(a => a.Id == from.Id)).Balance);
        Assert.Equal(0, await context.Transactions.CountAsync());
    }

    [Theory]
    [InlineData("same")]
    [InlineData("insufficient")]
    [InlineData("missing-from")]
    [InlineData("missing-to")]
    [InlineData("cross-currency")]
    [InlineData("scale")]
    public async Task InvalidTransfersLeaveBalancesUntouched(string scenario)
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var customer = await TestSupport.AddCustomerAsync(context);
        var from = await TestSupport.AddAccountAsync(context, customer.Id, 10m, "TRY");
        var to = await TestSupport.AddAccountAsync(context, customer.Id, 0m, scenario == "cross-currency" ? "USD" : "TRY");
        var service = CreateService(context);

        var request = new TransferRequest { FromAccountId = from.Id, ToAccountId = to.Id, Amount = 5m };
        if (scenario == "same") request.ToAccountId = from.Id;
        if (scenario == "insufficient") request.Amount = 11m;
        if (scenario == "missing-from") request.FromAccountId = Guid.NewGuid();
        if (scenario == "missing-to") request.ToAccountId = Guid.NewGuid();
        if (scenario == "scale") request.Amount = 1.001m;

        var expected = scenario is "missing-from" or "missing-to"
            ? typeof(NotFoundException)
            : typeof(BusinessRuleException);

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            service.TransferAsync(request, TestSupport.AdminActor));
        Assert.IsType(expected, exception);

        Assert.Equal(10m, (await context.Accounts.SingleAsync(a => a.Id == from.Id)).Balance);
        Assert.Equal(0m, (await context.Accounts.SingleAsync(a => a.Id == to.Id)).Balance);
        Assert.Equal(0, await context.Transactions.CountAsync());
    }

    [Fact]
    public async Task DatabaseFailureRollsBackBalanceAndLedgerRows()
    {
        await using var db = await TestDb.CreateAsync();
        Guid accountId;
        await using (var setup = db.NewContext())
        {
            var customer = await TestSupport.AddCustomerAsync(setup);
            var account = await TestSupport.AddAccountAsync(setup, customer.Id, 25m);
            accountId = account.Id;
        }

        await using (var failing = new FailingBankDbContext(db.Options))
        {
            var service = CreateService(failing);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DepositAsync(new DepositRequest { AccountId = accountId, Amount = 10m }, TestSupport.AdminActor));
        }

        await using var verification = db.NewContext();
        Assert.Equal(25m, (await verification.Accounts.SingleAsync(a => a.Id == accountId)).Balance);
        Assert.False(await verification.Transactions.AnyAsync(x => x.AccountId == accountId));
        Assert.False(await verification.AuditLogs.AnyAsync());
    }

    [Fact]
    public async Task StatementIncludesWholeEndDayAndReportsAreCurrencyScoped()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var customer = await TestSupport.AddCustomerAsync(context);
        var tryAccount = await TestSupport.AddAccountAsync(context, customer.Id, 0m, "TRY");
        var usdAccount = await TestSupport.AddAccountAsync(context, customer.Id, 0m, "USD");
        var service = CreateService(context);

        await service.DepositAsync(new DepositRequest { AccountId = tryAccount.Id, Amount = 12.34m }, TestSupport.AdminActor);
        await service.DepositAsync(new DepositRequest { AccountId = usdAccount.Id, Amount = 99.99m }, TestSupport.AdminActor);

        var today = DateTime.UtcNow.Date;
        var statement = await service.GetStatementAsync(tryAccount.Id, today, today);
        Assert.Equal(1, statement.Count);
        Assert.Equal("TRY", statement.Currency);
        Assert.True(statement.To > today);

        var topTry = await service.GetTopAccountsAsync(today, today, 10, "TRY");
        Assert.Single(topTry);
        Assert.Equal(tryAccount.Id, topTry[0].AccountId);
        Assert.Equal(12.34m, topTry[0].DepositTotal);
        Assert.Equal(12.34m, topTry[0].NetFlow);

        var topUsd = await service.GetTopAccountsAsync(today, today, 10, "USD");
        Assert.Single(topUsd);
        Assert.Equal(99.99m, topUsd[0].NetFlow);
    }

    [Fact]
    public async Task HistoryPaginationIsClampedAndAccountScoped()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var customer = await TestSupport.AddCustomerAsync(context);
        var account = await TestSupport.AddAccountAsync(context, customer.Id);
        var other = await TestSupport.AddAccountAsync(context, customer.Id);
        var service = CreateService(context);

        for (var i = 0; i < 3; i++)
            await service.DepositAsync(new DepositRequest { AccountId = account.Id, Amount = 1m + i }, TestSupport.AdminActor);
        await service.DepositAsync(new DepositRequest { AccountId = other.Id, Amount = 5m }, TestSupport.AdminActor);

        var history = await service.GetTransactionHistoryAsync(account.Id, page: 0, pageSize: 500);
        Assert.Equal(1, history.Pagination.PageNumber);
        Assert.Equal(100, history.Pagination.PageSize);
        Assert.Equal(3, history.Pagination.TotalCount);
        Assert.All(history.Transactions, t => Assert.Equal(account.Id, t.AccountId));
    }

    private sealed class FailingBankDbContext : BankDbContext
    {
        public FailingBankDbContext(DbContextOptions<BankDbContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated database failure.");
    }
}
