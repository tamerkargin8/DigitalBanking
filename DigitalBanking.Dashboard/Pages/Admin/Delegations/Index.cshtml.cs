using DigitalBanking.Application.DTOs.Delegations;
using DigitalBanking.Application.DTOs.Identity;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Admin.Delegations;

public class IndexModel : SecurePageModel
{
    private readonly ApiClient _api;

    public IndexModel(ApiClient api)
    {
        _api = api;
    }

    public List<DelegationDto> Items { get; set; } = new();
    public List<UserAdminDto> Approvers { get; set; } = new();
    public string? Error { get; set; }
    public string? Flash { get; set; }

    [BindProperty] public Guid UserId { get; set; }
    [BindProperty] public string Operation { get; set; } = "Transfer";
    [BindProperty] public string? Currency { get; set; } = "TRY";
    [BindProperty] public decimal? MaxAmount { get; set; } = 100_000m;
    [BindProperty] public decimal? DailyLimit { get; set; }
    [BindProperty] public DateTime? ExpiresAt { get; set; }

    public async Task<IActionResult> OnGetAsync(string? flash = null, CancellationToken cancellationToken = default)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;
        Flash = flash;

        var users = await _api.GetAsync<List<UserAdminDto>>(Token!, "/api/users?role=DelegatedApprover&status=Active", cancellationToken);
        if (users.Unauthorized) return SessionExpired();
        if (users.Success) Approvers = users.Data ?? new();
        else Error = users.Error;

        var result = await _api.GetAsync<List<DelegationDto>>(Token!, "/api/delegations", cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) { Error = result.Error; return Page(); }
        Items = result.Data ?? new();
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;

        var result = await _api.PostAsync<DelegationDto>(Token!, "/api/delegations", new
        {
            userId = UserId,
            operation = Operation,
            currency = Operation == "ReconciliationException" ? null : Currency,
            maxAmount = Operation == "ReconciliationException" ? (decimal?)null : MaxAmount,
            dailyLimit = Operation == "ReconciliationException" ? (decimal?)null : DailyLimit,
            expiresAt = DashboardUi.LocalInputToUtc(ExpiresAt)
        }, NewIdempotencyKey(), cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        return RedirectToPage(new { flash = result.Success ? "Yetki devri oluşturuldu." : result.Error });
    }

    public async Task<IActionResult> OnPostUpdateAsync(Guid id, decimal? maxAmount, decimal? dailyLimit, bool? isActive, DateTime? expiresAt, CancellationToken cancellationToken)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;

        var result = await _api.PutAsync<DelegationDto>(Token!, $"/api/delegations/{id}", new
        {
            maxAmount,
            dailyLimit,
            isActive,
            expiresAt
        }, NewIdempotencyKey(), cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        return RedirectToPage(new { flash = result.Success ? "Yetki devri güncellendi." : result.Error });
    }

    public async Task<IActionResult> OnPostRevokeAsync(Guid id, CancellationToken cancellationToken)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;
        var result = await _api.PostAsync<object>(Token!, $"/api/delegations/{id}/deactivate", null, NewIdempotencyKey(), cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        return RedirectToPage(new { flash = result.Success ? "Yetki hemen kapatıldı." : result.Error });
    }
}
