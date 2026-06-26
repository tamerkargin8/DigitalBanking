using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;
using System.Net.Http.Json;

namespace DigitalBanking.Dashboard.Pages.Admin.Users;

public class IndexModel : PageModel
{
    private readonly ApiClient _api;

    public IndexModel(ApiClient api)
    {
        _api = api;
    }

    public List<UserRow> Items { get; set; } = new();
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var token = HttpContext.Session.GetString("jwt");
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Login");

        try
        {
            var http = _api.Create(token);
            var resp = await http.GetAsync("/api/users");

            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                Error = "Access denied (Admin only).";
                return Page();
            }

            resp.EnsureSuccessStatusCode();
            Items = await resp.Content.ReadFromJsonAsync<List<UserRow>>() ?? new();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }

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