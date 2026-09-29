using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.DTOs;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;

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
            var validationSettings = new GoogleJsonWebSignature.ValidationSettings();

            if (!string.IsNullOrWhiteSpace(_googleAuthSettings.ClientId))
            {
                validationSettings.Audience = new[] { _googleAuthSettings.ClientId };
            }

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, validationSettings);

            if (payload == null)
            {
                throw new InvalidOperationException("Google ID Token is invalid.");
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
            _logger.LogWarning(ex, "Invalid Google ID Token: {Message}", ex.Message);
            throw new InvalidOperationException("Google ID Token is invalid or has expired.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Google ID Token: {Message}", ex.Message);
            throw new InvalidOperationException($"Error validating Google ID Token: {ex.Message}", ex);
        }
    }
}
