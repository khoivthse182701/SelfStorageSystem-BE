using SelfStorageSystem.Contracts.Auth;

namespace SelfStorageSystem.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<string> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default);
    Task<string> ResendOtpAsync(ResendOtpRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthUserDto> GetCurrentUserAsync(long userId, CancellationToken cancellationToken = default);
}
