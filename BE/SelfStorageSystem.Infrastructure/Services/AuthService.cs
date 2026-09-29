using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.DTOs;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Auth;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly SelfStorageDbContext _dbContext;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IEmailService _emailService;
    private readonly IOtpService _otpService;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly OtpSettings _otpSettings;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        SelfStorageDbContext dbContext,
        IJwtTokenService jwtTokenService,
        IEmailService emailService,
        IOtpService otpService,
        IGoogleAuthService googleAuthService,
        IOptions<OtpSettings> otpOptions,
        IOptions<JwtSettings> jwtOptions,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _jwtTokenService = jwtTokenService;
        _emailService = emailService;
        _otpService = otpService;
        _googleAuthService = googleAuthService;
        _otpSettings = otpOptions.Value;
        _jwtSettings = jwtOptions.Value;
        _logger = logger;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .Include(u => u.CustomerProfile)
            .Include(u => u.EmployeeProfile)
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user == null)
        {
            throw new UnauthorizedAccessException("Email hoặc mật khẩu không chính xác.");
        }

        bool isPasswordValid = false;
        try
        {
            isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        }
        catch
        {
            isPasswordValid = false;
        }

        if (!isPasswordValid)
        {
            throw new UnauthorizedAccessException("Email hoặc mật khẩu không chính xác.");
        }

        if (string.Equals(user.Status, UserStatusConstants.Locked, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.");
        }

        if (string.Equals(user.Status, UserStatusConstants.Disabled, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Tài khoản chưa được kích hoạt. Vui lòng xác thực mã OTP qua email.");
        }

        var now = DateTimeOffset.UtcNow;
        user.LastLoginAt = now;
        user.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return BuildAuthResponse(user);
    }

    public async Task<string> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 1. Anti-spam checks
        if (_otpService.IsLockedOut(normalizedEmail))
        {
            var remainingMin = _otpService.GetRemainingLockoutMinutes(normalizedEmail);
            throw new InvalidOperationException($"Tài khoản tạm thời bị khóa do nhập sai OTP quá nhiều lần. Vui lòng thử lại sau {remainingMin} phút.");
        }

        if (_otpService.IsInCooldown(normalizedEmail))
        {
            var cooldown = _otpService.GetRemainingCooldownSeconds(normalizedEmail);
            throw new InvalidOperationException($"Vui lòng đợi {cooldown} giây trước khi yêu cầu mã OTP mới.");
        }

        if (_otpService.HasExceededHourlyLimit(normalizedEmail))
        {
            throw new InvalidOperationException($"Bạn đã vượt quá giới hạn yêu cầu mã OTP trong 1 giờ ({_otpSettings.MaxRequestsPerHour} lần). Vui lòng thử lại sau.");
        }

        var existingUser = await _dbContext.Users
            .Include(u => u.CustomerProfile)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        if (existingUser != null)
        {
            if (string.Equals(existingUser.Status, UserStatusConstants.Active, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Email này đã được sử dụng. Vui lòng đăng nhập hoặc chọn email khác.");
            }

            if (string.Equals(existingUser.Status, UserStatusConstants.Locked, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Tài khoản này đã bị khóa. Vui lòng liên hệ quản trị viên.");
            }

            // Update existing unverified account with new registration details
            existingUser.PasswordHash = hashedPassword;
            existingUser.PhoneNumber = request.PhoneNumber;
            existingUser.UpdatedAt = now;

            if (existingUser.CustomerProfile != null)
            {
                existingUser.CustomerProfile.FullName = request.FullName.Trim();
                existingUser.CustomerProfile.UpdatedAt = now;
            }
            else
            {
                existingUser.CustomerProfile = new CustomerProfile
                {
                    FullName = request.FullName.Trim(),
                    CreatedAt = now,
                    UpdatedAt = now
                };
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var customerRole = await _dbContext.Roles
                .FirstOrDefaultAsync(r => r.Code == RoleConstants.Customer, cancellationToken);

            var newUser = new User
            {
                Email = normalizedEmail,
                PhoneNumber = request.PhoneNumber,
                PasswordHash = hashedPassword,
                Status = UserStatusConstants.Disabled,
                CreatedAt = now,
                UpdatedAt = now
            };

            newUser.CustomerProfile = new CustomerProfile
            {
                FullName = request.FullName.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };

            if (customerRole != null)
            {
                newUser.UserRoleUsers.Add(new UserRole
                {
                    Role = customerRole,
                    RoleId = customerRole.Id,
                    GrantedAt = now
                });
            }

            _dbContext.Users.Add(newUser);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Generate and send OTP
        var otpCode = _otpService.GenerateAndStoreOtp(normalizedEmail);
        await _emailService.SendOtpEmailAsync(
            normalizedEmail,
            request.FullName.Trim(),
            otpCode,
            _otpSettings.ExpirationMinutes,
            cancellationToken);

        return "Đăng ký thành công! Mã OTP xác nhận đã được gửi đến email của bạn.";
    }

    public async Task<AuthResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 1. Lockout check
        if (_otpService.IsLockedOut(normalizedEmail))
        {
            var remainingMin = _otpService.GetRemainingLockoutMinutes(normalizedEmail);
            throw new InvalidOperationException($"Tài khoản tạm thời bị khóa do nhập sai OTP quá nhiều lần. Vui lòng thử lại sau {remainingMin} phút.");
        }

        // 2. Verify OTP
        var isValidOtp = _otpService.VerifyOtp(normalizedEmail, request.OtpCode);
        if (!isValidOtp)
        {
            _otpService.RecordFailedAttempt(normalizedEmail);
            if (_otpService.HasExceededMaxAttempts(normalizedEmail))
            {
                throw new InvalidOperationException($"Bạn đã nhập sai mã OTP {_otpSettings.MaxFailedAttempts} lần liên tiếp. Mã OTP này đã bị hủy và tính năng xác thực tạm thời bị khóa {_otpSettings.LockoutMinutes} phút.");
            }

            var remaining = _otpService.GetRemainingAttempts(normalizedEmail);
            throw new InvalidOperationException($"Mã OTP không chính xác hoặc đã hết hạn. Bạn còn {remaining} lần thử.");
        }

        var user = await _dbContext.Users
            .Include(u => u.CustomerProfile)
            .Include(u => u.EmployeeProfile)
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user == null)
        {
            throw new KeyNotFoundException("Không tìm thấy thông tin tài khoản cho email này.");
        }

        var now = DateTimeOffset.UtcNow;
        user.Status = UserStatusConstants.Active;
        user.LastLoginAt = now;
        user.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _otpService.InvalidateOtp(normalizedEmail);

        return BuildAuthResponse(user);
    }

    public async Task<string> ResendOtpAsync(ResendOtpRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .Include(u => u.CustomerProfile)
            .Include(u => u.EmployeeProfile)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user == null)
        {
            throw new KeyNotFoundException("Không tìm thấy thông tin tài khoản với email này.");
        }

        if (string.Equals(user.Status, UserStatusConstants.Active, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Tài khoản của bạn đã được kích hoạt trước đó. Vui lòng đăng nhập.");
        }

        // Anti-spam checks
        if (_otpService.IsLockedOut(normalizedEmail))
        {
            var remainingMin = _otpService.GetRemainingLockoutMinutes(normalizedEmail);
            throw new InvalidOperationException($"Tài khoản tạm thời bị khóa do nhập sai OTP quá nhiều lần. Vui lòng thử lại sau {remainingMin} phút.");
        }

        if (_otpService.IsInCooldown(normalizedEmail))
        {
            var cooldown = _otpService.GetRemainingCooldownSeconds(normalizedEmail);
            throw new InvalidOperationException($"Vui lòng đợi {cooldown} giây trước khi yêu cầu gửi lại mã OTP mới.");
        }

        if (_otpService.HasExceededHourlyLimit(normalizedEmail))
        {
            throw new InvalidOperationException($"Bạn đã vượt quá giới hạn yêu cầu mã OTP trong 1 giờ ({_otpSettings.MaxRequestsPerHour} lần). Vui lòng thử lại sau.");
        }

        var fullName = user.CustomerProfile?.FullName ?? user.EmployeeProfile?.FullName ?? user.Email;
        var otpCode = _otpService.GenerateAndStoreOtp(normalizedEmail);

        await _emailService.SendOtpEmailAsync(
            normalizedEmail,
            fullName,
            otpCode,
            _otpSettings.ExpirationMinutes,
            cancellationToken);

        return "Mã OTP mới đã được gửi đến email của bạn.";
    }

    public async Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken cancellationToken = default)
    {
        var googleUser = await _googleAuthService.ValidateIdTokenAsync(request.IdToken, cancellationToken);
        var normalizedEmail = googleUser.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .Include(u => u.CustomerProfile)
            .Include(u => u.EmployeeProfile)
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        var now = DateTimeOffset.UtcNow;

        if (user == null)
        {
            var customerRole = await _dbContext.Roles
                .FirstOrDefaultAsync(r => r.Code == RoleConstants.Customer, cancellationToken);

            user = new User
            {
                Email = normalizedEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
                Status = UserStatusConstants.Active,
                CreatedAt = now,
                UpdatedAt = now,
                LastLoginAt = now
            };

            user.CustomerProfile = new CustomerProfile
            {
                FullName = string.IsNullOrWhiteSpace(googleUser.Name) ? normalizedEmail : googleUser.Name.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };

            if (customerRole != null)
            {
                user.UserRoleUsers.Add(new UserRole
                {
                    Role = customerRole,
                    RoleId = customerRole.Id,
                    GrantedAt = now
                });
            }

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            if (string.Equals(user.Status, UserStatusConstants.Locked, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.");
            }

            if (string.Equals(user.Status, UserStatusConstants.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                // Google already verified the email
                user.Status = UserStatusConstants.Active;
            }

            user.LastLoginAt = now;
            user.UpdatedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return BuildAuthResponse(user);
    }

    public async Task<AuthUserDto> GetCurrentUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.CustomerProfile)
            .Include(u => u.EmployeeProfile)
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw new KeyNotFoundException("Không tìm thấy người dùng.");
        }

        return MapToUserDto(user);
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        var roles = user.UserRoleUsers
            .Where(ur => ur.Role != null)
            .Select(ur => ur.Role.Code)
            .ToList();

        if (roles.Count == 0)
        {
            roles.Add(RoleConstants.Customer);
        }

        var fullName = user.CustomerProfile?.FullName ?? user.EmployeeProfile?.FullName ?? user.Email;
        var token = _jwtTokenService.GenerateToken(user, roles, fullName);

        return new AuthResponse
        {
            AccessToken = token,
            TokenType = AuthConstants.BearerTokenType,
            ExpiresInMinutes = _jwtSettings.AccessTokenMinutes > 0 ? _jwtSettings.AccessTokenMinutes : AuthConstants.DefaultAccessTokenMinutes,
            User = MapToUserDto(user, roles, fullName)
        };
    }

    private static AuthUserDto MapToUserDto(User user, List<string>? roles = null, string? fullName = null)
    {
        roles ??= user.UserRoleUsers
            .Where(ur => ur.Role != null)
            .Select(ur => ur.Role.Code)
            .ToList();

        if (roles.Count == 0)
        {
            roles.Add(RoleConstants.Customer);
        }

        fullName ??= user.CustomerProfile?.FullName ?? user.EmployeeProfile?.FullName ?? user.Email;

        return new AuthUserDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = fullName,
            PhoneNumber = user.PhoneNumber,
            Roles = roles,
            Status = user.Status
        };
    }
}
