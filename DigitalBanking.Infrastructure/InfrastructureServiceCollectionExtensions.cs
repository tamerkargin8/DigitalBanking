using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalBanking.Infrastructure;

/// <summary>
/// Extension methods for registering Infrastructure services in dependency injection.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Adds Infrastructure services (business logic layer).
    /// Currently includes: AccountService, TransactionService
    /// </summary>
    /// <example>
    /// builder.Services.AddInfrastructureServices();
    /// </example>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Account and Transaction services
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ITransactionService, TransactionService>();

        return services;
    }
}
