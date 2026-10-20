using DigitalBanking.API.Extensions;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Auth;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DigitalBanking.API.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserIdentityService _identity;
    private readonly IValidator<LoginRequest> _validator;

    public AuthController(IAuthService authService, IUserIdentityService identity, IValidator<LoginRequest> validator)
    {
        _authService = authService;
        _identity = identity;
        _validator = validator;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errorMessage = string.Join(" | ", validationResult.Errors.Select(x => x.ErrorMessage));
            return BadRequest(ApiResponse.Failure(errorMessage));
        }

        var response = await _authService.LoginAsync(request, cancellationToken);

        return Ok(ApiResponse<LoginResponse>.SuccessWith(response, "Login successful."));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("register")]
    [ProducesResponseType(typeof(ApiResponse<RegisterResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _identity.RegisterAsync(request, cancellationToken);
        return Ok(ApiResponse<RegisterResponse>.SuccessWith(result, result.Message));
    }

    /// <summary>
    /// Returns the authenticated caller's identity from validated claims. The
    /// dashboard uses it to detect an expired/invalid session without decoding the JWT.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<MeResponse>), StatusCodes.Status200OK)]
    public IActionResult Me()
    {
        var actor = User.GetActor();
        return Ok(ApiResponse<MeResponse>.SuccessWith(new MeResponse
        {
            UserId = actor.UserId,
            Username = actor.Username,
            Role = actor.Role.ToString()
        }));
    }

    public class MeResponse
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
