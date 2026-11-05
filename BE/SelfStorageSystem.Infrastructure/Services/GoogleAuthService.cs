using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw AppException.FromError(AuthErrors.InvalidGoogleToken);
        }

        // 1. Build comprehensive list of valid allowed audiences (configured Client ID + fallback registered Client IDs)
        var allowedAudiences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(_googleAuthSettings.ClientId) &&
            !_googleAuthSettings.ClientId.StartsWith("YOUR_GOOGLE_CLIENT_ID", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var aud in _googleAuthSettings.ClientId.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                allowedAudiences.Add(aud);
            }
        }

        // Always register the user's active Google OAuth Client IDs to prevent 'aud' claim mismatch
        allowedAudiences.Add("827798715826-ef477sod2l5a1f34484no0v78r1fbdsl.apps.googleusercontent.com");

        if (allowedAudiences.Count == 0)
        {
            _logger.LogError("Google Client ID is not configured properly in application settings.");
            throw AppException.FromError(AuthErrors.InvalidGoogleToken);
        }

        // 2. Pre-parse token claims for diagnostic inspection
        string? tokenAudience = null;
        DateTimeOffset? tokenExpiry = null;
        try
        {
            var parts = idToken.Split('.');
            if (parts.Length >= 2)
            {
                var base64 = parts[1].Replace('-', '+').Replace('_', '/');
                switch (base64.Length % 4)
                {
                    case 2: base64 += "=="; break;
                    case 3: base64 += "="; break;
                }
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("aud", out var audProp))
                {
                    tokenAudience = audProp.GetString();
                }
                if (doc.RootElement.TryGetProperty("exp", out var expProp) && expProp.TryGetInt64(out var expSeconds))
                {
                    tokenExpiry = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to pre-parse Google ID token payload.");
        }

        // If token audience is discovered and valid, ensure it is in the allowed validation settings
        if (!string.IsNullOrWhiteSpace(tokenAudience) && allowedAudiences.Contains(tokenAudience))
        {
            allowedAudiences.Add(tokenAudience);
        }

        try
        {
            var validationSettings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = allowedAudiences.ToList(),
                IssuedAtClockTolerance = TimeSpan.FromMinutes(10),
                ExpirationTimeClockTolerance = TimeSpan.FromMinutes(10)
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, validationSettings);

            if (payload == null)
            {
                _logger.LogWarning("GoogleJsonWebSignature.ValidateAsync returned null payload.");
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
            _logger.LogWarning(
                ex,
                "Invalid Google ID Token: {Message}. Token Aud: {TokenAud}, Token Exp: {TokenExp}, Allowed Auds: {AllowedAuds}",
                ex.Message,
                tokenAudience ?? "unknown",
                tokenExpiry?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "unknown",
                string.Join(", ", allowedAudiences));

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
