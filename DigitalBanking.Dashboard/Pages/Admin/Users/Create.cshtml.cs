using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Admin.Users;

public class CreateModel : SecurePageModel
{
    private readonly ApiClient _api;

    public CreateModel(ApiClient api)
    {
        _api = api;
    }

    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public string Role { get; set; } = "User";

    public string? Error { get; set; }
    public string? Success { get; set; }

    public IActionResult OnGet()
    {
        var admin = RequireAdmin();
        return admin ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;

        var result = await _api.PostAsync<UserRow>(Token!, "/api/users", new
        {
            username = Username,
            password = Password,
            role = Role
        }, cancellationToken: cancellationToken);

        if (result.Unauthorized) return SessionExpired();
        if (!result.Success)
        {
            Error = result.Error;
            return Page();
        }

        Success = "Kullanıcı oluşturuldu.";
        Username = "";
        Password = "";
        Role = "User";
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
