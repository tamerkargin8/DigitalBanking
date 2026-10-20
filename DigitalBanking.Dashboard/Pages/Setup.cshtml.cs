using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DigitalBanking.Dashboard.Pages;

public class SetupModel : PageModel
{
    private readonly ApiAuthService _auth;

    public SetupModel(ApiAuthService auth)
    {
        _auth = auth;
    }

    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Email { get; set; } = "";
    [BindProperty] public string FirstName { get; set; } = "";
    [BindProperty] public string LastName { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public string ConfirmPassword { get; set; } = "";
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var status = await _auth.GetSetupStatusAsync();
        if (status is null)
        {
            Error = "API'ye ulaşılamıyor. Yerel servislerin çalıştığını kontrol edin.";
            return Page();
        }
        if (!status.NeedsOwner)
            return RedirectToPage("/Login", new { setup = true });
        if (!status.SetupEnabled)
        {
            Error = "Owner kurulumu yalnız localhost/Development ortamında açıktır.";
            return Page();
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // A form opened before setup completed can be submitted later. Re-check
        // the authoritative setup state before accepting any password material.
        var status = await _auth.GetSetupStatusAsync(cancellationToken);
        if (status is null)
        {
            ClearSecrets();
            Error = "API'ye ulaşılamıyor. Yerel servislerin çalıştığını kontrol edin.";
            return Page();
        }
        if (!status.NeedsOwner)
        {
            ClearSecrets();
            return RedirectToPage("/Login", new { setup = true });
        }
        if (!status.SetupEnabled)
        {
            ClearSecrets();
            Error = "Owner kurulumu yalnız localhost/Development ortamında açıktır.";
            return Page();
        }

        if (Password != ConfirmPassword)
        {
            ClearSecrets();
            Error = "Şifre ve şifre doğrulaması eşleşmiyor.";
            return Page();
        }

        var result = await _auth.SetupOwnerAsync(
            Username, Email, FirstName, LastName, Password, ConfirmPassword, cancellationToken);
        if (!result.Success)
        {
            // A concurrent setup may have won between the status check and POST.
            // Treat that as a completed setup, not as a form error or a 500.
            var after = await _auth.GetSetupStatusAsync(cancellationToken);
            if (after is { NeedsOwner: false })
            {
                ClearSecrets();
                return RedirectToPage("/Login", new { setup = true });
            }

            ClearSecrets();
            Error = result.Message ?? "Kurulum tamamlanamadı. Lütfen tekrar deneyin.";
            return Page();
        }

        ClearSecrets();
        return RedirectToPage("/Login", new { setup = true });
    }

    private void ClearSecrets()
    {
        Password = string.Empty;
        ConfirmPassword = string.Empty;
    }
}
