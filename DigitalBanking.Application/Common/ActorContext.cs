using DigitalBanking.Domain.Enums;

namespace DigitalBanking.Application.Common;

/// <summary>
/// Authenticated caller identity derived exclusively from validated JWT claims.
/// Services receive this value instead of trusting user ids supplied in request bodies.
/// </summary>
public sealed record ActorContext(Guid UserId, string Username, UserRole Role)
{
    public bool IsAdmin => Role == UserRole.Admin;
}
