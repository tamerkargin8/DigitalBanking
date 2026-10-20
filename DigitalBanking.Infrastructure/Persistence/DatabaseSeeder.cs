using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Infrastructure.Persistence;

/// <summary>
/// Deterministic Development seed. No Owner credential is seeded: the user creates
/// the Owner once through the localhost /Setup flow. Demo customers/approvers are
/// synthetic and marked Active; a pending applicant demonstrates activation.
/// </summary>
public static class DatabaseSeeder
{
    private static readonly (string Username, string Email, string First, string Last, UserRole Role, UserStatus Status)[] Users =
    {
        ("customer.a", "customer.a@demo.invalid", "Ayşe", "Demo", UserRole.Customer, UserStatus.Active),
        ("customer.b", "customer.b@demo.invalid", "Burak", "Demo", UserRole.Customer, UserStatus.Active),
        ("approver.100k", "approver.100k@demo.invalid", "Deniz", "Yetkili", UserRole.DelegatedApprover, UserStatus.Active),
        ("approver.low", "approver.low@demo.invalid", "Ece", "Yetkili", UserRole.DelegatedApprover, UserStatus.Active),
        ("applicant.pending", "applicant.pending@demo.invalid", "Yeni", "Üye", UserRole.Customer, UserStatus.PendingActivation)
    };

    private static readonly (string AccountNumber, string Owner, string Currency, decimal OpeningBalance)[] Accounts =
    {
        ("1000000001", "A", "TRY", 250_000m),
        ("1000000002", "A", "USD", 3_000m),
        ("1000000003", "A", "EUR", 1_500m),
        ("2000000001", "B", "TRY", 25_000m),
        ("2000000002", "B", "USD", 800m),
        ("2000000003", "B", "EUR", 400m)
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

        foreach (var definition in Users)
        {
            var user = await context.Users.FirstOrDefaultAsync(x => x.Username == definition.Username);
            if (user is null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Username = definition.Username,
                    Email = definition.Email,
                    FirstName = definition.First,
                    LastName = definition.Last,
                    PasswordHash = passwordHasher.Hash(demoPassword),
                    Role = definition.Role,
                    Status = definition.Status,
                    CreatedDate = DateTime.UtcNow,
                    ActivatedAt = definition.Status == UserStatus.Active ? DateTime.UtcNow : null
                };
                context.Users.Add(user);
            }
            else
            {
                // Demo personas may have been created by an older run; keep them
                // deterministic for the synthetic set only.
                user.Role = definition.Role;
                user.Status = definition.Status;
                user.Email ??= definition.Email;
                if (string.IsNullOrWhiteSpace(user.FirstName)) user.FirstName = definition.First;
                if (string.IsNullOrWhiteSpace(user.LastName)) user.LastName = definition.Last;
            }

            result[definition.Username] = user;
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
