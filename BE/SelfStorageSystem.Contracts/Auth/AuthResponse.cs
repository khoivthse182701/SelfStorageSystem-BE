namespace SelfStorageSystem.Contracts.Auth;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresInMinutes { get; set; }
    public AuthUserDto User { get; set; } = null!;
}
