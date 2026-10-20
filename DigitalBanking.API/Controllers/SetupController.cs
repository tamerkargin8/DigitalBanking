using System.Net;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Identity;
using DigitalBanking.Application.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DigitalBanking.API.Controllers;

/// <summary>
/// One-time local Owner setup. It is anonymous only until an Owner exists, only
/// from loopback and only in Development. Public registration can never create Owner.
/// </summary>
[ApiController]
[Route("api/setup")]
[AllowAnonymous]
public class SetupController : ControllerBase
{
    private readonly IUserIdentityService _identity;
    private readonly IWebHostEnvironment _environment;

    public SetupController(IUserIdentityService identity, IWebHostEnvironment environment)
    {
        _identity = identity;
        _environment = environment;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        EnsureLoopback();
        var ownerExists = await _identity.OwnerExistsAsync(cancellationToken);
        return Ok(new
        {
            needsOwner = !ownerExists,
            setupEnabled = !ownerExists && _environment.IsDevelopment() && IsLoopback()
        });
    }

    [HttpPost("owner")]
    [EnableRateLimiting("register")]
    public async Task<IActionResult> CreateOwner(
        [FromBody] SetupOwnerRequest request,
        CancellationToken cancellationToken)
    {
        EnsureLoopback();
        if (!_environment.IsDevelopment())
            throw new NotFoundException("Owner setup is available only in local Development.");

        var owner = await _identity.SetupOwnerAsync(request, cancellationToken);
        return Ok(ApiResponse<UserAdminDto>.SuccessWith(owner,
            "Owner oluşturuldu. Kurulum endpoint'i kapatıldı; lütfen giriş yapın."));
    }

    private void EnsureLoopback()
    {
        if (!IsLoopback())
            throw new ForbiddenException("Owner setup yalnız localhost üzerinden yapılabilir.");
    }

    private bool IsLoopback()
    {
        var address = HttpContext.Connection.RemoteIpAddress;
        if (address is null) return false;
        if (IPAddress.IsLoopback(address)) return true;
        return address.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(address.MapToIPv4());
    }
}
