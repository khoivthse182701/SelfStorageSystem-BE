using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Auth;
using SelfStorageSystem.Contracts.Common;

namespace SelfStorageSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(RateLimitingSettings.AuthPolicyName)]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
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
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request, CancellationToken cancellationToken)
    {
        var message = await _authService.ResendOtpAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(message));
    }

    /// <summary>
    /// Login with Email and Password to receive JWT Access Token.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.LoginAsync(request, cancellationToken);
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
        var response = await _authService.GoogleLoginAsync(request, cancellationToken);
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
}
