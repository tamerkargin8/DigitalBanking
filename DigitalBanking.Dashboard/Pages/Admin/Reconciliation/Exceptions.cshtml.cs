using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Reconciliation;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Admin.Reconciliation;

public class ExceptionsModel : SecurePageModel
{
    private readonly ApiClient _api;

    public ExceptionsModel(ApiClient api)
    {
        _api = api;
    }

    public List<ReconciliationExceptionDto> Items { get; set; } = new();
    public ReconciliationExceptionDto? Selected { get; set; }
    public string? Error { get; set; }
    public string? Flash { get; set; }
    public string? Status { get; set; }
    [BindProperty] public string? ResolutionNote { get; set; }
    [BindProperty] public string? RejectReason { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid? id = null, string? status = null, string? flash = null, CancellationToken cancellationToken = default)
    {
        var approver = RequireApprover();
        if (approver is not null) return approver;
        Flash = flash;
        Status = status;

        if (id is not null)
        {
            var detail = await _api.GetAsync<ReconciliationExceptionDto>(Token!, $"/api/reconciliation/exceptions/{id}", cancellationToken);
            if (detail.Unauthorized) return SessionExpired();
            if (!detail.Success) { Error = detail.Error; return Page(); }
            Selected = detail.Data;
        }

        var query = "/api/reconciliation/exceptions?pageSize=50";
        if (!string.IsNullOrWhiteSpace(status)) query += $"&status={Uri.EscapeDataString(status)}";
        var result = await _api.GetAsync<PaginatedResponse<ReconciliationExceptionDto>>(Token!, query, cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) { Error = result.Error; return Page(); }
        Items = result.Data?.Items ?? new();
        return Page();
    }

    public Task<IActionResult> OnPostReviewAsync(Guid id, CancellationToken cancellationToken) =>
        TransitionAsync(id, "review", null, "İstisna incelemeye alındı.", cancellationToken);

    public Task<IActionResult> OnPostApproveAsync(Guid id, CancellationToken cancellationToken) =>
        TransitionAsync(id, "approve", null, "İstisna kapatıldı; bakiye düzeltmesi yapılmadı.", cancellationToken);

    public Task<IActionResult> OnPostReopenAsync(Guid id, CancellationToken cancellationToken) =>
        TransitionAsync(id, "reopen", new { reason = "Owner yeniden açtı" }, "İstisna yeniden açıldı.", cancellationToken);

    public async Task<IActionResult> OnPostProposeAsync(Guid id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ResolutionNote) || ResolutionNote.Trim().Length < 5)
            return RedirectToPage(new { id, flash = "Çözüm gerekçesi en az 5 karakter olmalıdır." });
        return await TransitionAsync(id, "propose", new { resolutionNote = ResolutionNote.Trim() }, "Çözüm önerildi.", cancellationToken);
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(RejectReason) || RejectReason.Trim().Length < 3)
            return RedirectToPage(new { id, flash = "Red gerekçesi zorunludur." });
        return await TransitionAsync(id, "reject", new { reason = RejectReason.Trim() }, "İstisna reddedildi.", cancellationToken);
    }

    private async Task<IActionResult> TransitionAsync(Guid id, string action, object? body, string success, CancellationToken cancellationToken)
    {
        var approver = RequireApprover();
        if (approver is not null) return approver;
        var result = await _api.PostAsync<ReconciliationExceptionDto>(Token!, $"/api/reconciliation/exceptions/{id}/{action}",
            body, NewIdempotencyKey(), cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        return RedirectToPage(new { id, flash = result.Success ? success : result.Error });
    }
}
