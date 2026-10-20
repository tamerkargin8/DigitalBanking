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
        if (Password != ConfirmPassword)
        {
            Error = "Şifre ve şifre doğrulaması eşleşmiyor.";
            return Page();
        }

        var result = await _auth.SetupOwnerAsync(
            Username, Email, FirstName, LastName, Password, ConfirmPassword, cancellationToken);
        if (!result.Success)
        {
            Error = result.Message;
            return Page();
        }

        return RedirectToPage("/Login", new { setup = true });
    }
}
