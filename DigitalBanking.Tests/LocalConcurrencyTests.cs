using DigitalBanking.Domain.Entities;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Tests;

public class LocalConcurrencyTests
{
    [Fact]
    public async Task StaleBalanceUpdateIsRejectedAndCommittedBalanceIsPreserved()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BankDbContext>().UseSqlite(connection).Options;
        var id = Guid.NewGuid();
        await using (var setup = new BankDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Accounts.Add(new Account
            {
                Id = id, AccountNumber = "SYNTHETIC-TEST", Balance = 100m,
                Customer = new Customer { Id = Guid.NewGuid(), FirstName = "Demo", LastName = "Test", IdentityNumber = "SYNTHETIC" }
            });
            await setup.SaveChangesAsync();
        }

        await using var first = new BankDbContext(options);
        await using var second = new BankDbContext(options);
        var account1 = await first.Accounts.SingleAsync(a => a.Id == id);
        var account2 = await second.Accounts.SingleAsync(a => a.Id == id);
        Assert.NotEmpty(account1.RowVersion);
        account1.Balance -= 10.10m;
        await first.SaveChangesAsync();
        account2.Balance -= 20.20m;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        await using var verification = new BankDbContext(options);
        Assert.Equal(89.90m, (await verification.Accounts.SingleAsync(a => a.Id == id)).Balance);
    }
}
