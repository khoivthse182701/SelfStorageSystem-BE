using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Auth;
using SelfStorageSystem.Contracts.Common;

namespace SelfStorageSystem.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly OtpSettings _otpSettings;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        ILogger<AuthController> logger,
        IOptions<OtpSettings>? otpOptions = null)
    {
        _authService = authService;
        _logger = logger;
        _otpSettings = otpOptions?.Value ?? new OtpSettings();
    }

    /// <summary>
    /// Register a new account and send OTP verification code via email.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var message = await _authService.RegisterAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(message));
    }

    /// <summary>
    /// Verify OTP code sent via email to activate the account.
    /// </summary>
    [HttpPost("verify-otp")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.VerifyOtpAsync(request, cancellationToken);
        return Ok(ApiResponse<AuthResponse>.Ok(response, "OTP verification successful. Your account has been activated."));
    }

    /// <summary>
    /// Resend OTP verification code via email.
    /// </summary>
    [HttpPost("resend-otp")]
    [ProducesResponseType(typeof(ApiResponse<ResendOtpResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request, CancellationToken cancellationToken)
    {
        var message = await _authService.ResendOtpAsync(request, cancellationToken);
        return Ok(ApiResponse<ResendOtpResponse>.Ok(new ResendOtpResponse(_otpSettings.ResendCooldownSeconds), message));
    }

    /// <summary>
    /// Login with Email and Password to receive JWT Access Token.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.LoginAsync(request, cancellationToken, GetClientIp(), GetUserAgent());
        return Ok(ApiResponse<AuthResponse>.Ok(response, "Login successful."));
    }

    /// <summary>
    /// Login / Register with Google (using Google ID Token).
    /// </summary>
    [HttpPost("google-login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.GoogleLoginAsync(request, cancellationToken, GetClientIp(), GetUserAgent());
        return Ok(ApiResponse<AuthResponse>.Ok(response, "Google login successful."));
    }

    /// <summary>
    /// Get current authenticated user profile from JWT token.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AuthUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot authenticate user identity from token."));
        }

        var user = await _authService.GetCurrentUserAsync(userId, cancellationToken);
        return Ok(ApiResponse<AuthUserDto>.Ok(user, "User profile retrieved successfully."));
    }

    /// <summary>
    /// Get customer profile details including locked CCCD, monthly edit quota, and negative deposit debt alerts.
    /// </summary>
    [HttpGet("profile")]
    [HttpGet("/api/v1/profile")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CustomerProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot authenticate user identity from token."));
        }

        var profile = await _authService.GetProfileAsync(userId, cancellationToken);
        return Ok(ApiResponse<CustomerProfileDto>.Ok(profile, "Profile retrieved successfully."));
    }

    /// <summary>
    /// Update customer profile (FullName and PhoneNumber up to 2 times/month; CCCD is locked).
    /// </summary>
    [HttpPut("profile")]
    [HttpPut("/api/v1/profile")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CustomerProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateCustomerProfileRequest request, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot authenticate user identity from token."));
        }

        var profile = await _authService.UpdateProfileAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<CustomerProfileDto>.Ok(profile, "Profile updated successfully."));
    }

    /// <summary>
    /// Change password for authenticated user.
    /// </summary>
    [HttpPost("change-password")]
    [HttpPost("/api/v1/change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot authenticate user identity from token."));
        }

        await _authService.ChangePasswordAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<string>.Ok("Mật khẩu đã được thay đổi thành công.", "Password changed successfully."));
    }

    private string? GetClientIp()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        var ip = HttpContext.Connection.RemoteIpAddress;
        if (ip == null) return null;
        return ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4().ToString() : ip.ToString();
    }

    private string? GetUserAgent()
    {
        var ua = Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(ua) ? null : ua;
    }
}
