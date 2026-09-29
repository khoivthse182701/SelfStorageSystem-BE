using System.ComponentModel.DataAnnotations;

namespace SelfStorageSystem.Contracts.Auth;

public class GoogleLoginRequest
{
    [Required(ErrorMessage = "Google ID Token is required.")]
    public string IdToken { get; set; } = string.Empty;
}
