using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Infrastructure.Services;

/// <summary>
/// Durable Idempotency-Key store backed by the application database.
/// </summary>
public sealed class IdempotencyStore : IIdempotencyStore
{
    private readonly BankDbContext _context;

    public IdempotencyStore(BankDbContext context)
    {
        _context = context;
    }

    public async Task<IdempotencyRecord?> FindAsync(string endpoint, string key, CancellationToken cancellationToken = default)
    {
        return await _context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Endpoint == endpoint && x.Key == key, cancellationToken);
    }

    public async Task<bool> TryReserveAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
    {
        _context.IdempotencyRecords.Add(record);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // The unique (Endpoint, Key) index lost the race. Detach the rejected
            // entity so the caller can read the winner's state instead.
            _context.Entry(record).State = EntityState.Detached;
            return false;
        }
    }

    public Task CompleteAsync(string endpoint, string key, int statusCode, string? responseBody, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return _context.IdempotencyRecords
            .Where(x => x.Endpoint == endpoint && x.Key == key)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.StatusCode, statusCode)
                .SetProperty(x => x.ResponseBody, responseBody)
                .SetProperty(x => x.CompletedAt, now), cancellationToken);
    }

    public Task RemoveAsync(string endpoint, string key, CancellationToken cancellationToken = default)
    {
        return _context.IdempotencyRecords
            .Where(x => x.Endpoint == endpoint && x.Key == key)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
