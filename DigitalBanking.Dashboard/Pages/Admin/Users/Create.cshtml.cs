using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;
using System.Net.Http.Json;

namespace DigitalBanking.Dashboard.Pages.Admin.Users;

public class CreateModel : PageModel
{
    private readonly ApiClient _api;

    public CreateModel(ApiClient api)
    {
        _api = api;
    }

    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public string Role { get; set; } = "BankUser";

    public string? Error { get; set; }
    public string? Success { get; set; }

    public IActionResult OnGet()
    {
        var token = HttpContext.Session.GetString("jwt");
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Login");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var token = HttpContext.Session.GetString("jwt");
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Login");

        try
        {
            var http = _api.Create(token);

            var payload = new
            {
                username = Username,
                password = Password,
                role = Role
            };

            var resp = await http.PostAsJsonAsync("/api/users", payload);

            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                Error = "Access denied (Admin only).";
                return Page();
            }

            if (resp.StatusCode == HttpStatusCode.Conflict)
            {
                Error = "Username already exists.";
                return Page();
            }

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                Error = $"Create failed: {(int)resp.StatusCode} {resp.ReasonPhrase} | {body}";
                return Page();
            }

            Success = "User created ✅";
            // Formu temizle
            Username = "";
            Password = "";
            Role = "BankUser";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }

        return Page();
    }
}