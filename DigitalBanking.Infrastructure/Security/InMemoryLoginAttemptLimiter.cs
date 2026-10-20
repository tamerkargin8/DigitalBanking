using System.Collections.Concurrent;
using DigitalBanking.Application.Abstractions.Services;

namespace DigitalBanking.Infrastructure.Security;

/// <summary>
/// In-memory, per-username failed-login throttle for the local demo.
/// State is per process and is intentionally not distributed; a production
/// deployment should use a shared cache/identity provider lockout instead.
/// </summary>
public sealed class InMemoryLoginAttemptLimiter : ILoginAttemptLimiter
{
    private sealed class AttemptState
    {
        public int Failures;
        public DateTimeOffset FirstFailureUtc;
        public DateTimeOffset? LockedUntilUtc;
    }

    private readonly ConcurrentDictionary<string, AttemptState> _attempts = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _maxFailures;
    private readonly TimeSpan _window;
    private readonly TimeSpan _lockout;

    public InMemoryLoginAttemptLimiter(int maxFailures = 5, TimeSpan? window = null, TimeSpan? lockout = null)
    {
        _maxFailures = maxFailures > 0 ? maxFailures : 5;
        _window = window ?? TimeSpan.FromMinutes(15);
        _lockout = lockout ?? TimeSpan.FromMinutes(5);
    }

    public LoginAttemptDecision Check(string username)
    {
        var key = Normalize(username);
        if (key.Length == 0) return LoginAttemptDecision.Allowed;

        if (!_attempts.TryGetValue(key, out var state))
            return LoginAttemptDecision.Allowed;

        lock (state)
        {
            var now = DateTimeOffset.UtcNow;
            if (state.LockedUntilUtc is { } lockedUntil)
            {
                if (lockedUntil > now) return LoginAttemptDecision.Blocked;
                state.LockedUntilUtc = null;
                state.Failures = 0;
                state.FirstFailureUtc = default;
            }

            if (state.Failures > 0 && now - state.FirstFailureUtc > _window)
            {
                state.Failures = 0;
                state.FirstFailureUtc = default;
            }

            return LoginAttemptDecision.Allowed;
        }
    }

    public void RecordFailure(string username)
    {
        var key = Normalize(username);
        if (key.Length == 0) return;

        var state = _attempts.GetOrAdd(key, _ => new AttemptState());
        lock (state)
        {
            var now = DateTimeOffset.UtcNow;
            if (state.FirstFailureUtc == default || now - state.FirstFailureUtc > _window)
            {
                state.FirstFailureUtc = now;
                state.Failures = 0;
            }

            state.Failures++;
            if (state.Failures >= _maxFailures)
                state.LockedUntilUtc = now + _lockout;
        }
    }

    public void RecordSuccess(string username)
    {
        var key = Normalize(username);
        if (key.Length > 0) _attempts.TryRemove(key, out _);
    }

    private static string Normalize(string username) => (username ?? string.Empty).Trim();
}
