using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Infrastructure.Persistence;

/// <summary>
/// Deterministic Development seed. Re-running must not inflate balances:
/// users/customers/accounts are created only when missing, and opening ledger rows
/// are written only with the account. Demo center reset is scoped to IsDemo data.
/// </summary>
public static class DatabaseSeeder
{
    private static readonly (string Username, UserRole Role)[] Users =
    {
        ("admin", UserRole.Admin),
        ("admin.checker", UserRole.Admin),
        ("tamer", UserRole.User),
        ("customer.a", UserRole.User),
        ("customer.b", UserRole.User)
    };

    private static readonly (string AccountNumber, string Owner, string Currency, decimal OpeningBalance)[] Accounts =
    {
        ("DEMO-A-TRY", "A", "TRY", 250_000m),
        ("DEMO-A-USD", "A", "USD", 3_000m),
        ("DEMO-A-EUR", "A", "EUR", 1_500m),
        ("DEMO-B-TRY", "B", "TRY", 25_000m),
        ("DEMO-B-USD", "B", "USD", 800m),
        ("DEMO-B-EUR", "B", "EUR", 400m)
    };

    public static async Task SeedAsync(
        BankDbContext context,
        PasswordHasher passwordHasher,
        string demoPassword)
    {
        if (context.Database.IsSqlite())
            await context.Database.EnsureCreatedAsync();
        else
            await context.Database.MigrateAsync();

        var users = await SeedUsersAsync(context, passwordHasher, demoPassword);
        await SeedDemoCustomersAndAccountsAsync(context, users);
        await context.SaveChangesAsync();
    }

    private static async Task<Dictionary<string, User>> SeedUsersAsync(
        BankDbContext context,
        PasswordHasher passwordHasher,
        string demoPassword)
    {
        var result = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);

        foreach (var (username, role) in Users)
        {
            var user = await context.Users.FirstOrDefaultAsync(x => x.Username == username);
            if (user is null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Username = username,
                    PasswordHash = passwordHasher.Hash(demoPassword),
                    Role = role,
                    CreatedDate = DateTime.UtcNow
                };
                context.Users.Add(user);
            }

            result[username] = user;
        }

        return result;
    }

    private static async Task SeedDemoCustomersAndAccountsAsync(
        BankDbContext context,
        IReadOnlyDictionary<string, User> users)
    {
        var customerA = await EnsureCustomerAsync(
            context, "DEMO-CUSTOMER-A", "Ayşe", "Demo", users["customer.a"].Id);
        var customerB = await EnsureCustomerAsync(
            context, "DEMO-CUSTOMER-B", "Burak", "Demo", users["customer.b"].Id);

        var customers = new Dictionary<string, Customer>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = customerA,
            ["B"] = customerB
        };

        foreach (var (accountNumber, owner, currency, openingBalance) in Accounts)
        {
            var exists = await context.Accounts.AnyAsync(x => x.AccountNumber == accountNumber);
            if (exists) continue;

            var account = new Account
            {
                Id = Guid.NewGuid(),
                AccountNumber = accountNumber,
                CustomerId = customers[owner].Id,
                Currency = currency,
                Balance = openingBalance,
                IsDemo = true,
                CreatedDate = DateTime.UtcNow
            };
            context.Accounts.Add(account);

            if (openingBalance > 0)
            {
                context.Transactions.Add(new Transaction
                {
                    Id = Guid.NewGuid(),
                    AccountId = account.Id,
                    Type = TransactionType.Deposit,
                    Amount = openingBalance,
                    BalanceAfter = openingBalance,
                    Description = "DEMO: opening synthetic balance",
                    IsDemo = true,
                    CreatedDate = DateTime.UtcNow
                });
            }
        }
    }

    private static async Task<Customer> EnsureCustomerAsync(
        BankDbContext context,
        string identityNumber,
        string firstName,
        string lastName,
        Guid userId)
    {
        var customer = await context.Customers
            .FirstOrDefaultAsync(x => x.IdentityNumber == identityNumber);

        if (customer is null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                FirstName = firstName,
                LastName = lastName,
                IdentityNumber = identityNumber,
                UserId = userId,
                IsDemo = true,
                CreatedDate = DateTime.UtcNow
            };
            context.Customers.Add(customer);
        }
        else
        {
            if (customer.UserId is null) customer.UserId = userId;
            customer.IsDemo = true;
        }

        return customer;
    }
}
