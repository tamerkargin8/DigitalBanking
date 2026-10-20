using DigitalBanking.Domain.Enums;

namespace DigitalBanking.Domain.Entities;

/// <summary>
/// Owner-granted approval authority. Every approval re-reads this row from the
/// database, so revocation/expiry takes effect immediately even with an old JWT.
/// </summary>
public class ApprovalDelegation
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public ApprovalOperation Operation { get; set; }
    public string? Currency { get; set; }

    /// <summary>Per-request cap. Null only for non-financial operations.</summary>
    public decimal? MaxAmount { get; set; }

    /// <summary>Optional same-day cumulative cap (owner-configurable, not implied).</summary>
    public decimal? DailyLimit { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }

    public Guid CreatedByOwnerId { get; set; }
    public string CreatedByOwnerUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
