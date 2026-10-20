using AutoMapper;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.Mappings;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DigitalBanking.Tests;

/// <summary>
/// Shared SQLite (in-memory) test database. SQL Server-specific behavior is not
/// proven by these tests; see docs/VERIFICATION.md for that limitation.
/// </summary>
public sealed class TestDb : IAsyncDisposable
{
    private TestDb(SqliteConnection connection, DbContextOptions<BankDbContext> options)
    {
        Connection = connection;
        Options = options;
    }

    public SqliteConnection Connection { get; }
    public DbContextOptions<BankDbContext> Options { get; }

    public static async Task<TestDb> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BankDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new BankDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new TestDb(connection, options);
    }

    public BankDbContext NewContext() => new(Options);

    public async ValueTask DisposeAsync() => await Connection.DisposeAsync();
}

public static class TestSupport
{
    public static async Task<(User User, ActorContext Actor)> AddActorAsync(
        BankDbContext context,
        string username,
        UserRole role,
        UserStatus status = UserStatus.Active,
        string passwordHash = "synthetic-hash")
    {
        var user = await AddUserAsync(context, username, passwordHash, role, status);
        return (user, new ActorContext(user.Id, user.Username, user.Role));
    }

    public static IMapper CreateMapper()
    {
        var configuration = new MapperConfiguration(
            cfg => cfg.AddProfile<MappingProfile>(),
            NullLoggerFactory.Instance);
        return configuration.CreateMapper();
    }

    public static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Banking:DirectTransferLimit"] = "50000",
                ["Jwt:Issuer"] = "digitalbanking-tests",
                ["Jwt:Audience"] = "digitalbanking-tests-client",
                ["Jwt:SecretKey"] = "synthetic-test-signing-key-that-is-long-enough-0123456789"
            })
            .Build();

    public static async Task<Customer> AddCustomerAsync(
        BankDbContext context,
        string firstName = "Synthetic",
        string lastName = "Customer",
        Guid? userId = null)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            IdentityNumber = "SYNTHETIC-" + Guid.NewGuid().ToString("N")[..8],
            UserId = userId,
            CreatedDate = DateTime.UtcNow
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    public static async Task<Account> AddAccountAsync(
        BankDbContext context,
        Guid customerId,
        decimal balance = 0m,
        string currency = "TRY")
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            AccountNumber = "ACC" + Guid.NewGuid().ToString("N")[..12],
            CustomerId = customerId,
            Balance = balance,
            Currency = currency,
            CreatedDate = DateTime.UtcNow
        };
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        return account;
    }

    public static async Task<User> AddUserAsync(
        BankDbContext context,
        string username,
        string passwordHash,
        UserRole role,
        UserStatus status = UserStatus.Active)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = username + "@test.invalid",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = passwordHash,
            Role = role,
            Status = status,
            CreatedDate = DateTime.UtcNow,
            ActivatedAt = status == UserStatus.Active ? DateTime.UtcNow : null
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
