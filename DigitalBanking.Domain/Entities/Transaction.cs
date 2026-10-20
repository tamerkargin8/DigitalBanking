using DigitalBanking.Domain.Enums;

namespace DigitalBanking.Domain.Entities;

public class Transaction
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public TransactionType Type { get; set; }

    public decimal Amount { get; set; }

    public decimal BalanceAfter { get; set; }

    public string? Description { get; set; }

    /// <summary>Links the ledger row to the payment approval/timeline correlation.</summary>
    public Guid? CorrelationId { get; set; }

    /// <summary>Marks deterministic synthetic demo data for scoped reset.</summary>
    public bool IsDemo { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
