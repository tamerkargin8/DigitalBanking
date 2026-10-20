using DigitalBanking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Infrastructure.Persistence;

public class BankDbContext : DbContext
{
    public BankDbContext(DbContextOptions<BankDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<User> Users => Set<User>();
    public DbSet<PaymentApproval> PaymentApprovals => Set<PaymentApproval>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>(entity =>
        {
            entity.Property(x => x.Balance).HasPrecision(18, 2);
            entity.Property(x => x.Currency)
                .HasMaxLength(3)
                .IsRequired()
                .HasDefaultValue("TRY");
            entity.HasIndex(x => x.AccountNumber).IsUnique();

            entity.Property(x => x.RowVersion).IsRowVersion();
            if (Database.IsSqlite())
                entity.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.BalanceAfter).HasPrecision(18, 2);
        });

        modelBuilder.Entity<PaymentApproval>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(x => x.Currency)
                .HasMaxLength(3)
                .IsRequired();

            entity.Property(x => x.Description).HasMaxLength(250);
            entity.Property(x => x.ApprovalReason).HasMaxLength(500);
            entity.Property(x => x.RejectionReason).HasMaxLength(500);
            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.RiskLevel).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();

            entity.Property(x => x.RowVersion).IsRowVersion();
            if (Database.IsSqlite())
                entity.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();

            entity.HasIndex(x => new { x.Status, x.CreatedAt });
        });

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Endpoint).HasMaxLength(200).IsRequired();
            entity.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => new { x.Endpoint, x.Key }).IsUnique();
            entity.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(100);
            entity.Property(x => x.Username).HasMaxLength(100);
            entity.HasIndex(x => x.CreatedDate);
        });
    }

    private void RefreshLocalConcurrencyTokens()
    {
        if (!Database.IsSqlite()) return;

        foreach (var entry in ChangeTracker.Entries<Account>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified))
            entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();

        foreach (var entry in ChangeTracker.Entries<PaymentApproval>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified))
            entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RefreshLocalConcurrencyTokens();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RefreshLocalConcurrencyTokens();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
