using System.ComponentModel.DataAnnotations;

namespace SelfStorageSystem.Contracts.Auth;

public class VerifyOtpRequest
{
    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mã OTP là bắt buộc.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có đúng 6 chữ số.")]
    public string OtpCode { get; set; } = string.Empty;
}
