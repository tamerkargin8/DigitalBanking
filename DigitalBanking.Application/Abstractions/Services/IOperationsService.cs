using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Operations;

namespace DigitalBanking.Application.Abstractions.Services;

/// <summary>
/// Read-only operations control centre plus approval assignment. Assignment never
/// grants approval rights or bypasses limits; the live approval check still runs.
/// </summary>
public interface IOperationsService
{
    Task<OperationsSummaryDto> GetSummaryAsync(ActorContext actor, CancellationToken cancellationToken = default);

    Task<PaginatedResponse<OperationsApprovalRowDto>> GetApprovalsAsync(
        int page,
        int pageSize,
        string? status,
        string? requestType,
        string? currency,
        string? age,
        string? assignment,
        ActorContext actor,
        CancellationToken cancellationToken = default);

    Task<PaginatedResponse<OperationsReconciliationItemDto>> GetReconciliationAsync(
        int page,
        int pageSize,
        string? currency,
        string? exceptionFilter,
        ActorContext actor,
        CancellationToken cancellationToken = default);

    Task<OperationsAssignmentResultDto> AssignApprovalAsync(
        int approvalId,
        Guid? assignedToUserId,
        ActorContext actor,
        CancellationToken cancellationToken = default);
}
