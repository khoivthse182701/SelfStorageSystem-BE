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
        Error.Forbidden("Auth.AccountLocked", "Account has been locked. Please contact support administrator.");

    public static readonly Error AccountNotActivated =
        Error.Unauthorized("Auth.AccountNotActivated", "Account has not been activated. Please verify your OTP code via email.");

    // ── Registration ───────────────────────────────────────────────────────
    public static readonly Error EmailAlreadyInUse =
        Error.Conflict("Auth.EmailAlreadyInUse", "This email is already in use. Please log in or choose a different email.");

    public static readonly Error IdentityNumberAlreadyInUse =
        Error.Conflict("Auth.IdentityNumberAlreadyInUse", "Số CCCD này đã được đăng ký cho một tài khoản khác.");

    public static readonly Error AccountAlreadyActivated =
        Error.Conflict("Auth.AccountAlreadyActivated", "Your account is already activated. Please log in.");

    // ── Profile ────────────────────────────────────────────────────────────
    public static readonly Error IdentityNumberLocked =
        Error.Validation("Profile.IdentityNumberLocked", "Số CCCD đã được khóa cố định sau khi đăng ký và không thể chỉnh sửa.");

    public static readonly Error MonthlyProfileEditLimitReached =
        Error.Conflict("Profile.MonthlyEditLimitReached", "Bạn đã sử dụng hết tối đa 2 lần chỉnh sửa Họ tên / Số điện thoại trong tháng này.");

    public static readonly Error CurrentPasswordIncorrect =
        Error.Validation("Auth.CurrentPasswordIncorrect", "Mật khẩu hiện tại không chính xác.");

    public static readonly Error PasswordTooShort =
        Error.Validation("Auth.PasswordTooShort", "Mật khẩu mới phải có ít nhất 6 ký tự.");

    public static readonly Error NewPasswordMismatch =
        Error.Validation("Auth.NewPasswordMismatch", "Mật khẩu xác nhận không khớp với mật khẩu mới.");

    public static readonly Error NewPasswordSameAsOld =
        Error.Validation("Auth.NewPasswordSameAsOld", "Mật khẩu mới không được trùng với mật khẩu hiện tại.");

    public static readonly Error StaffIdentityLocked =
        Error.Forbidden("Profile.StaffIdentityLocked", "Họ tên và Số CCCD của nhân sự do Admin/HR quản lý và không thể tự chỉnh sửa.");

    // ── OTP ────────────────────────────────────────────────────────────────
    public static readonly Error OtpInvalid =
        Error.Validation("Auth.OtpInvalid", "Incorrect or expired OTP code.");

    public static readonly Error OtpInvalidOrExpired = OtpInvalid;

    // ── User ───────────────────────────────────────────────────────────────
    public static readonly Error UserNotFound =
        Error.NotFound("Auth.UserNotFound", "Account information not found for this email.");

    public static readonly Error InvalidGoogleToken =
        Error.Unauthorized("Auth.InvalidGoogleToken", "Google ID Token is invalid or has expired.");

    public static readonly Error UserTokenInvalid =
        Error.Unauthorized("Auth.UserTokenInvalid", "Cannot authenticate user identity from token.");

    // ── Dynamic OTP throttle errors (require runtime values) ───────────────
    public static Error OtpLockedOut(long remainingMinutes) =>
        Error.TooManyRequests("Auth.OtpLockedOut",
            $"Account is temporarily locked due to too many failed OTP attempts. Please try again after {remainingMinutes} minutes.");

    public static Error OtpCooldown(long remainingSeconds) =>
        Error.TooManyRequests("Auth.OtpCooldown",
            $"Please wait {remainingSeconds} seconds before requesting a new OTP code.");

    public static Error OtpHourlyLimitExceeded(int maxPerHour) =>
        Error.TooManyRequests("Auth.OtpHourlyLimitExceeded",
            $"You have exceeded the maximum OTP request limit per hour ({maxPerHour} times). Please try again later.");

    public static Error OtpMaxAttempts(int maxAttempts, int lockoutMinutes) =>
        Error.Validation("Auth.OtpMaxAttempts",
            $"You have entered incorrect OTP {maxAttempts} consecutive times. This OTP code has been cancelled and verification is temporarily locked for {lockoutMinutes} minutes.");

    public static Error OtpRemainingAttempts(int remaining) =>
        Error.Validation("Auth.OtpRemainingAttempts",
            $"Incorrect or expired OTP code. You have {remaining} attempt(s) remaining.");
}
