using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Admin.Users;

public class IndexModel : SecurePageModel
{
    private readonly ApiClient _api;

    public IndexModel(ApiClient api)
    {
        _api = api;
    }

    public List<UserRow> Items { get; set; } = new();
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;

        var result = await _api.GetAsync<List<UserRow>>(Token!, "/api/users", cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        if (result.Forbidden)
        {
            Error = "Bu ekran için Admin yetkisi gerekir.";
            return Page();
        }
        if (!result.Success)
        {
            Error = result.Error;
            return Page();
        }

        Items = result.Data ?? new List<UserRow>();
        return Page();
    }

    public class UserRow
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = "";
        public string Role { get; set; } = "";
        public DateTime CreatedDate { get; set; }
    }
}
