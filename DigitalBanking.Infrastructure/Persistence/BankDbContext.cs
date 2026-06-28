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

    public DbSet<PaymentApproval> PaymentApprovals {  get; set; }



    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>()
            .Property(a => a.Balance)
            .HasPrecision(18, 2); // para için standart: 18 toplam basamak, 2 küsurat

        modelBuilder.Entity<Transaction>()
            .Property(t => t.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Transaction>()
            .Property(t => t.BalanceAfter)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Account>()
            .HasIndex(a => a.AccountNumber)
            .IsUnique();
        
        modelBuilder.Entity<Account>()
            .Property(a => a.RowVersion)
            .IsRowVersion();

        modelBuilder.Entity<Account>(entity =>
        {
            entity.Property(x => x.Balance)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.Currency)
                .HasMaxLength(3)
                .IsRequired()
                .HasDefaultValue("TRY");
        });

        modelBuilder.Entity<PaymentApproval>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Amount)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(x => x.Currency)
                .HasMaxLength(3)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasMaxLength(250);

            entity.Property(x => x.ApprovalReason)
                .HasMaxLength(500);

            entity.Property(x => x.RejectionReason)
                .HasMaxLength(500);

            entity.Property(x => x.Status)
                .IsRequired();

            entity.Property(x => x.RiskLevel)
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .IsRequired();
        });

    }
}
