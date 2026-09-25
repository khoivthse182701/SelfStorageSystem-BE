using System.ComponentModel.DataAnnotations;

namespace SelfStorageSystem.Contracts.Auth;

public class RegisterRequest
{
    [Required(ErrorMessage = "Họ và tên là bắt buộc.")]
    [MaxLength(100, ErrorMessage = "Họ và tên tối đa 100 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
    [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
    public string Password { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    public string? PhoneNumber { get; set; }
}
