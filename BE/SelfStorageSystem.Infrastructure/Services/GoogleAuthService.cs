using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.DTOs;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;

namespace SelfStorageSystem.Infrastructure.Services;

public class GoogleAuthService : IGoogleAuthService
{
    private readonly GoogleAuthSettings _googleAuthSettings;
    private readonly ILogger<GoogleAuthService> _logger;

    public GoogleAuthService(IOptions<GoogleAuthSettings> googleOptions, ILogger<GoogleAuthService> logger)
    {
        _googleAuthSettings = googleOptions.Value;
        _logger = logger;
    }

    public async Task<GoogleUserInfo> ValidateIdTokenAsync(string idToken, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_googleAuthSettings.ClientId) ||
                _googleAuthSettings.ClientId.StartsWith("YOUR_GOOGLE_CLIENT_ID", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Google Client ID is not configured properly in application settings.");
                throw AppException.FromError(AuthErrors.InvalidGoogleToken);
            }

            var validationSettings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _googleAuthSettings.ClientId }
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, validationSettings);

            if (payload == null)
            {
                throw AppException.FromError(AuthErrors.InvalidGoogleToken);
            }

            return new GoogleUserInfo
            {
                Subject = payload.Subject,
                Email = payload.Email,
                Name = payload.Name,
                Picture = payload.Picture
            };
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, SelfStorageSystem.Domain.Constants.AuthLogMessages.InvalidGoogleToken, ex.Message);
            throw AppException.FromError(AuthErrors.InvalidGoogleToken);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, SelfStorageSystem.Domain.Constants.AuthLogMessages.VerifyGoogleTokenError, ex.Message);
            throw AppException.FromError(AuthErrors.InvalidGoogleToken);
        }
    }
}
