using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Account;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Tests;

public class AccountServiceTests
{
    private static readonly ActorContext Owner = new(Guid.NewGuid(), "owner-test", UserRole.Owner);
    private static readonly ActorContext Customer = new(Guid.NewGuid(), "customer-test", UserRole.Customer);

    [Fact]
    public async Task OpeningAccountHonorsCurrencyAndInitialBalanceAndWritesAudit()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var customer = await TestSupport.AddCustomerAsync(context);
        var service = new AccountService(context, new AuditService(context));

        var account = await service.OpenAccountAsync(
            new CreateAccountRequest
            {
                CustomerId = customer.Id,
                Currency = "usd",
                InitialBalance = 150.25m
            },
            Owner);

        Assert.Equal("USD", account.Currency);
        Assert.Equal(150.25m, account.Balance);

        var stored = await context.Accounts.SingleAsync(x => x.Id == account.Id);
        Assert.Equal(150.25m, stored.Balance);
        Assert.Equal("USD", stored.Currency);

        var opening = await context.Transactions.SingleAsync(x => x.AccountId == account.Id);
        Assert.Equal(TransactionType.Deposit, opening.Type);
        Assert.Equal(150.25m, opening.Amount);
        Assert.Equal(150.25m, opening.BalanceAfter);

        var audit = await context.AuditLogs.SingleAsync(x => x.Action == "ACCOUNT_OPENED");
        Assert.Equal(account.Id.ToString(), audit.EntityId);
        Assert.DoesNotContain("password", audit.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StandardUserCannotOpenAccount()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var customer = await TestSupport.AddCustomerAsync(context);
        var service = new AccountService(context, new AuditService(context));

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.OpenAccountAsync(new CreateAccountRequest { CustomerId = customer.Id }, Customer));

        Assert.Contains("Admin", exception.Message);
        Assert.Equal(0, await context.Accounts.CountAsync());
    }

    [Theory]
    [InlineData("BTC")]
    [InlineData("TR")]
    public async Task UnsupportedCurrencyIsRejected(string currency)
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var customer = await TestSupport.AddCustomerAsync(context);
        var service = new AccountService(context, new AuditService(context));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.OpenAccountAsync(
                new CreateAccountRequest { CustomerId = customer.Id, Currency = currency },
                Owner));
    }

    [Fact]
    public async Task NegativeOpeningBalanceAndMissingCustomerAreRejected()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var customer = await TestSupport.AddCustomerAsync(context);
        var service = new AccountService(context, new AuditService(context));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.OpenAccountAsync(
                new CreateAccountRequest { CustomerId = customer.Id, InitialBalance = -1m },
                Owner));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.OpenAccountAsync(
                new CreateAccountRequest { CustomerId = Guid.NewGuid() },
                Owner));
    }

    [Fact]
    public async Task ZeroInitialBalanceCreatesNoOpeningTransaction()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var customer = await TestSupport.AddCustomerAsync(context);
        var service = new AccountService(context, new AuditService(context));

        var account = await service.OpenAccountAsync(
            new CreateAccountRequest { CustomerId = customer.Id },
            Owner);

        Assert.Equal(0m, account.Balance);
        Assert.Equal("TRY", account.Currency);
        Assert.False(await context.Transactions.AnyAsync(x => x.AccountId == account.Id));
    }
}
