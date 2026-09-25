namespace SelfStorageSystem.Application.Settings;

public class GoogleAuthSettings
{
    public const string SectionName = "GoogleAuthSettings";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
