using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DigitalBanking.Dashboard.Pages;

public class RegisterModel : PageModel
{
    private readonly ApiAuthService _auth;

    public RegisterModel(ApiAuthService auth)
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
    public string? Success { get; set; }

    public IActionResult OnGet()
    {
        if (!string.IsNullOrEmpty(HttpContext.Session.GetString(SessionKeys.Token)))
            return RedirectToPage("/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (Password != ConfirmPassword)
        {
            Error = "Şifre ve şifre doğrulaması eşleşmiyor.";
            return Page();
        }

        var result = await _auth.RegisterAsync(
            Username, Email, FirstName, LastName, Password, ConfirmPassword, cancellationToken);
        if (!result.Success)
        {
            Error = result.Message;
            return Page();
        }

        Success = result.Message;
        Username = Email = FirstName = LastName = Password = ConfirmPassword = "";
        return Page();
    }
}
