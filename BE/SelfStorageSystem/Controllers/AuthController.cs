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
    /// Đăng ký tài khoản mới và gửi mã OTP qua Gmail
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var message = await _authService.RegisterAsync(request, cancellationToken);
            return Ok(ApiResponse.Ok(message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xảy ra trong quá trình đăng ký: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("Đã xảy ra lỗi hệ thống trong quá trình xử lý đăng ký. Vui lòng thử lại sau."));
        }
    }

    /// <summary>
    /// Xác thực mã OTP gửi về Gmail để kích hoạt tài khoản
    /// </summary>
    [HttpPost("verify-otp")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.VerifyOtpAsync(request, cancellationToken);
            return Ok(ApiResponse<AuthResponse>.Ok(response, "Xác thực mã OTP thành công. Tài khoản của bạn đã được kích hoạt."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xác thực OTP: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("Đã xảy ra lỗi hệ thống khi xác thực OTP. Vui lòng thử lại sau."));
        }
    }

    /// <summary>
    /// Gửi lại mã OTP qua Gmail
    /// </summary>
    [HttpPost("resend-otp")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var message = await _authService.ResendOtpAsync(request, cancellationToken);
            return Ok(ApiResponse.Ok(message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi gửi lại mã OTP: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("Đã xảy ra lỗi hệ thống khi gửi lại mã OTP. Vui lòng thử lại sau."));
        }
    }

    /// <summary>
    /// Đăng nhập bằng Email và Password nhận JWT Access Token
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.LoginAsync(request, cancellationToken);
            return Ok(ApiResponse<AuthResponse>.Ok(response, "Đăng nhập thành công."));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi đăng nhập: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("Đã xảy ra lỗi hệ thống trong quá trình đăng nhập. Vui lòng thử lại sau."));
        }
    }

    /// <summary>
    /// Đăng nhập / Đăng ký bằng Google (sử dụng Google ID Token)
    /// </summary>
    [HttpPost("google-login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.GoogleLoginAsync(request, cancellationToken);
            return Ok(ApiResponse<AuthResponse>.Ok(response, "Đăng nhập bằng Google thành công."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi đăng nhập Google: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("Đã xảy ra lỗi hệ thống khi đăng nhập bằng Google. Vui lòng thử lại sau."));
        }
    }

    /// <summary>
    /// Lấy thông tin tài khoản hiện tại từ JWT token
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AuthUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(ApiResponse.Fail("Không thể xác thực thông tin người dùng từ token."));
        }

        try
        {
            var user = await _authService.GetCurrentUserAsync(userId, cancellationToken);
            return Ok(ApiResponse<AuthUserDto>.Ok(user, "Lấy thông tin người dùng thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lấy thông tin người dùng: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("Đã xảy ra lỗi hệ thống khi truy xuất thông tin tài khoản. Vui lòng thử lại sau."));
        }
    }
}
