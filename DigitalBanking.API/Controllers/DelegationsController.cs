using DigitalBanking.API.Extensions;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Delegations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.API.Controllers;

/// <summary>
/// Owner-only approval delegation management. DelegatedApprover cannot assign
/// itself or others; role/limit checks are repeated server-side.
/// </summary>
[ApiController]
[Route("api/delegations")]
[Authorize(Roles = "Owner")]
public class DelegationsController : ControllerBase
{
    private readonly IDelegationService _delegations;

    public DelegationsController(IDelegationService delegations)
    {
        _delegations = delegations;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        var items = await _delegations.GetAllAsync(userId, cancellationToken);
        return Ok(ApiResponse<List<DelegationDto>>.SuccessWith(items));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDelegationRequest request, CancellationToken cancellationToken)
    {
        var item = await _delegations.CreateAsync(request, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<DelegationDto>.SuccessWith(item, "Yetki devri oluşturuldu."));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDelegationRequest request, CancellationToken cancellationToken)
    {
        var item = await _delegations.UpdateAsync(id, request, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<DelegationDto>.SuccessWith(item, "Yetki devri güncellendi."));
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await _delegations.DeactivateAsync(id, User.GetActor(), cancellationToken);
        return Ok(ApiResponse.SuccessResponse("Yetki devri hemen kapatıldı."));
    }
}
