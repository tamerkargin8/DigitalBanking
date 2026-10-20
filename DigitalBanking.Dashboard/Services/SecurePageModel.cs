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
    public string CurrentDisplayName => HttpContext.Session.GetString(SessionKeys.DisplayName) ?? CurrentUsername;
    public string CurrentRole => HttpContext.Session.GetString(SessionKeys.Role) ?? string.Empty;
    public Guid? CurrentUserId => Guid.TryParse(HttpContext.Session.GetString(SessionKeys.UserId), out var id) ? id : null;
    protected bool IsAdmin => string.Equals(CurrentRole, "Owner", StringComparison.OrdinalIgnoreCase);
    protected bool IsDelegatedApprover => string.Equals(CurrentRole, "DelegatedApprover", StringComparison.OrdinalIgnoreCase);
    protected bool IsCustomer => string.Equals(CurrentRole, "Customer", StringComparison.OrdinalIgnoreCase);

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

    protected IActionResult? RequireApprover()
    {
        var login = RequireLogin();
        if (login is not null) return login;
        return IsAdmin || IsDelegatedApprover
            ? null
            : RedirectToPage("/Index", new { denied = true });
    }

    protected IActionResult? RequireCustomer()
    {
        var login = RequireLogin();
        if (login is not null) return login;
        if (IsCustomer) return null;
        return IsAdmin
            ? RedirectToPage("/Admin/Overview")
            : RedirectToPage("/Admin/Approvals/Index");
    }

    protected IActionResult SessionExpired()
    {
        HttpContext.Session.Clear();
        return RedirectToPage("/Login", new { expired = 1 });
    }

    protected IActionResult? HandleUnauthorized<T>(ApiResult<T> result) =>
        result.Unauthorized ? SessionExpired() : null;

    protected static string NewIdempotencyKey() => Guid.NewGuid().ToString("N");

    protected static string ShortId(Guid id) => id.ToString("N")[..8];
}
