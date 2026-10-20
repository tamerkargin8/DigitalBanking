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
    public DbSet<PaymentTimelineEvent> PaymentTimelineEvents => Set<PaymentTimelineEvent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<ReconciliationBatch> ReconciliationBatches => Set<ReconciliationBatch>();
    public DbSet<ReconciliationRow> ReconciliationRows => Set<ReconciliationRow>();
    public DbSet<ReconciliationException> ReconciliationExceptions => Set<ReconciliationException>();
    public DbSet<ApprovalDelegation> ApprovalDelegations => Set<ApprovalDelegation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(x => x.Username).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.FirstName).HasMaxLength(100);
            entity.Property(x => x.LastName).HasMaxLength(100);
            entity.HasIndex(x => x.Username);
            entity.HasIndex(x => x.Email);
            entity.HasIndex(x => new { x.Role, x.Status });
            // A single Owner is a database invariant, not just a service check.
            // The filtered unique index lets Customers/DelegatedApprovers repeat
            // freely while allowing at most one row with Role = Owner (3).
            entity.HasIndex(x => x.Role)
                .IsUnique()
                .HasDatabaseName("IX_Users_Role_OwnerUnique")
                .HasFilter("[Role] = 3");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.IdentityNumber).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.IdentityNumber);
        });

        modelBuilder.Entity<Account>(entity =>
        {
            entity.Property(x => x.Balance).HasPrecision(18, 2);
            entity.Property(x => x.Currency)
                .HasMaxLength(3)
                .IsRequired()
                .HasDefaultValue("TRY");
            entity.HasIndex(x => x.AccountNumber).IsUnique();
            entity.HasIndex(x => x.CustomerId);

            entity.Property(x => x.RowVersion).IsRowVersion();
            if (Database.IsSqlite())
                entity.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.BalanceAfter).HasPrecision(18, 2);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasIndex(x => x.AccountId);
            entity.HasIndex(x => x.CorrelationId);
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
            entity.HasIndex(x => x.CorrelationId);
            entity.HasIndex(x => new { x.Status, x.CreatedAt });
            entity.HasIndex(x => new { x.RequestType, x.Status });

            entity.Property(x => x.RowVersion).IsRowVersion();
            if (Database.IsSqlite())
                entity.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();
        });

        modelBuilder.Entity<PaymentTimelineEvent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActorUsername).HasMaxLength(100);
            entity.Property(x => x.ActorRole).HasMaxLength(50);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasIndex(x => x.PaymentApprovalId);
            entity.HasIndex(x => x.CorrelationId);
            entity.HasIndex(x => x.OccurredAtUtc);
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

        modelBuilder.Entity<ApprovalDelegation>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.MaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.DailyLimit).HasPrecision(18, 2);
            entity.Property(x => x.CreatedByOwnerUsername).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.Operation, x.Currency });
            entity.HasIndex(x => new { x.IsActive, x.ExpiresAt });
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(x => x.RowVersion).IsRowVersion();
            if (Database.IsSqlite())
                entity.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();
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

        modelBuilder.Entity<ReconciliationBatch>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            entity.Property(x => x.FileHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ScopeKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CurrencyScope).HasMaxLength(3);
            entity.Property(x => x.ImportedByUsername).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.FileHash, x.ScopeKey }).IsUnique();
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => new { x.Status, x.CreatedAt });
            entity.HasMany(x => x.Rows)
                .WithOne(x => x.Batch)
                .HasForeignKey(x => x.BatchId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Exceptions)
                .WithOne(x => x.Batch)
                .HasForeignKey(x => x.BatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReconciliationRow>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalReference).HasMaxLength(100).IsRequired();
            entity.Property(x => x.InternalReference).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.ValidationError).HasMaxLength(500);
            entity.Property(x => x.SystemCurrency).HasMaxLength(3);
            entity.Property(x => x.SystemAmount).HasPrecision(18, 2);
            entity.Property(x => x.DifferenceAmount).HasPrecision(18, 2);
            entity.HasIndex(x => x.BatchId);
            entity.HasIndex(x => new { x.BatchId, x.ExternalReference, x.Direction });
            entity.HasIndex(x => x.MatchStatus);
        });

        modelBuilder.Entity<ReconciliationException>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalReference).HasMaxLength(100).IsRequired();
            entity.Property(x => x.InternalReference).HasMaxLength(100);
            entity.Property(x => x.Description).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.AssignedToUsername).HasMaxLength(100);
            entity.Property(x => x.ResolutionNote).HasMaxLength(1000);
            entity.Property(x => x.ProposedByUsername).HasMaxLength(100);
            entity.Property(x => x.ReviewedByUsername).HasMaxLength(100);
            entity.Property(x => x.RejectionReason).HasMaxLength(500);
            entity.HasIndex(x => x.BatchId);
            entity.HasIndex(x => new { x.Status, x.UpdatedAt });
            entity.HasOne(x => x.Row)
                .WithMany(x => x.Exceptions)
                .HasForeignKey(x => x.RowId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(x => x.RowVersion).IsRowVersion();
            if (Database.IsSqlite())
                entity.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();
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

        foreach (var entry in ChangeTracker.Entries<ReconciliationException>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified))
            entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();

        foreach (var entry in ChangeTracker.Entries<ApprovalDelegation>()
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
