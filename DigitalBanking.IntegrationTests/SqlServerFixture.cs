using AutoMapper;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.Mappings;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using DigitalBanking.Infrastructure.Security;
using DigitalBanking.Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DigitalBanking.IntegrationTests;

/// <summary>
/// Real SQL Server only; skipped unless an operator supplies a disposable server
/// connection and explicitly allows the test to create/drop its own random DB.
/// The supplied connection is never printed and an existing database is never
/// opened or dropped.
/// </summary>
public sealed class SqlServerDatabaseFixture : IAsyncLifetime
{
    public const string ConnectionEnv = "DIGITALBANKING_SQLSERVER_CONNECTION";
    public const string AllowDestructiveEnv = "DIGITALBANKING_SQLSERVER_ALLOW_DESTRUCTIVE";
    private const string Prefix = "DigitalBanking_IT_";

    private string? _databaseName;
    private SqlConnectionStringBuilder? _masterBuilder;
    private bool _initialized;

    public bool Enabled =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionEnv))
        && string.Equals(Environment.GetEnvironmentVariable(AllowDestructiveEnv), "1", StringComparison.Ordinal);

    public string ConnectionString { get; private set; } = string.Empty;
    public DbContextOptions<BankDbContext> Options { get; private set; } = null!;
    public IMapper Mapper { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        if (!Enabled) return;

        var source = Environment.GetEnvironmentVariable(ConnectionEnv)!;
        _masterBuilder = new SqlConnectionStringBuilder(source)
        {
            InitialCatalog = "master",
            TrustServerCertificate = true,
            Encrypt = true
        };
        _databaseName = Prefix + Guid.NewGuid().ToString("N");
        if (!IsSafeDatabaseName(_databaseName))
            throw new InvalidOperationException("Refusing to create a test database with an unexpected name.");

        await using var connection = new SqlConnection(_masterBuilder.ConnectionString);
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"CREATE DATABASE [{_databaseName}];";
            await command.ExecuteNonQueryAsync();
        }

        var dbBuilder = new SqlConnectionStringBuilder(_masterBuilder.ConnectionString)
        {
            InitialCatalog = _databaseName
        };
        ConnectionString = dbBuilder.ConnectionString;
        Options = new DbContextOptionsBuilder<BankDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        // Mark created now so a failed migration still drops only this random DB.
        _initialized = true;

        await using var context = NewContext();
        await context.Database.MigrateAsync();
        Mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
    }

    public async Task DisposeAsync()
    {
        if (!_initialized || _databaseName is null || _masterBuilder is null) return;
        if (!IsSafeDatabaseName(_databaseName)) return;

        await using var connection = new SqlConnection(_masterBuilder.ConnectionString);
        await connection.OpenAsync();
        await using (var singleUser = connection.CreateCommand())
        {
            singleUser.CommandText = $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;";
            await singleUser.ExecuteNonQueryAsync();
        }
        await using (var drop = connection.CreateCommand())
        {
            drop.CommandText = $"DROP DATABASE [{_databaseName}];";
            await drop.ExecuteNonQueryAsync();
        }
    }

    public BankDbContext NewContext() => new(Options);

    public PaymentApprovalService NewApprovalService(BankDbContext context) =>
        new(context, Mapper, new AuditService(context), new CustomerAccessService(context),
            new ApprovalAuthorizationService(context));

    public ReconciliationService NewReconciliationService(BankDbContext context) =>
        new(context, new AuditService(context), new ApprovalAuthorizationService(context));

    public async Task<(User User, ActorContext Actor)> AddActorAsync(
        BankDbContext context, UserRole role, UserStatus status = UserStatus.Active)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = $"it-{role}-{Guid.NewGuid():N}"[..42],
            Email = $"it-{Guid.NewGuid():N}@demo.invalid",
            FirstName = "Integration",
            LastName = "Test",
            PasswordHash = "synthetic",
            Role = role,
            Status = status,
            CreatedDate = DateTime.UtcNow,
            ActivatedAt = status == UserStatus.Active ? DateTime.UtcNow : null
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return (user, new ActorContext(user.Id, user.Username, user.Role));
    }

    public async Task<Customer> AddCustomerAsync(BankDbContext context, Guid? userId = null)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = "Integration",
            LastName = "Customer",
            IdentityNumber = "IT-" + Guid.NewGuid().ToString("N")[..12],
            UserId = userId,
            CreatedDate = DateTime.UtcNow
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    public async Task<Account> AddAccountAsync(
        BankDbContext context, Guid customerId, decimal balance = 0m, string currency = "TRY",
        string name = "Vadesiz Hesap", AccountType type = AccountType.Checking)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            AccountNumber = "IT" + Guid.NewGuid().ToString("N")[..14],
            AccountName = name,
            AccountType = type,
            Balance = balance,
            Currency = currency,
            CustomerId = customerId,
            CreatedDate = DateTime.UtcNow
        };
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        return account;
    }

    public async Task<ActorContext> AddDelegationAsync(
        BankDbContext context, Guid userId, ApprovalOperation operation, string? currency, decimal? max, Guid ownerId)
    {
        context.ApprovalDelegations.Add(new ApprovalDelegation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Operation = operation,
            Currency = currency,
            MaxAmount = max,
            IsActive = true,
            CreatedByOwnerId = ownerId,
            CreatedByOwnerUsername = "integration-owner",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return new ActorContext(Guid.Empty, "delegation", UserRole.DelegatedApprover);
    }

    private static bool IsSafeDatabaseName(string name) =>
        name.Length <= 128
        && name.StartsWith(Prefix, StringComparison.Ordinal)
        && name.Skip(Prefix.Length).All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        var connection = Environment.GetEnvironmentVariable(SqlServerDatabaseFixture.ConnectionEnv);
        var allow = Environment.GetEnvironmentVariable(SqlServerDatabaseFixture.AllowDestructiveEnv);
        if (string.IsNullOrWhiteSpace(connection) || !string.Equals(allow, "1", StringComparison.Ordinal))
            Skip = "Real SQL Server integration not configured. Set DIGITALBANKING_SQLSERVER_CONNECTION and DIGITALBANKING_SQLSERVER_ALLOW_DESTRUCTIVE=1 (disposable server only).";
    }
}

[CollectionDefinition("SqlServerIntegration")]
public sealed class SqlServerIntegrationCollection : ICollectionFixture<SqlServerDatabaseFixture>;
