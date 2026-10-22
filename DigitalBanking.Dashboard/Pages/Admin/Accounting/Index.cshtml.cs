using DigitalBanking.Application.DTOs.Accounting;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Admin.Accounting;

public class IndexModel : SecurePageModel
{
    private readonly ApiClient _api;

    public IndexModel(ApiClient api)
    {
        _api = api;
    }

    public TrialBalanceDto? TrialBalance { get; set; }
    public PaginatedResponse<JournalEntrySummaryDto> Entries { get; set; } = new();
    public BaselinePreviewDto? Baseline { get; set; }
    public bool IsOwner => IsAdmin;
    public string? Error { get; set; }
    public string? Flash { get; set; }

    [BindProperty(SupportsGet = true)] public string? Currency { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? AccountId { get; set; }
    [BindProperty(SupportsGet = true)] public string? SourceOperation { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;

    public async Task<IActionResult> OnGetAsync(string? flash = null, CancellationToken cancellationToken = default)
    {
        var approver = RequireApprover();
        if (approver is not null) return approver;
        Flash = flash;

        var currencyQuery = string.IsNullOrWhiteSpace(Currency) ? string.Empty : $"currency={Uri.EscapeDataString(Currency)}";
        var trial = await _api.GetAsync<TrialBalanceDto>(
            Token!, $"/api/accounting/trial-balance?{currencyQuery}", cancellationToken);
        if (trial.Unauthorized) return SessionExpired();
        if (!trial.Success) { Error = trial.Error; return Page(); }
        TrialBalance = trial.Data;

        var entriesQuery = $"/api/accounting/journal-entries?page={Math.Max(PageNumber, 1)}&pageSize=25" +
                           (string.IsNullOrWhiteSpace(Currency) ? "" : $"&currency={Uri.EscapeDataString(Currency)}") +
                           (AccountId is null ? "" : $"&accountId={AccountId}") +
                           (string.IsNullOrWhiteSpace(SourceOperation) ? "" : $"&sourceOperation={Uri.EscapeDataString(SourceOperation)}");
        var entries = await _api.GetAsync<PaginatedResponse<JournalEntrySummaryDto>>(Token!, entriesQuery, cancellationToken);
        if (entries.Unauthorized) return SessionExpired();
        if (!entries.Success) { Error = entries.Error; return Page(); }
        Entries = entries.Data ?? new PaginatedResponse<JournalEntrySummaryDto>();

        if (IsAdmin)
        {
            var baseline = await _api.GetAsync<BaselinePreviewDto>(Token!, "/api/accounting/baseline/preview", cancellationToken);
            if (!baseline.Unauthorized && baseline.Success) Baseline = baseline.Data;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostBaselineAsync(CancellationToken cancellationToken)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;

        var result = await _api.PostAsync<BaselinePostResultDto>(
            Token!, "/api/accounting/baseline/post", null, NewIdempotencyKey(), cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        return RedirectToPage(new
        {
            flash = result.Success
                ? $"Baseline üretildi: {result.Data?.CreatedCount ?? 0} kayıt, atlanan {result.Data?.SkippedCount ?? 0}."
                : result.Error
        });
    }
}
