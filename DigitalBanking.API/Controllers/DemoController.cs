using System.Text;
using DigitalBanking.API.Extensions;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Demo;
using DigitalBanking.Application.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.API.Controllers;

/// <summary>
/// Development-only demo center. Both the controller and service reject
/// non-Development environments; hiding the menu is not the security boundary.
/// </summary>
[ApiController]
[Route("api/demo")]
[Authorize(Roles = "Owner")]
public class DemoController : ControllerBase
{
    private readonly IDemoScenarioService _demo;
    private readonly IWebHostEnvironment _environment;

    public DemoController(IDemoScenarioService demo, IWebHostEnvironment environment)
    {
        _demo = demo;
        _environment = environment;
    }

    [HttpGet("scenarios")]
    [ProducesResponseType(typeof(ApiResponse<List<DemoScenarioDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScenarios(CancellationToken cancellationToken)
    {
        EnsureDevelopment();
        var scenarios = await _demo.GetScenariosAsync(cancellationToken);
        return Ok(ApiResponse<List<DemoScenarioDto>>.SuccessWith(scenarios));
    }

    [HttpPost("seed")]
    [ProducesResponseType(typeof(ApiResponse<DemoSeedResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Seed(CancellationToken cancellationToken)
    {
        EnsureDevelopment();
        var result = await _demo.SeedAsync(User.GetActor(), cancellationToken);
        return Ok(ApiResponse<DemoSeedResultDto>.SuccessWith(result,
            result.AlreadySeeded ? "Demo senaryoları zaten seed edilmiş." : "Demo senaryoları oluşturuldu."));
    }

    [HttpPost("reset")]
    [ProducesResponseType(typeof(ApiResponse<DemoResetResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reset(CancellationToken cancellationToken)
    {
        EnsureDevelopment();
        var result = await _demo.ResetAsync(User.GetActor(), cancellationToken);
        return Ok(ApiResponse<DemoResetResultDto>.SuccessWith(result, result.Message));
    }

    [HttpGet("reconciliation-sample")]
    [Produces("text/csv")]
    public async Task<IActionResult> ReconciliationSample([FromQuery] string kind = "matched", CancellationToken cancellationToken = default)
    {
        EnsureDevelopment();
        var csv = await _demo.BuildReconciliationSampleAsync(kind, cancellationToken);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", $"demo-reconciliation-{kind}.csv");
    }

    private void EnsureDevelopment()
    {
        if (!_environment.IsDevelopment())
            throw new NotFoundException("Demo center is available only in Development.");
    }
}
