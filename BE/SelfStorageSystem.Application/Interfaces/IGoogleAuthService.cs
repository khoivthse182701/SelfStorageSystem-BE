using SelfStorageSystem.Application.DTOs;

namespace SelfStorageSystem.Application.Interfaces;

public interface IGoogleAuthService
{
    Task<GoogleUserInfo> ValidateIdTokenAsync(string idToken, CancellationToken cancellationToken = default);
}
