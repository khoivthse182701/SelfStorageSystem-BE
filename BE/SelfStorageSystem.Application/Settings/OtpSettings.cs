namespace SelfStorageSystem.Application.Settings;

public class OtpSettings
{
    public const string SectionName = "OtpSettings";

    public int ExpirationMinutes { get; set; } = 10;
    public int CodeLength { get; set; } = 6;
    public int ResendCooldownSeconds { get; set; } = 60;
    public int MaxFailedAttempts { get; set; } = 5;
    public int MaxRequestsPerHour { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}
