namespace SelfStorageSystem.Application.Interfaces;

public interface IEmailService
{
    Task SendOtpEmailAsync(string toEmail, string fullName, string otpCode, int expiryMinutes, CancellationToken cancellationToken = default);
    Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
