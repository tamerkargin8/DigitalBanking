namespace DigitalBanking.Application.Abstractions.Services;

/// <summary>
/// Injectable UTC clock for deterministic SLA/age tests. Production uses the
/// system clock; tests use a fixed instant.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
