using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DigitalBanking.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c>. It never starts the API host,
/// so migrations can be generated without local secrets or a running database.
/// </summary>
public sealed class BankDbContextFactory : IDesignTimeDbContextFactory<BankDbContext>
{
    public BankDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("Database__Provider") ?? "SqlServer";
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=DigitalBankingDb;Trusted_Connection=True;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<BankDbContext>();
        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            options.UseSqlite(connection);
        else
            options.UseSqlServer(connection);

        return new BankDbContext(options.Options);
    }
}
