namespace SelfStorageSystem.Application.Interfaces;

public interface IOtpService
{
    string GenerateAndStoreOtp(string email);
    bool VerifyOtp(string email, string otpCode);
    void InvalidateOtp(string email);

    // Anti-spam helpers
    bool IsInCooldown(string email);
    int GetRemainingCooldownSeconds(string email);
    bool HasExceededHourlyLimit(string email);
    bool IsLockedOut(string email);
    int GetRemainingLockoutMinutes(string email);
    int RecordFailedAttempt(string email);
    bool HasExceededMaxAttempts(string email);
    int GetRemainingAttempts(string email);
}
