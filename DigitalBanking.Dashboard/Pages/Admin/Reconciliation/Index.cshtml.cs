using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Reconciliation;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Admin.Reconciliation;

public class IndexModel : SecurePageModel
{
    private readonly ApiClient _api;

    public IndexModel(ApiClient api)
    {
        _api = api;
    }

    public List<ReconciliationBatchSummaryDto> Items { get; set; } = new();
    public string? Error { get; set; }
    public string? Flash { get; set; }
    [BindProperty] public IFormFile? CsvFile { get; set; }
    [BindProperty] public string? Currency { get; set; }
    [BindProperty] public string? Direction { get; set; }
    [BindProperty] public DateTime? From { get; set; }
    [BindProperty] public DateTime? To { get; set; }

    public async Task<IActionResult> OnGetAsync(string? flash = null, CancellationToken cancellationToken = default)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;
        Flash = flash;
        var result = await _api.GetAsync<PaginatedResponse<ReconciliationBatchSummaryDto>>(Token!, "/api/reconciliation/batches?pageSize=50", cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) { Error = result.Error; return Page(); }
        Items = result.Data?.Items ?? new();
        return Page();
    }

    public async Task<IActionResult> OnPostUploadAsync(CancellationToken cancellationToken)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;
        if (CsvFile is null || CsvFile.Length == 0)
        {
            Error = "CSV dosyası seçin.";
            return await OnGetAsync(null, cancellationToken);
        }

        using var stream = new MemoryStream();
        await CsvFile.CopyToAsync(stream, cancellationToken);
        var query = $"/api/reconciliation/preview?currency={Uri.EscapeDataString(Currency ?? "")}" +
                    $"&direction={Uri.EscapeDataString(Direction ?? "")}" +
                    (From is null ? "" : $"&from={From:yyyy-MM-dd}") +
                    (To is null ? "" : $"&to={To:yyyy-MM-dd}");

        var result = await _api.PostFileAsync<ReconciliationPreviewDto>(
            Token!, query, stream.ToArray(), CsvFile.FileName, cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) { Error = result.Error; return await OnGetAsync(null, cancellationToken); }

        return RedirectToPage("Details", new { id = result.Data!.Batch.Id });
    }
}
