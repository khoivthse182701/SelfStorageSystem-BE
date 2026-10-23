using SelfStorageSystem.Contracts.Auth;

namespace SelfStorageSystem.Application.Interfaces;

public interface ICapabilitiesService
{
    Task<UserCapabilitiesDto> GetUserCapabilitiesAsync(long userId, CancellationToken cancellationToken = default);
}
