using DigitalBanking.Application.DTOs.Accounting;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Admin.Accounting;

public class DetailsModel : SecurePageModel
{
    private readonly ApiClient _api;

    public DetailsModel(ApiClient api)
    {
        _api = api;
    }

    public JournalEntryDetailDto? Detail { get; set; }
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var approver = RequireApprover();
        if (approver is not null) return approver;

        var result = await _api.GetAsync<JournalEntryDetailDto>(Token!, $"/api/accounting/journal-entries/{id}", cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) { Error = result.Error; return Page(); }
        Detail = result.Data;
        return Page();
    }
}
