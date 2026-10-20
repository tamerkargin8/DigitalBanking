using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Reconciliation;

namespace DigitalBanking.Application.Abstractions.Services;

/// <summary>
/// Synthetic external-statement reconciliation. All operations are Admin-only;
/// preview is persisted, import is idempotent per batch, and exception closure is
/// operational only (never adjusts balances).
/// </summary>
public interface IReconciliationService
{
    Task<ReconciliationPreviewDto> PreviewAsync(
        byte[] content,
        string fileName,
        string? currency,
        string? direction,
        DateTime? from,
        DateTime? to,
        bool isDemo,
        ActorContext actor,
        CancellationToken cancellationToken = default);

    Task<ReconciliationBatchDetailDto> ImportAsync(Guid batchId, ActorContext actor, CancellationToken cancellationToken = default);
    Task<PaginatedResponse<ReconciliationBatchSummaryDto>> GetBatchesAsync(
        int page, int pageSize, string? status, string? currency, DateTime? from, DateTime? to,
        CancellationToken cancellationToken = default);
    Task<ReconciliationBatchDetailDto> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<List<ReconciliationRowDto>> GetRowsAsync(Guid batchId, string? matchStatus, CancellationToken cancellationToken = default);
    Task<string> ExportCsvAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<PaginatedResponse<ReconciliationExceptionDto>> GetExceptionsAsync(
        int page, int pageSize, string? status, Guid? batchId,
        CancellationToken cancellationToken = default);
    Task<ReconciliationExceptionDto> GetExceptionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ReconciliationExceptionDto> CreateExceptionAsync(
        CreateReconciliationExceptionRequest request, ActorContext actor, CancellationToken cancellationToken = default);
    Task<ReconciliationExceptionDto> AssignAsync(
        Guid id, Guid? assignedToUserId, ActorContext actor, CancellationToken cancellationToken = default);
    Task<ReconciliationExceptionDto> ReviewAsync(Guid id, ActorContext actor, CancellationToken cancellationToken = default);
    Task<ReconciliationExceptionDto> ProposeAsync(
        Guid id, string resolutionNote, ActorContext actor, CancellationToken cancellationToken = default);
    Task<ReconciliationExceptionDto> ApproveAsync(Guid id, ActorContext actor, CancellationToken cancellationToken = default);
    Task<ReconciliationExceptionDto> RejectAsync(
        Guid id, string reason, ActorContext actor, CancellationToken cancellationToken = default);
    Task<ReconciliationExceptionDto> ReopenAsync(
        Guid id, string? reason, ActorContext actor, CancellationToken cancellationToken = default);
}
