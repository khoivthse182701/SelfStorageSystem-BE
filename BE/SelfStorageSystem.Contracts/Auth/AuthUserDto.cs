namespace SelfStorageSystem.Contracts.Auth;

public class AuthUserDto
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public List<string> Roles { get; set; } = new();
    public string Status { get; set; } = string.Empty;
}
