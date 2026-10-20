using DigitalBanking.API.Extensions;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.API.Controllers;

/// <summary>
/// Owner-only membership management. Registration is public; activation and
/// Customer/DelegatedApprover role changes are Owner decisions. Owner role is not
/// assignable here and the last Owner cannot be disabled or demoted.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize(Roles = "Owner")]
public class UsersController : ControllerBase
{
    private readonly IUserIdentityService _identity;

    public UsersController(IUserIdentityService identity)
    {
        _identity = identity;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<UserAdminDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status = null,
        [FromQuery] string? role = null,
        CancellationToken cancellationToken = default)
    {
        var users = await _identity.GetUsersAsync(status, role, cancellationToken);
        return Ok(ApiResponse<List<UserAdminDto>>.SuccessWith(users));
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var user = await _identity.ActivateAsync(id, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<UserAdminDto>.SuccessWith(user, "Üyelik aktive edildi."));
    }

    [HttpPost("{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken)
    {
        var user = await _identity.DisableAsync(id, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<UserAdminDto>.SuccessWith(user, "Üyelik devre dışı bırakıldı; delegasyonlar kapatıldı."));
    }

    [HttpPost("{id:guid}/role")]
    public async Task<IActionResult> ChangeRole(
        Guid id,
        [FromBody] ChangeUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _identity.ChangeRoleAsync(id, request.Role, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<UserAdminDto>.SuccessWith(user, "Rol güncellendi."));
    }
}
