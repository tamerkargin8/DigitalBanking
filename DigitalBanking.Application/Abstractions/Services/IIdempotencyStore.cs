using DigitalBanking.Domain.Entities;

namespace DigitalBanking.Application.Abstractions.Services;

/// <summary>
/// Persistence boundary for the Idempotency-Key filter. Implementations must make
/// the (Endpoint, Key) reservation atomic.
/// </summary>
public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> FindAsync(string endpoint, string key, CancellationToken cancellationToken = default);

    /// <summary>Returns false when another request already reserved the same key.</summary>
    Task<bool> TryReserveAsync(IdempotencyRecord record, CancellationToken cancellationToken = default);

    Task CompleteAsync(string endpoint, string key, int statusCode, string? responseBody, CancellationToken cancellationToken = default);

    Task RemoveAsync(string endpoint, string key, CancellationToken cancellationToken = default);
}
