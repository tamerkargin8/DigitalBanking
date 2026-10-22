using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Operations;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Admin.Operations;

public class IndexModel : SecurePageModel
{
    private readonly ApiClient _api;

    public IndexModel(ApiClient api)
    {
        _api = api;
    }

    public OperationsSummaryDto? Summary { get; set; }
    public PaginatedResponse<OperationsApprovalRowDto> Approvals { get; set; } = new();
    public PaginatedResponse<OperationsReconciliationItemDto> Reconciliation { get; set; } = new();
    public string? Error { get; set; }
    public string? Flash { get; set; }
    public bool IsOwner => IsAdmin;

    [BindProperty(SupportsGet = true)] public string Status { get; set; } = "Pending";
    [BindProperty(SupportsGet = true)] public string? RequestType { get; set; }
    [BindProperty(SupportsGet = true)] public string? Currency { get; set; }
    [BindProperty(SupportsGet = true)] public string? Age { get; set; }
    [BindProperty(SupportsGet = true)] public string? Assignment { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public string? ReconCurrency { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReconFilter { get; set; } = "open";
    [BindProperty(SupportsGet = true)] public int ReconPage { get; set; } = 1;

    public async Task<IActionResult> OnGetAsync(string? flash = null, CancellationToken cancellationToken = default)
    {
        var approver = RequireApprover();
        if (approver is not null) return approver;
        Flash = flash;

        var summary = await _api.GetAsync<OperationsSummaryDto>(Token!, "/api/operations/summary", cancellationToken);
        if (summary.Unauthorized) return SessionExpired();
        if (!summary.Success) { Error = summary.Error; return Page(); }
        Summary = summary.Data;

        var query = $"/api/operations/approvals?page={Math.Max(PageNumber, 1)}&pageSize=25" +
                    $"&status={Uri.EscapeDataString(Status ?? "Pending")}" +
                    (string.IsNullOrWhiteSpace(RequestType) ? "" : $"&requestType={Uri.EscapeDataString(RequestType)}") +
                    (string.IsNullOrWhiteSpace(Currency) ? "" : $"&currency={Uri.EscapeDataString(Currency)}") +
                    (string.IsNullOrWhiteSpace(Age) ? "" : $"&age={Uri.EscapeDataString(Age)}") +
                    (string.IsNullOrWhiteSpace(Assignment) ? "" : $"&assignment={Uri.EscapeDataString(Assignment)}");
        var approvals = await _api.GetAsync<PaginatedResponse<OperationsApprovalRowDto>>(Token!, query, cancellationToken);
        if (approvals.Unauthorized) return SessionExpired();
        if (!approvals.Success) { Error = approvals.Error; return Page(); }
        Approvals = approvals.Data ?? new PaginatedResponse<OperationsApprovalRowDto>();

        if (Summary?.CanViewReconciliation == true)
        {
            var reconQuery = $"/api/operations/reconciliation?page={Math.Max(ReconPage, 1)}&pageSize=25" +
                             (string.IsNullOrWhiteSpace(ReconCurrency) ? "" : $"&currency={Uri.EscapeDataString(ReconCurrency)}") +
                             $"&exceptionFilter={Uri.EscapeDataString(ReconFilter ?? "open")}";
            var recon = await _api.GetAsync<PaginatedResponse<OperationsReconciliationItemDto>>(Token!, reconQuery, cancellationToken);
            if (!recon.Unauthorized && recon.Success)
                Reconciliation = recon.Data ?? new PaginatedResponse<OperationsReconciliationItemDto>();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAssignAsync(
        int approvalId, Guid? assignedToUserId, CancellationToken cancellationToken)
    {
        var approver = RequireApprover();
        if (approver is not null) return approver;

        var result = await _api.PostAsync<OperationsAssignmentResultDto>(
            Token!,
            $"/api/operations/approvals/{approvalId}/assignment",
            new { assignedToUserId },
            NewIdempotencyKey(),
            cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        return RedirectToPage(new
        {
            status = Status,
            requestType = RequestType,
            currency = Currency,
            age = Age,
            assignment = Assignment,
            pageNumber = PageNumber,
            reconCurrency = ReconCurrency,
            reconFilter = ReconFilter,
            reconPage = ReconPage,
            flash = result.Success ? result.Data?.Message : result.Error
        });
    }

    public static string AgeText(int minutes)
    {
        if (minutes < 60) return $"{minutes} dk";
        if (minutes < 1440) return $"{minutes / 60} sa {minutes % 60} dk";
        return $"{minutes / 1440} gün {minutes % 1440 / 60} sa";
    }
}
