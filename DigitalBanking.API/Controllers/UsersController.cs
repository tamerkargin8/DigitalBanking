using DigitalBanking.API.Extensions;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
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
    private readonly IAuditService _auditService;

    public UsersController(BankDbContext db, PasswordHasher hasher, IAuditService auditService)
    {
        _db = db;
        _hasher = hasher;
        _auditService = auditService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<UserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var users = await _db.Users
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new UserDto
            {
                Id = x.Id,
                Username = x.Username,
                Role = x.Role.ToString(),
                CreatedDate = x.CreatedDate
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<List<UserDto>>.SuccessWith(users));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        if (username.Length < 3 || username.Length > 64)
            throw new BusinessRuleException("Username must be between 3 and 64 characters.");

        PasswordPolicy.EnsureValid(request.Password);

        if (!TryParseRole(request.Role, out var role))
            throw new BusinessRuleException("Invalid role. Use Admin or User.");

        if (await _db.Users.AnyAsync(x => x.Username == username, cancellationToken))
            return Conflict(ApiResponse.Failure("Username already exists."));

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = _hasher.Hash(request.Password),
            Role = role,
            CreatedDate = DateTime.UtcNow
        };

        _db.Users.Add(user);
        _auditService.Add(
            action: "USER_CREATED",
            entityName: "User",
            entityId: user.Id.ToString(),
            username: User.GetActor().Username,
            details: $"User created. Role: {user.Role}");

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<UserDto>.SuccessWith(new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Role = user.Role.ToString(),
            CreatedDate = user.CreatedDate
        }, "User created successfully."));
    }

    private static bool TryParseRole(string? roleText, out UserRole role)
    {
        role = UserRole.None;
        if (string.IsNullOrWhiteSpace(roleText)) return false;

        var normalized = roleText.Trim();
        // The previous dashboard used "BankUser" for the standard role.
        if (string.Equals(normalized, "BankUser", StringComparison.OrdinalIgnoreCase))
        {
            role = UserRole.User;
            return true;
        }

        return Enum.TryParse(normalized, ignoreCase: true, out role) && role is UserRole.Admin or UserRole.User;
    }

    public class CreateUserRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "User";
    }

    public class UserDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }
}
