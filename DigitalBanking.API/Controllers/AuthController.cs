using DigitalBanking.Domain.Entities;
using DigitalBanking.Infrastructure;
using DigitalBanking.Infrastructure.Persistence;
using DigitalBanking.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly BankDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly PasswordHasher _hasher;

    public AuthController(BankDbContext db, JwtTokenService jwt, PasswordHasher hasher)
    {
        _db = db;
        _jwt = jwt;
        _hasher = hasher;
    }

    // POST /api/auth/login?username=admin&password=1234
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromQuery] string username, [FromQuery] string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return BadRequest("username/password required");

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Username == username);
        if (user == null)
            return Unauthorized("User not found");

        if (!_hasher.Verify(password, user.PasswordHash))
            return Unauthorized("Invalid password");

        var token = _jwt.GenerateToken(user);
        return Ok(new { token });
    }
}