namespace DigitalBanking.Domain.Entities;

/// <summary>
/// Durable record of a request that supplied an Idempotency-Key header.
/// A row is reserved before the operation runs (StatusCode is null) and completed
/// with the response status/body afterwards. The unique (Endpoint, Key) index
/// makes a concurrent duplicate reservation fail instead of applying money twice.
/// </summary>
public class IdempotencyRecord
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public Guid? UserId { get; set; }

    /// <summary>SHA-256 of the normalized request body. Reusing a key with a different payload is rejected.</summary>
    public string RequestHash { get; set; } = string.Empty;

    /// <summary>Null while the original request is still in progress.</summary>
    public int? StatusCode { get; set; }

    public string? ResponseBody { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
