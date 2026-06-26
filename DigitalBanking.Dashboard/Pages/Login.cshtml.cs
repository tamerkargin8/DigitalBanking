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

    public IActionResult OnGet()
    {
        // Zaten token varsa dashboard'a
        var token = HttpContext.Session.GetString("jwt");
        if (!string.IsNullOrEmpty(token))
            return RedirectToPage("/Index");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var token = await _auth.LoginAsync(Username, Password);

        if (string.IsNullOrEmpty(token))
        {
            Error = "Login failed. Username/password yanlış olabilir.";
            return Page();
        }

        HttpContext.Session.SetString("jwt", token);
        return RedirectToPage("/Index");
    }
}