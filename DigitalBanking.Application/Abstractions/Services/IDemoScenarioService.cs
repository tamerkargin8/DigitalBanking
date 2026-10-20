using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Demo;

namespace DigitalBanking.Application.Abstractions.Services;

/// <summary>
/// Development-only demo center. The controller and implementation both reject
/// non-Development environments server-side; production never relies on hidden UI.
/// </summary>
public interface IDemoScenarioService
{
    Task<List<DemoScenarioDto>> GetScenariosAsync(CancellationToken cancellationToken = default);
    Task<DemoSeedResultDto> SeedAsync(ActorContext actor, CancellationToken cancellationToken = default);
    Task<DemoResetResultDto> ResetAsync(ActorContext actor, CancellationToken cancellationToken = default);
    Task<string> BuildReconciliationSampleAsync(string kind, CancellationToken cancellationToken = default);
}
