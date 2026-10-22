using DigitalBanking.Application.Abstractions.Services;

namespace DigitalBanking.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
