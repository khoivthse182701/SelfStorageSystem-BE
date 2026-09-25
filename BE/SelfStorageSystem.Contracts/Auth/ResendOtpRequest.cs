using System.ComponentModel.DataAnnotations;

namespace SelfStorageSystem.Contracts.Auth;

public class ResendOtpRequest
{
    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;
}
