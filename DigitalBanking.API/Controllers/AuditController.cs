using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Audit;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.API.Controllers;

/// <summary>
/// Read-only audit trail. Admin only; details never contain passwords or tokens.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Owner")]
public class AuditController : ControllerBase
{
    private readonly BankDbContext _db;

    public AuditController(BankDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<AuditLogDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? action = null,
        [FromQuery] string? username = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var rangeTo = to ?? DateTime.UtcNow;
        var rangeFrom = from ?? rangeTo.AddDays(-7);
        var (normalizedFrom, normalizedTo) = ReportDateRange.Normalize(rangeFrom, rangeTo);

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.AuditLogs.AsNoTracking()
            .Where(x => x.CreatedDate >= normalizedFrom && x.CreatedDate <= normalizedTo);

        if (!string.IsNullOrWhiteSpace(action))
        {
            var actionFilter = action.Trim();
            query = query.Where(x => x.Action.Contains(actionFilter));
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            var usernameFilter = username.Trim();
            query = query.Where(x => x.Username != null && x.Username.Contains(usernameFilter));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AuditLogDto
            {
                Id = x.Id,
                Username = x.Username,
                Action = x.Action,
                EntityName = x.EntityName,
                EntityId = x.EntityId,
                IsSuccess = x.IsSuccess,
                Details = x.Details,
                CreatedDate = x.CreatedDate
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<PaginatedResponse<AuditLogDto>>.SuccessWith(
            new PaginatedResponse<AuditLogDto>(items, page, pageSize, totalCount)));
    }
}
