namespace SelfStorageSystem.Application.Settings;

public class RateLimitingSettings
{
    public const string SectionName = "RateLimitingSettings";
    public const string AuthPolicyName = "AuthRateLimitPolicy";

    public int GlobalPermitLimit { get; set; } = 100;
    public int GlobalWindowSeconds { get; set; } = 60;

    public int AuthPermitLimit { get; set; } = 10;
    public int AuthWindowSeconds { get; set; } = 60;
}
