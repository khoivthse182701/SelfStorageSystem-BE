namespace SelfStorageSystem.Application.DTOs;

public class GoogleUserInfo
{
    public string Subject { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Picture { get; set; }
}
