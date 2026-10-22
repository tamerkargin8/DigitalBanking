using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Infrastructure.Security;
using DigitalBanking.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalBanking.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPaymentApprovalService, PaymentApprovalService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<ICustomerAccessService, CustomerAccessService>();
        services.AddScoped<ICustomerPortalService, CustomerPortalService>();
        services.AddScoped<IPaymentTimelineService, PaymentTimelineService>();
        services.AddScoped<IReconciliationService, ReconciliationService>();
        services.AddScoped<IDemoScenarioService, DemoScenarioService>();
        services.AddScoped<IUserIdentityService, UserIdentityService>();
        services.AddScoped<IDelegationService, DelegationService>();
        services.AddScoped<IApprovalAuthorizationService, ApprovalAuthorizationService>();
        services.AddScoped<IAccountingService, AccountingService>();
        services.AddScoped<IOperationsService, OperationsService>();
        services.Configure<OperationsOptions>(configuration.GetSection(OperationsOptions.SectionName));
        services.AddSingleton<IClock, SystemClock>();

        services.AddSingleton<ILoginAttemptLimiter, InMemoryLoginAttemptLimiter>();

        return services;
    }
}
