namespace SelfStorageSystem.Domain.Errors;

using SelfStorageSystem.Domain.Common;

/// <summary>
/// Typed error codes for authentication and user account operations.
/// </summary>
public static class AuthErrors
{
    // ── Login ──────────────────────────────────────────────────────────────
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Auth.InvalidCredentials", "Incorrect email or password.");

    public static readonly Error AccountLocked =
        Error.Unauthorized("Auth.AccountLocked", "Account has been locked. Please contact support administrator.");

    public static readonly Error AccountNotActivated =
        Error.Unauthorized("Auth.AccountNotActivated", "Account has not been activated. Please verify your OTP code via email.");

    // ── Registration ───────────────────────────────────────────────────────
    public static readonly Error EmailAlreadyInUse =
        Error.Conflict("Auth.EmailAlreadyInUse", "This email is already in use. Please log in or choose a different email.");

    public static readonly Error AccountAlreadyActivated =
        Error.Conflict("Auth.AccountAlreadyActivated", "Your account is already activated. Please log in.");

    // ── OTP ────────────────────────────────────────────────────────────────
    public static readonly Error OtpInvalidOrExpired =
        Error.Validation("Auth.OtpInvalidOrExpired", "Incorrect or expired OTP code.");

    // ── User ───────────────────────────────────────────────────────────────
    public static readonly Error UserNotFound =
        Error.NotFound("Auth.UserNotFound", "Account information not found for this email.");

    public static readonly Error UserTokenInvalid =
        Error.Unauthorized("Auth.UserTokenInvalid", "Cannot authenticate user identity from token.");

    // ── Dynamic OTP throttle errors (require runtime values) ───────────────
    public static Error OtpLockedOut(long remainingMinutes) =>
        Error.Validation("Auth.OtpLockedOut",
            $"Account is temporarily locked due to too many failed OTP attempts. Please try again after {remainingMinutes} minutes.");

    public static Error OtpCooldown(long remainingSeconds) =>
        Error.Validation("Auth.OtpCooldown",
            $"Please wait {remainingSeconds} seconds before requesting a new OTP code.");

    public static Error OtpHourlyLimitExceeded(int maxPerHour) =>
        Error.Validation("Auth.OtpHourlyLimitExceeded",
            $"You have exceeded the maximum OTP request limit per hour ({maxPerHour} times). Please try again later.");

    public static Error OtpMaxAttempts(int maxAttempts, int lockoutMinutes) =>
        Error.Validation("Auth.OtpMaxAttempts",
            $"You have entered incorrect OTP {maxAttempts} consecutive times. This OTP code has been cancelled and verification is temporarily locked for {lockoutMinutes} minutes.");

    public static Error OtpRemainingAttempts(int remaining) =>
        Error.Validation("Auth.OtpRemainingAttempts",
            $"Incorrect or expired OTP code. You have {remaining} attempt(s) remaining.");
}
