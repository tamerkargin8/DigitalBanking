using DigitalBanking.Application.DTOs.Account;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Admin.Accounts;

public class IndexModel : SecurePageModel
{
    private readonly ApiClient _api;

    public IndexModel(ApiClient api)
    {
        _api = api;
    }

    public List<AccountDto> Items { get; set; } = new();
    public int PageNumber { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public Guid? CustomerId { get; set; }
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(int page = 1, Guid? customerId = null, CancellationToken cancellationToken = default)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;
        PageNumber = Math.Max(page, 1);
        CustomerId = customerId;
        var query = $"/api/accounts?page={PageNumber}&pageSize=20";
        if (customerId is not null) query += $"&customerId={customerId}";

        var result = await _api.GetAsync<PaginatedResponse<AccountDto>>(Token!, query, cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) { Error = result.Error; return Page(); }
        Items = result.Data?.Items ?? new();
        TotalPages = result.Data?.TotalPages ?? 0;
        TotalCount = result.Data?.TotalCount ?? 0;
        return Page();
    }
}
