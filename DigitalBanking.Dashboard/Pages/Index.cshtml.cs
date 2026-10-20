using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages;

public class IndexModel : SecurePageModel
{
    public bool AccessDenied { get; set; }

    public IActionResult OnGet(bool denied = false)
    {
        var login = RequireLogin();
        if (login is not null) return login;

        AccessDenied = denied;
        return Page();
    }
}
