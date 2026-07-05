using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Auth;
using DigitalBanking.Application.DTOs.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.API.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<LoginRequest> _validator;

    public AuthController(
        IAuthService authService,
        IValidator<LoginRequest> validator)
    {
        _authService = authService;
        _validator = validator;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login(
    [FromBody] LoginRequest request,
    CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(
            request,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            var errorMessage = string.Join(" | ",
                validationResult.Errors.Select(x => x.ErrorMessage));

            return BadRequest(ApiResponse.Failure(errorMessage));
        }

        var response = await _authService.LoginAsync(
            request,
            cancellationToken);

        return Ok(ApiResponse<LoginResponse>.SuccessWith(
            response,
            "Login successful."));
    }
}