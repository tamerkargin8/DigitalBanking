using DigitalBanking.Application.DTOs.Reconciliation;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Admin.Reconciliation;

public class DetailsModel : SecurePageModel
{
    private readonly ApiClient _api;

    public DetailsModel(ApiClient api)
    {
        _api = api;
    }

    public ReconciliationBatchDetailDto? Detail { get; set; }
    public string? Error { get; set; }
    public string? Flash { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, string? flash = null, CancellationToken cancellationToken = default)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;
        Flash = flash;
        var result = await _api.GetAsync<ReconciliationBatchDetailDto>(Token!, $"/api/reconciliation/batches/{id}", cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) { Error = result.Error; return Page(); }
        Detail = result.Data;
        return Page();
    }

    public async Task<IActionResult> OnPostImportAsync(Guid id, CancellationToken cancellationToken)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;
        var result = await _api.PostAsync<ReconciliationBatchDetailDto>(Token!, $"/api/reconciliation/{id}/import", null, NewIdempotencyKey(), cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        return RedirectToPage(new { id, flash = result.Success ? "Batch içe aktarıldı." : result.Error });
    }

    public async Task<IActionResult> OnPostExceptionAsync(Guid id, Guid rowId, string description, CancellationToken cancellationToken)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;
        var result = await _api.PostAsync<ReconciliationExceptionDto>(Token!, "/api/reconciliation/exceptions",
            new { rowId, description }, NewIdempotencyKey(), cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        return RedirectToPage(new { id, flash = result.Success ? "İstisna açıldı." : result.Error });
    }
}
