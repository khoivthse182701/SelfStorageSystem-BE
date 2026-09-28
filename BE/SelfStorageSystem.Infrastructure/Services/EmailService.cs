using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;

namespace SelfStorageSystem.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly MailSettings _mailSettings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<MailSettings> mailOptions, ILogger<EmailService> logger)
    {
        _mailSettings = mailOptions.Value;
        _logger = logger;
    }

    public async Task SendOtpEmailAsync(
        string toEmail,
        string fullName,
        string otpCode,
        int expiryMinutes,
        CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        var fromAddress = string.IsNullOrWhiteSpace(_mailSettings.SenderEmail) 
            ? _mailSettings.Username.Trim() 
            : _mailSettings.SenderEmail.Trim();
        message.From.Add(new MailboxAddress(_mailSettings.SenderName, fromAddress));
        message.To.Add(new MailboxAddress(fullName, toEmail.Trim()));
        message.Subject = $"[{_mailSettings.SenderName}] Mã xác thực OTP đăng ký: {otpCode}";

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; background-color: #f4f6f8; margin: 0; padding: 20px; }}
        .container {{ max-width: 560px; margin: auto; background: #ffffff; border-radius: 10px; padding: 30px; box-shadow: 0 4px 12px rgba(0,0,0,0.08); }}
        .header {{ text-align: center; border-bottom: 2px solid #0056b3; padding-bottom: 15px; margin-bottom: 25px; }}
        .header h1 {{ color: #0056b3; margin: 0; font-size: 24px; }}
        .greeting {{ font-size: 16px; color: #333333; margin-bottom: 15px; }}
        .otp-box {{ background-color: #f0f7ff; border: 2px dashed #0056b3; border-radius: 8px; text-align: center; padding: 20px; margin: 25px 0; }}
        .otp-code {{ font-size: 36px; font-weight: bold; letter-spacing: 8px; color: #0056b3; font-family: Consolas, monospace; }}
        .note {{ font-size: 14px; color: #666666; line-height: 1.6; }}
        .warning {{ font-size: 13px; color: #d9534f; margin-top: 15px; }}
        .footer {{ text-align: center; margin-top: 30px; padding-top: 15px; border-top: 1px solid #eeeeee; font-size: 12px; color: #999999; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>{_mailSettings.SenderName}</h1>
        </div>
        <p class='greeting'>Xin chào <strong>{fullName}</strong>,</p>
        <p class='note'>Cảm ơn bạn đã đăng ký tài khoản tại hệ thống {_mailSettings.SenderName}. Dưới đây là mã xác thực OTP của bạn:</p>
        
        <div class='otp-box'>
            <div class='otp-code'>{otpCode}</div>
        </div>

        <p class='note'>Mã xác thực có hiệu lực trong vòng <strong>{expiryMinutes} phút</strong>. Vui lòng nhập mã này vào trang xác thực để hoàn tất kích hoạt tài khoản.</p>
        <p class='warning'>* Lưu ý: Không chia sẻ mã OTP này cho bất kỳ ai nhằm bảo mật thông tin tài khoản của bạn.</p>

        <div class='footer'>
            <p>Email này được gửi tự động từ hệ thống {_mailSettings.SenderName}. Vui lòng không phản hồi thư này.</p>
        </div>
    </div>
</body>
</html>"
        };

        message.Body = bodyBuilder.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            var secureSocketOptions = _mailSettings.Port == 465 
                ? SecureSocketOptions.SslOnConnect 
                : SecureSocketOptions.StartTls;

            await client.ConnectAsync(_mailSettings.Host.Trim(), _mailSettings.Port, secureSocketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_mailSettings.Username) && !string.IsNullOrWhiteSpace(_mailSettings.Password))
            {
                var username = _mailSettings.Username.Trim();
                var password = _mailSettings.Password.Trim().Replace(" ", "");
                await client.AuthenticateAsync(username, password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Sent OTP email successfully to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OTP email to {Email}: {Message}", toEmail, ex.Message);
            throw new InvalidOperationException($"Không thể gửi email OTP qua Gmail: {ex.Message}", ex);
        }
    }

    public async Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new MimeMessage();
            var fromAddress = string.IsNullOrWhiteSpace(_mailSettings.SenderEmail) 
                ? _mailSettings.Username.Trim() 
                : _mailSettings.SenderEmail.Trim();
            message.From.Add(new MailboxAddress(_mailSettings.SenderName, fromAddress));
            message.To.Add(new MailboxAddress(toEmail.Trim(), toEmail.Trim()));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            var secureSocketOptions = _mailSettings.Port == 465 
                ? SecureSocketOptions.SslOnConnect 
                : SecureSocketOptions.StartTls;

            await client.ConnectAsync(_mailSettings.Host.Trim(), _mailSettings.Port, secureSocketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_mailSettings.Username) && !string.IsNullOrWhiteSpace(_mailSettings.Password))
            {
                var username = _mailSettings.Username.Trim();
                var password = _mailSettings.Password.Trim().Replace(" ", "");
                await client.AuthenticateAsync(username, password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            _logger.LogInformation("Sent email successfully to {Email} with subject {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}: {Message}", toEmail, ex.Message);
        }
    }
}
