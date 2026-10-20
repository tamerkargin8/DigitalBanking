using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Delegations;

namespace DigitalBanking.Application.Abstractions.Services;

public interface IDelegationService
{
    Task<List<DelegationDto>> GetAllAsync(Guid? userId, CancellationToken cancellationToken = default);
    Task<DelegationDto> CreateAsync(CreateDelegationRequest request, ActorContext owner, CancellationToken cancellationToken = default);
    Task<DelegationDto> UpdateAsync(Guid id, UpdateDelegationRequest request, ActorContext owner, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid id, ActorContext owner, CancellationToken cancellationToken = default);
}
