namespace DigitalBanking.Application.Abstractions.Services;

public enum LoginAttemptDecision
{
    Allowed = 1,
    Blocked = 2
}

/// <summary>
/// Per-username failed-login throttling. The demo implementation is in-memory and
/// therefore per process; a multi-instance deployment would need a shared store.
/// </summary>
public interface ILoginAttemptLimiter
{
    LoginAttemptDecision Check(string username);
    void RecordFailure(string username);
    void RecordSuccess(string username);
}
