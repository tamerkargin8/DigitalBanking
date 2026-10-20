namespace DigitalBanking.Application.Abstractions.Services;

/// <summary>
/// Server-side ownership resolution for the customer portal. Never trust ids
/// supplied by the browser; resolve the customer from the JWT user id.
/// </summary>
public interface ICustomerAccessService
{
    Task<Guid?> GetCustomerIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> UserOwnsAccountAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default);
    Task<bool> UserOwnsPaymentAsync(Guid userId, int paymentId, CancellationToken cancellationToken = default);
}
