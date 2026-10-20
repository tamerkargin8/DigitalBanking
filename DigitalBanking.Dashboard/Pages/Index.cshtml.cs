using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages;

public class IndexModel : SecurePageModel
{
    public IActionResult OnGet()
    {
        var login = RequireLogin();
        if (login is not null) return login;

        if (IsAdmin) return RedirectToPage("/Admin/Overview");
        if (IsDelegatedApprover) return RedirectToPage("/Admin/Approvals/Index");
        return RedirectToPage("/Customer/Home");
    }
}
