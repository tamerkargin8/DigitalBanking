using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Accounting;
using DigitalBanking.Application.DTOs.Common;

namespace DigitalBanking.Application.Abstractions.Services;

/// <summary>
/// Read-only double-entry accounting plus the explicit Owner baseline. There is no
/// endpoint that edits or deletes a posted journal; corrections must be new,
/// linked reversal entries.
/// </summary>
public interface IAccountingService
{
    Task<TrialBalanceDto> GetTrialBalanceAsync(
        string? currency, ActorContext actor, CancellationToken cancellationToken = default);

    Task<PaginatedResponse<JournalEntrySummaryDto>> GetEntriesAsync(
        int page, int pageSize, string? currency, Guid? accountId, string? sourceOperation,
        ActorContext actor, CancellationToken cancellationToken = default);

    Task<JournalEntryDetailDto> GetEntryAsync(
        Guid id, ActorContext actor, CancellationToken cancellationToken = default);

    Task<BaselinePreviewDto> PreviewBaselineAsync(
        ActorContext actor, CancellationToken cancellationToken = default);

    Task<BaselinePostResultDto> PostBaselineAsync(
        ActorContext actor, CancellationToken cancellationToken = default);
}
