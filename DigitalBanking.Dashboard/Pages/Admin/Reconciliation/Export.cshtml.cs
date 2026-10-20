using System.Text;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Admin.Reconciliation;

public class ExportModel : SecurePageModel
{
    private readonly ApiClient _api;

    public ExportModel(ApiClient api)
    {
        _api = api;
    }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;
        var result = await _api.GetTextAsync(Token!, $"/api/reconciliation/batches/{id}/export", cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success || result.Data is null) return BadRequest(result.Error ?? "Dosya indirilemedi.");
        return File(new UTF8Encoding(true).GetBytes(result.Data), "text/csv; charset=utf-8", $"reconciliation-{id:N}.csv");
    }
}
