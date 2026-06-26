using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure;
using DigitalBanking.Infrastructure.Persistence;
using DigitalBanking.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly BankDbContext _db;
    private readonly PasswordHasher _hasher;

    public UsersController(BankDbContext db, PasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    // GET /api/users
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _db.Users
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new UserDto
            {
                Id = x.Id,
                Username = x.Username,
                Role = x.Role.ToString(),
                CreatedDate = x.CreatedDate
            })
            .ToListAsync();

        return Ok(users);
    }

    // POST /api/users
    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username))
            return BadRequest("Username required");

        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 4)
            return BadRequest("Password min 4 chars");

        if (!Enum.TryParse<UserRole>(req.Role, ignoreCase: true, out var role))
            return BadRequest("Invalid role. Use Admin or BankUser");

        var exists = await _db.Users.AnyAsync(x => x.Username == req.Username);
        if (exists)
            return Conflict("Username already exists");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = req.Username.Trim(),
            PasswordHash = _hasher.Hash(req.Password),
            Role = role,
            CreatedDate = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Role = user.Role.ToString(),
            CreatedDate = user.CreatedDate
        });
    }

    public class CreateUserRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Role { get; set; } = "BankUser"; // "Admin" | "BankUser"
    }

    public class UserDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = "";
        public string Role { get; set; } = "";
        public DateTime CreatedDate { get; set; }
    }
}