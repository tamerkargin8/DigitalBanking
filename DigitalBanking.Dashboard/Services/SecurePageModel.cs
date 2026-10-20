using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DigitalBanking.Dashboard.Services;

/// <summary>
/// Shared session/role helpers for Razor Pages. Hiding a link is only a UX nicety;
/// the API independently enforces the same role policy.
/// </summary>
public abstract class SecurePageModel : PageModel
{
    protected string? Token => HttpContext.Session.GetString(SessionKeys.Token);
    public string CurrentUsername => HttpContext.Session.GetString(SessionKeys.Username) ?? string.Empty;
    public string CurrentRole => HttpContext.Session.GetString(SessionKeys.Role) ?? string.Empty;
    protected bool IsAdmin => string.Equals(CurrentRole, "Admin", StringComparison.OrdinalIgnoreCase);

    protected IActionResult? RequireLogin() =>
        string.IsNullOrWhiteSpace(Token) ? RedirectToPage("/Login") : null;

    protected IActionResult? RequireAdmin()
    {
        var login = RequireLogin();
        if (login is not null) return login;

        return IsAdmin
            ? null
            : RedirectToPage("/Index", new { denied = true });
    }

    protected IActionResult SessionExpired()
    {
        HttpContext.Session.Clear();
        return RedirectToPage("/Login", new { expired = 1 });
    }

    protected static string NewIdempotencyKey() => Guid.NewGuid().ToString("N");

    protected static string ShortId(Guid id) => id.ToString("N")[..8];
}
