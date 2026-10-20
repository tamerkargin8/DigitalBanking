using DigitalBanking.Application.DTOs.Demo;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Admin.Demo;

public class IndexModel : SecurePageModel
{
    private readonly ApiClient _api;

    public IndexModel(ApiClient api)
    {
        _api = api;
    }

    public List<DemoScenarioDto> Scenarios { get; set; } = new();
    public string? Error { get; set; }
    public string? Flash { get; set; }

    public async Task<IActionResult> OnGetAsync(string? flash = null, CancellationToken cancellationToken = default)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;
        Flash = flash;
        var result = await _api.GetAsync<List<DemoScenarioDto>>(Token!, "/api/demo/scenarios", cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) { Error = result.Error; return Page(); }
        Scenarios = result.Data ?? new();
        return Page();
    }

    public async Task<IActionResult> OnPostSeedAsync(CancellationToken cancellationToken)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;
        var result = await _api.PostAsync<DemoSeedResultDto>(Token!, "/api/demo/seed", null, NewIdempotencyKey(), cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        return RedirectToPage(new { flash = result.Success ? (result.Data?.AlreadySeeded == true ? "Senaryolar zaten seed edilmiş." : "Demo senaryoları oluşturuldu.") : result.Error });
    }

    public async Task<IActionResult> OnPostResetAsync(CancellationToken cancellationToken)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;
        var result = await _api.PostAsync<DemoResetResultDto>(Token!, "/api/demo/reset", null, NewIdempotencyKey(), cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        return RedirectToPage(new { flash = result.Success ? result.Data?.Message : result.Error });
    }
}
