using DigitalBanking.Application.DTOs.Audit;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Admin.Audit;

public class IndexModel : SecurePageModel
{
    private readonly ApiClient _api;

    public IndexModel(ApiClient api)
    {
        _api = api;
    }

    public List<AuditLogDto> Items { get; set; } = new();
    public int PageNumber { get; set; } = 1;
    public int TotalPages { get; set; }
    public string? Error { get; set; }
    public string? ActionFilter { get; set; }
    public string? Username { get; set; }
    public string? From { get; set; }
    public string? To { get; set; }

    public async Task<IActionResult> OnGetAsync(
        int page = 1, string? action = null, string? username = null,
        string? from = null, string? to = null, CancellationToken cancellationToken = default)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;
        PageNumber = Math.Max(page, 1);
        ActionFilter = action;
        Username = username;
        From = from;
        To = to;

        var query = $"/api/audit?page={PageNumber}&pageSize=30";
        if (!string.IsNullOrWhiteSpace(action)) query += $"&action={Uri.EscapeDataString(action)}";
        if (!string.IsNullOrWhiteSpace(username)) query += $"&username={Uri.EscapeDataString(username)}";
        if (!string.IsNullOrWhiteSpace(from)) query += $"&from={Uri.EscapeDataString(from)}";
        if (!string.IsNullOrWhiteSpace(to)) query += $"&to={Uri.EscapeDataString(to)}";

        var result = await _api.GetAsync<PaginatedResponse<AuditLogDto>>(Token!, query, cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) { Error = result.Error; return Page(); }
        Items = result.Data?.Items ?? new();
        TotalPages = result.Data?.TotalPages ?? 0;
        return Page();
    }
}
