using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DigitalBanking.Dashboard.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        var token = HttpContext.Session.GetString("jwt");
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Login");

        return Page();
    }
}