using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DigitalBanking.Dashboard.Pages.Admin.Users;

// Membership creation is now public registration + Owner activation; this legacy
// page is kept only as a redirect so old bookmarks do not break.
public class CreateModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Admin/Users/Index");
    public IActionResult OnPost() => RedirectToPage("/Admin/Users/Index");
}
