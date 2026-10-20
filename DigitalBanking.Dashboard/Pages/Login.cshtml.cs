using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DigitalBanking.Dashboard.Pages;

public class LoginModel : PageModel
{
    private readonly ApiAuthService _auth;

    public LoginModel(ApiAuthService auth)
    {
        _auth = auth;
    }

    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    public string? Error { get; set; }
    public string? Info { get; set; }

    public async Task<IActionResult> OnGetAsync(bool expired = false, bool loggedOut = false, bool setup = false)
    {
        if (!string.IsNullOrEmpty(HttpContext.Session.GetString(SessionKeys.Token)))
            return RedirectToPage("/Index");

        var status = await _auth.GetSetupStatusAsync();
        if (status is { NeedsOwner: true, SetupEnabled: true })
            return RedirectToPage("/Setup");

        if (setup)
            Info = "Owner oluşturuldu. Lütfen giriş yapın.";
        else if (expired)
            Info = "Oturum süresi doldu. Lütfen yeniden giriş yapın.";
        else if (loggedOut)
            Info = "Çıkış yapıldı.";

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            Error = "Kullanıcı adı ve şifre zorunludur.";
            return Page();
        }

        var outcome = await _auth.LoginAsync(Username.Trim(), Password, cancellationToken);
        if (!outcome.Success || string.IsNullOrWhiteSpace(outcome.Token))
        {
            Error = outcome.Error ?? "Giriş yapılamadı.";
            return Page();
        }

        // Clear any previous session state before storing the new identity.
        HttpContext.Session.Clear();
        HttpContext.Session.SetString(SessionKeys.Token, outcome.Token);
        if (outcome.UserId != Guid.Empty)
            HttpContext.Session.SetString(SessionKeys.UserId, outcome.UserId.ToString());
        HttpContext.Session.SetString(SessionKeys.Username, outcome.Username ?? Username.Trim());
        HttpContext.Session.SetString(SessionKeys.Role, outcome.Role ?? "");
        if (outcome.CustomerId is { } customerId)
            HttpContext.Session.SetString(SessionKeys.CustomerId, customerId.ToString());
        if (!string.IsNullOrWhiteSpace(outcome.DisplayName))
            HttpContext.Session.SetString(SessionKeys.DisplayName, outcome.DisplayName);

        return RedirectToPage("/Index");
    }
}
