using DigitalBanking.Application.Common;
using DigitalBanking.Domain.Enums;

namespace DigitalBanking.Application.Abstractions.Services;

public sealed record ApprovalPermission(bool Allowed, string Reason, Guid? DelegationId = null)
{
    public static ApprovalPermission Allow(Guid? delegationId = null) => new(true, string.Empty, delegationId);
    public static ApprovalPermission Deny(string reason) => new(false, reason);
}

/// <summary>
/// DB-backed authorization. The JWT role is only a hint; every critical check
/// re-reads the current user status, role and delegation so revocation/expiry is
/// immediately effective even for an old token or an already-open screen.
/// </summary>
public interface IApprovalAuthorizationService
{
    Task<ActorContext> RequireActiveActorAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsOwnerAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApprovalPermission> CheckAsync(
        Guid userId,
        ApprovalOperation operation,
        string? currency,
        decimal? amount,
        CancellationToken cancellationToken = default);
}
