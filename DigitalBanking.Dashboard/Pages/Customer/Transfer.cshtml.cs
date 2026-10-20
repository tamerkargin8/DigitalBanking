using DigitalBanking.Application.DTOs.Account;
using DigitalBanking.Application.DTOs.CustomerPortal;
using DigitalBanking.Application.DTOs.PaymentApprovals;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Customer;

public class TransferModel : SecurePageModel
{
    private readonly ApiClient _api;

    public TransferModel(ApiClient api)
    {
        _api = api;
    }

    public List<AccountDto> Accounts { get; set; } = new();
    public List<BeneficiaryDto> Beneficiaries { get; set; } = new();
    public string? Error { get; set; }
    public bool ShowSummary { get; set; }

    [BindProperty] public Guid FromAccountId { get; set; }
    [BindProperty] public Guid ToAccountId { get; set; }
    [BindProperty] public decimal Amount { get; set; }
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public string IdempotencyKey { get; set; } = "";
    [BindProperty] public bool Confirm { get; set; }
    [BindProperty] public bool Edit { get; set; }

    public AccountDto? SelectedAccount => Accounts.FirstOrDefault(x => x.Id == FromAccountId);
    public BeneficiaryDto? SelectedBeneficiary => Beneficiaries.FirstOrDefault(x => x.AccountId == ToAccountId);

    public async Task<IActionResult> OnGetAsync(Guid? from = null, CancellationToken cancellationToken = default)
    {
        var customer = RequireCustomer();
        if (customer is not null) return customer;
        if (from is not null) FromAccountId = from.Value;
        IdempotencyKey = NewIdempotencyKey();
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is not null) return customer;
        await LoadAsync(cancellationToken);

        if (FromAccountId == Guid.Empty || ToAccountId == Guid.Empty)
            Error = "Hesap ve alıcı seçin.";
        else if (Amount <= 0)
            Error = "Tutar 0'dan büyük olmalıdır.";
        else if (SelectedAccount is null)
            Error = "Seçili hesap bulunamadı.";

        if (!string.IsNullOrEmpty(Error))
        {
            if (string.IsNullOrWhiteSpace(IdempotencyKey)) IdempotencyKey = NewIdempotencyKey();
            return Page();
        }

        if (Edit)
        {
            ShowSummary = false;
            return Page();
        }

        if (!Confirm)
        {
            ShowSummary = true;
            return Page();
        }

        if (string.IsNullOrWhiteSpace(IdempotencyKey)) IdempotencyKey = NewIdempotencyKey();

        var result = await _api.PostAsync<PaymentApprovalResponse>(Token!, "/api/me/transfers", new
        {
            fromAccountId = FromAccountId,
            toAccountId = ToAccountId,
            amount = Amount,
            description = Description
        }, IdempotencyKey, cancellationToken);

        if (result.Unauthorized) return SessionExpired();
        if (!result.Success)
        {
            Error = result.Error;
            ShowSummary = false;
            return Page();
        }

        return RedirectToPage("/Customer/Payments/Details", new { id = result.Data!.Id, created = true });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var accounts = await _api.GetAsync<List<AccountDto>>(Token!, "/api/me/accounts", cancellationToken);
        if (accounts.Unauthorized) return;
        if (accounts.Success) Accounts = accounts.Data ?? new();

        var beneficiaries = await _api.GetAsync<List<BeneficiaryDto>>(Token!, "/api/me/beneficiaries", cancellationToken);
        if (beneficiaries.Success) Beneficiaries = beneficiaries.Data ?? new();
    }
}
