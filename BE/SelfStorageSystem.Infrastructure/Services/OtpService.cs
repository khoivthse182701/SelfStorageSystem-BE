using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;

namespace SelfStorageSystem.Infrastructure.Services;

public class OtpService : IOtpService
{
    private readonly IMemoryCache _cache;
    private readonly OtpSettings _otpSettings;

    public OtpService(IMemoryCache cache, IOptions<OtpSettings> otpOptions)
    {
        _cache = cache;
        _otpSettings = otpOptions.Value;
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
    private static string GetOtpKey(string email) => $"OTP:{Normalize(email)}";
    private static string GetCooldownKey(string email) => $"OTP_COOLDOWN:{Normalize(email)}";
    private static string GetHourlyKey(string email) => $"OTP_HOURLY:{Normalize(email)}";
    private static string GetFailedKey(string email) => $"OTP_FAILED:{Normalize(email)}";
    private static string GetLockoutKey(string email) => $"OTP_LOCKOUT:{Normalize(email)}";

    public bool IsLockedOut(string email)
    {
        if (_cache.TryGetValue<DateTimeOffset>(GetLockoutKey(email), out var lockoutUntil))
        {
            return DateTimeOffset.UtcNow < lockoutUntil;
        }
        return false;
    }

    public int GetRemainingLockoutMinutes(string email)
    {
        if (_cache.TryGetValue<DateTimeOffset>(GetLockoutKey(email), out var lockoutUntil))
        {
            var remaining = lockoutUntil - DateTimeOffset.UtcNow;
            return remaining.TotalMinutes > 0 ? (int)Math.Ceiling(remaining.TotalMinutes) : 0;
        }
        return 0;
    }

    public bool IsInCooldown(string email)
    {
        if (_cache.TryGetValue<DateTimeOffset>(GetCooldownKey(email), out var cooldownUntil))
        {
            return DateTimeOffset.UtcNow < cooldownUntil;
        }
        return false;
    }

    public int GetRemainingCooldownSeconds(string email)
    {
        if (_cache.TryGetValue<DateTimeOffset>(GetCooldownKey(email), out var cooldownUntil))
        {
            var remaining = cooldownUntil - DateTimeOffset.UtcNow;
            return remaining.TotalSeconds > 0 ? (int)Math.Ceiling(remaining.TotalSeconds) : 0;
        }
        return 0;
    }

    public bool HasExceededHourlyLimit(string email)
    {
        if (_cache.TryGetValue<int>(GetHourlyKey(email), out var count))
        {
            var limit = _otpSettings.MaxRequestsPerHour > 0 ? _otpSettings.MaxRequestsPerHour : 5;
            return count >= limit;
        }
        return false;
    }

    public string GenerateAndStoreOtp(string email)
    {
        var normEmail = Normalize(email);

        // 1. Generate code
        var length = _otpSettings.CodeLength > 0 ? _otpSettings.CodeLength : 6;
        var min = (int)Math.Pow(10, length - 1);
        var max = (int)Math.Pow(10, length);
        var code = RandomNumberGenerator.GetInt32(min, max).ToString($"D{length}");
        var expiration = TimeSpan.FromMinutes(_otpSettings.ExpirationMinutes > 0 ? _otpSettings.ExpirationMinutes : 10);
        _cache.Set(GetOtpKey(normEmail), code, expiration);

        // 2. Set resend cooldown
        var cooldownSec = _otpSettings.ResendCooldownSeconds > 0 ? _otpSettings.ResendCooldownSeconds : 60;
        var cooldownUntil = DateTimeOffset.UtcNow.AddSeconds(cooldownSec);
        _cache.Set(GetCooldownKey(normEmail), cooldownUntil, TimeSpan.FromSeconds(cooldownSec));

        // 3. Increment hourly counter
        var currentHourly = 0;
        if (_cache.TryGetValue<int>(GetHourlyKey(normEmail), out var existingCount))
        {
            currentHourly = existingCount;
        }
        _cache.Set(GetHourlyKey(normEmail), currentHourly + 1, TimeSpan.FromHours(1));

        // 4. Reset failed attempts for the new code
        _cache.Remove(GetFailedKey(normEmail));

        return code;
    }

    public bool VerifyOtp(string email, string otpCode)
    {
        var normEmail = Normalize(email);

        if (_cache.TryGetValue<string>(GetOtpKey(normEmail), out var storedCode))
        {
            return string.Equals(storedCode?.Trim(), otpCode.Trim(), StringComparison.Ordinal);
        }

        return false;
    }

    public int RecordFailedAttempt(string email)
    {
        var normEmail = Normalize(email);
        var attempts = 1;
        if (_cache.TryGetValue<int>(GetFailedKey(normEmail), out var existing))
        {
            attempts = existing + 1;
        }

        // Store failed count with same expiry as OTP
        var expiration = TimeSpan.FromMinutes(_otpSettings.ExpirationMinutes > 0 ? _otpSettings.ExpirationMinutes : 10);
        _cache.Set(GetFailedKey(normEmail), attempts, expiration);

        // Check if exceeded max failed attempts -> lock out
        var maxAttempts = _otpSettings.MaxFailedAttempts > 0 ? _otpSettings.MaxFailedAttempts : 5;
        if (attempts >= maxAttempts)
        {
            // Invalidate the current OTP code so it cannot be used anymore
            _cache.Remove(GetOtpKey(normEmail));

            // Set lockout
            var lockoutMin = _otpSettings.LockoutMinutes > 0 ? _otpSettings.LockoutMinutes : 15;
            _cache.Set(GetLockoutKey(normEmail), DateTimeOffset.UtcNow.AddMinutes(lockoutMin), TimeSpan.FromMinutes(lockoutMin));
        }

        return attempts;
    }

    public bool HasExceededMaxAttempts(string email)
    {
        var normEmail = Normalize(email);
        if (IsLockedOut(normEmail))
        {
            return true;
        }
        var maxAttempts = _otpSettings.MaxFailedAttempts > 0 ? _otpSettings.MaxFailedAttempts : 5;
        if (_cache.TryGetValue<int>(GetFailedKey(normEmail), out var count))
        {
            return count >= maxAttempts;
        }
        return false;
    }

    public int GetRemainingAttempts(string email)
    {
        var normEmail = Normalize(email);
        if (IsLockedOut(normEmail))
        {
            return 0;
        }
        var maxAttempts = _otpSettings.MaxFailedAttempts > 0 ? _otpSettings.MaxFailedAttempts : 5;
        if (_cache.TryGetValue<int>(GetFailedKey(normEmail), out var count))
        {
            return Math.Max(0, maxAttempts - count);
        }
        return maxAttempts;
    }

    public void InvalidateOtp(string email)
    {
        var normEmail = Normalize(email);
        _cache.Remove(GetOtpKey(normEmail));
        _cache.Remove(GetFailedKey(normEmail));
    }
}
