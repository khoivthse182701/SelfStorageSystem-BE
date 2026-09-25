using System.ComponentModel.DataAnnotations;

namespace SelfStorageSystem.Contracts.Auth;

public class GoogleLoginRequest
{
    [Required(ErrorMessage = "Google ID Token là bắt buộc.")]
    public string IdToken { get; set; } = string.Empty;
}
