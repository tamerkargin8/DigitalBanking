using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Infrastructure.Security;
using DigitalBanking.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalBanking.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPaymentApprovalService, PaymentApprovalService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();

        services.AddSingleton<ILoginAttemptLimiter, InMemoryLoginAttemptLimiter>();

        return services;
    }
}
