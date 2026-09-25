namespace SelfStorageSystem.Application.Settings;

public class CorsSettings
{
    public const string SectionName = "CorsSettings";
    public const string PolicyName = "FrontendCorsPolicy";

    public string[] AllowedOrigins { get; set; } = [];
}
