using DigitalBanking.Application.DTOs.Account;
using DigitalBanking.Application.DTOs.Audit;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Customers;
using DigitalBanking.Application.DTOs.Delegations;
using DigitalBanking.Application.DTOs.Identity;
using DigitalBanking.Application.DTOs.PaymentApprovals;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Admin;

public class OverviewModel : SecurePageModel
{
    private readonly ApiClient _api;

    public OverviewModel(ApiClient api)
    {
        _api = api;
    }

    public List<PaymentApprovalResponse> Pending { get; set; } = new();
    public List<UserAdminDto> PendingMembers { get; set; } = new();
    public int CustomerCount { get; set; }
    public int AccountCount { get; set; }
    public List<AccountDto> Accounts { get; set; } = new();
    public List<AuditLogDto> RecentAudit { get; set; } = new();
    public List<DelegationDto> Delegations { get; set; } = new();
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;
        var token = Token!;

        var pending = await _api.GetAsync<List<PaymentApprovalResponse>>(token, "/api/paymentapprovals/queue?status=Pending", cancellationToken);
        if (pending.Unauthorized) return SessionExpired();
        if (!pending.Success) { Error = pending.Error; return Page(); }
        Pending = pending.Data ?? new();

        var members = await _api.GetAsync<List<UserAdminDto>>(token, "/api/users?status=PendingActivation", cancellationToken);
        if (members.Unauthorized) return SessionExpired();
        if (members.Success) PendingMembers = members.Data ?? new();

        var customers = await _api.GetAsync<List<CustomerResponse>>(token, "/api/customers", cancellationToken);
        if (customers.Unauthorized) return SessionExpired();
        if (customers.Success) CustomerCount = customers.Data?.Count ?? 0;

        var accounts = await _api.GetAsync<PaginatedResponse<AccountDto>>(token, "/api/accounts?pageSize=100", cancellationToken);
        if (accounts.Unauthorized) return SessionExpired();
        if (accounts.Success)
        {
            Accounts = accounts.Data?.Items ?? new();
            AccountCount = accounts.Data?.TotalCount ?? Accounts.Count;
        }

        var audit = await _api.GetAsync<PaginatedResponse<AuditLogDto>>(token, "/api/audit?pageSize=5", cancellationToken);
        if (audit.Success) RecentAudit = audit.Data?.Items ?? new();

        var delegations = await _api.GetAsync<List<DelegationDto>>(token, "/api/delegations", cancellationToken);
        if (delegations.Success) Delegations = delegations.Data ?? new();

        return Page();
    }

    public IEnumerable<IGrouping<string, AccountDto>> BalancesByCurrency =>
        Accounts.GroupBy(x => x.Currency).OrderBy(x => x.Key);
}
