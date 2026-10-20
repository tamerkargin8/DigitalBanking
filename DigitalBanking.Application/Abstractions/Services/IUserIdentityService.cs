using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Identity;

namespace DigitalBanking.Application.Abstractions.Services;

public interface IUserIdentityService
{
    Task<bool> OwnerExistsAsync(CancellationToken cancellationToken = default);
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<UserAdminDto> SetupOwnerAsync(SetupOwnerRequest request, CancellationToken cancellationToken = default);
    Task<List<UserAdminDto>> GetUsersAsync(string? status, string? role, CancellationToken cancellationToken = default);
    Task<UserAdminDto> ActivateAsync(Guid userId, ActorContext owner, CancellationToken cancellationToken = default);
    Task<UserAdminDto> DisableAsync(Guid userId, ActorContext owner, CancellationToken cancellationToken = default);
    Task<UserAdminDto> ChangeRoleAsync(Guid userId, string role, ActorContext owner, CancellationToken cancellationToken = default);
}
