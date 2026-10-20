using DigitalBanking.Application.DTOs.Identity;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Admin.Users;

public class IndexModel : SecurePageModel
{
    private readonly ApiClient _api;

    public IndexModel(ApiClient api)
    {
        _api = api;
    }

    public List<UserAdminDto> Items { get; set; } = new();
    public string? Error { get; set; }
    public string? Flash { get; set; }
    public string? Status { get; set; }
    public string? Role { get; set; }

    public async Task<IActionResult> OnGetAsync(string? status = null, string? role = null, string? flash = null, CancellationToken cancellationToken = default)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;
        Status = status;
        Role = role;
        Flash = flash;

        var query = "/api/users";
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(status)) parts.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(role)) parts.Add($"role={Uri.EscapeDataString(role)}");
        if (parts.Count > 0) query += "?" + string.Join("&", parts);

        var result = await _api.GetAsync<List<UserAdminDto>>(Token!, query, cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) { Error = result.Error; return Page(); }
        Items = result.Data ?? new();
        return Page();
    }

    public async Task<IActionResult> OnPostActivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;
        var result = await _api.PostAsync<UserAdminDto>(Token!, $"/api/users/{id}/activate", null, NewIdempotencyKey(), cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        return RedirectToPage(new { flash = result.Success ? "Üyelik aktive edildi." : result.Error });
    }

    public async Task<IActionResult> OnPostDisableAsync(Guid id, CancellationToken cancellationToken)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;
        var result = await _api.PostAsync<UserAdminDto>(Token!, $"/api/users/{id}/disable", null, NewIdempotencyKey(), cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        return RedirectToPage(new { flash = result.Success ? "Üyelik devre dışı bırakıldı." : result.Error });
    }

    public async Task<IActionResult> OnPostRoleAsync(Guid id, string role, CancellationToken cancellationToken)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;
        var result = await _api.PostAsync<UserAdminDto>(Token!, $"/api/users/{id}/role", new { role }, NewIdempotencyKey(), cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        return RedirectToPage(new { flash = result.Success ? "Rol güncellendi." : result.Error });
    }
}
