using DigitalBanking.Application.DTOs.Auth;

namespace DigitalBanking.Application.Abstractions.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);
}