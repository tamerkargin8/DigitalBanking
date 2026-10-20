using DigitalBanking.Application.DTOs.CustomerPortal;

namespace DigitalBanking.Application.Abstractions.Services;

/// <summary>
/// Read-only timeline/receipt projection built from persisted events. Callers must
/// perform authorization (Admin or owning customer) before invoking.
/// </summary>
public interface IPaymentTimelineService
{
    Task<PaymentTimelineDto> GetTimelineAsync(int paymentId, CancellationToken cancellationToken = default);
    Task<PaymentReceiptDto> GetReceiptAsync(int paymentId, CancellationToken cancellationToken = default);
}
