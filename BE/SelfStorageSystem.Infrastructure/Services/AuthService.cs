using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.DTOs;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Auth;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
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

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default, string? ipAddress = null, string? userAgent = null)
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
            await RecordLoginAsync(null, normalizedEmail, LoginHistoryResultConstants.Failed, ipAddress, userAgent, cancellationToken);
            throw AppException.FromError(AuthErrors.InvalidCredentials);
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
            await RecordLoginAsync(user.Id, normalizedEmail, LoginHistoryResultConstants.Failed, ipAddress, userAgent, cancellationToken);
            throw AppException.FromError(AuthErrors.InvalidCredentials);
        }

        if (string.Equals(user.Status, UserStatusConstants.Locked, StringComparison.OrdinalIgnoreCase))
        {
            await RecordLoginAsync(user.Id, normalizedEmail, LoginHistoryResultConstants.Locked, ipAddress, userAgent, cancellationToken);
            throw AppException.FromError(AuthErrors.AccountLocked);
        }

        if (string.Equals(user.Status, UserStatusConstants.Disabled, StringComparison.OrdinalIgnoreCase))
        {
            await RecordLoginAsync(user.Id, normalizedEmail, LoginHistoryResultConstants.Blocked, ipAddress, userAgent, cancellationToken);
            throw AppException.FromError(AuthErrors.AccountNotActivated);
        }

        var now = DateTimeOffset.UtcNow;
        user.LastLoginAt = now;
        user.UpdatedAt = now;
        _dbContext.LoginHistories.Add(BuildLoginHistory(user.Id, normalizedEmail, LoginHistoryResultConstants.Succeeded, ipAddress, userAgent, now));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, cancellationToken);
    }

    public async Task<string> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 1. Anti-spam checks
        if (_otpService.IsLockedOut(normalizedEmail))
        {
            var remainingMin = _otpService.GetRemainingLockoutMinutes(normalizedEmail);
            throw AppException.FromError(AuthErrors.OtpLockedOut(remainingMin));
        }

        if (_otpService.IsInCooldown(normalizedEmail))
        {
            var cooldown = _otpService.GetRemainingCooldownSeconds(normalizedEmail);
            throw AppException.FromError(AuthErrors.OtpCooldown(cooldown));
        }

        if (_otpService.HasExceededHourlyLimit(normalizedEmail))
        {
            throw AppException.FromError(AuthErrors.OtpHourlyLimitExceeded(_otpSettings.MaxRequestsPerHour));
        }

        var existingUser = await _dbContext.Users
            .Include(u => u.CustomerProfile)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        var normalizedIdentity = request.IdentityNumber?.Trim() ?? string.Empty;
        var normalizedPhone = request.PhoneNumber?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(normalizedIdentity))
        {
            var cccdConflict = await _dbContext.CustomerProfiles
                .AnyAsync(cp => cp.IdentityNumber == normalizedIdentity && (existingUser == null || cp.UserId != existingUser.Id), cancellationToken);
            if (cccdConflict)
            {
                throw AppException.FromError(AuthErrors.IdentityNumberAlreadyInUse);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        if (existingUser != null)
        {
            if (string.Equals(existingUser.Status, UserStatusConstants.Active, StringComparison.OrdinalIgnoreCase))
            {
                throw AppException.FromError(AuthErrors.EmailAlreadyInUse);
            }

            if (string.Equals(existingUser.Status, UserStatusConstants.Locked, StringComparison.OrdinalIgnoreCase))
            {
                throw AppException.FromError(AuthErrors.AccountLocked);
            }

            // Update existing unverified account with new registration details
            existingUser.PasswordHash = hashedPassword;
            existingUser.PhoneNumber = normalizedPhone;
            existingUser.UpdatedAt = now;

            if (existingUser.CustomerProfile != null)
            {
                existingUser.CustomerProfile.FullName = request.FullName.Trim();
                existingUser.CustomerProfile.IdentityNumber = normalizedIdentity;
                existingUser.CustomerProfile.UpdatedAt = now;
            }
            else
            {
                existingUser.CustomerProfile = new CustomerProfile
                {
                    FullName = request.FullName.Trim(),
                    IdentityNumber = normalizedIdentity,
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
                PhoneNumber = normalizedPhone,
                PasswordHash = hashedPassword,
                Status = UserStatusConstants.Disabled,
                CreatedAt = now,
                UpdatedAt = now
            };

            newUser.CustomerProfile = new CustomerProfile
            {
                FullName = request.FullName.Trim(),
                IdentityNumber = normalizedIdentity,
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

        return "Registration successful! Verification OTP code has been sent to your email.";
    }

    public async Task<AuthResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 1. Lockout check
        if (_otpService.IsLockedOut(normalizedEmail))
        {
            var remainingMin = _otpService.GetRemainingLockoutMinutes(normalizedEmail);
            throw AppException.FromError(AuthErrors.OtpLockedOut(remainingMin));
        }

        // 2. Verify OTP
        var isValidOtp = _otpService.VerifyOtp(normalizedEmail, request.OtpCode);
        if (!isValidOtp)
        {
            _otpService.RecordFailedAttempt(normalizedEmail);
            if (_otpService.HasExceededMaxAttempts(normalizedEmail))
            {
                throw AppException.FromError(AuthErrors.OtpMaxAttempts(_otpSettings.MaxFailedAttempts, _otpSettings.LockoutMinutes));
            }

            var remaining = _otpService.GetRemainingAttempts(normalizedEmail);
            throw AppException.FromError(AuthErrors.OtpRemainingAttempts(remaining));
        }

        var user = await _dbContext.Users
            .Include(u => u.CustomerProfile)
            .Include(u => u.EmployeeProfile)
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user == null)
        {
            throw AppException.FromError(AuthErrors.UserNotFound);
        }

        var now = DateTimeOffset.UtcNow;
        user.Status = UserStatusConstants.Active;
        user.LastLoginAt = now;
        user.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _otpService.InvalidateOtp(normalizedEmail);

        return await BuildAuthResponseAsync(user, cancellationToken);
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
            throw AppException.FromError(AuthErrors.UserNotFound);
        }

        if (string.Equals(user.Status, UserStatusConstants.Active, StringComparison.OrdinalIgnoreCase))
        {
            throw AppException.FromError(AuthErrors.AccountAlreadyActivated);
        }

        // Anti-spam checks
        if (_otpService.IsLockedOut(normalizedEmail))
        {
            var remainingMin = _otpService.GetRemainingLockoutMinutes(normalizedEmail);
            throw AppException.FromError(AuthErrors.OtpLockedOut(remainingMin));
        }

        if (_otpService.IsInCooldown(normalizedEmail))
        {
            var cooldown = _otpService.GetRemainingCooldownSeconds(normalizedEmail);
            throw AppException.FromError(AuthErrors.OtpCooldown(cooldown));
        }

        if (_otpService.HasExceededHourlyLimit(normalizedEmail))
        {
            throw AppException.FromError(AuthErrors.OtpHourlyLimitExceeded(_otpSettings.MaxRequestsPerHour));
        }

        var fullName = user.CustomerProfile?.FullName ?? user.EmployeeProfile?.FullName ?? user.Email;
        var otpCode = _otpService.GenerateAndStoreOtp(normalizedEmail);

        await _emailService.SendOtpEmailAsync(
            normalizedEmail,
            fullName,
            otpCode,
            _otpSettings.ExpirationMinutes,
            cancellationToken);

        return "A new OTP code has been sent to your email.";
    }

    public async Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken cancellationToken = default, string? ipAddress = null, string? userAgent = null)
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

            _dbContext.LoginHistories.Add(BuildLoginHistory(user.Id, normalizedEmail, LoginHistoryResultConstants.Succeeded, ipAddress, userAgent, now));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            if (string.Equals(user.Status, UserStatusConstants.Locked, StringComparison.OrdinalIgnoreCase))
            {
                await RecordLoginAsync(user.Id, normalizedEmail, LoginHistoryResultConstants.Locked, ipAddress, userAgent, cancellationToken);
                throw AppException.FromError(AuthErrors.AccountLocked);
            }

            if (string.Equals(user.Status, UserStatusConstants.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                // Google already verified the email
                user.Status = UserStatusConstants.Active;
            }

            user.LastLoginAt = now;
            user.UpdatedAt = now;
            _dbContext.LoginHistories.Add(BuildLoginHistory(user.Id, normalizedEmail, LoginHistoryResultConstants.Succeeded, ipAddress, userAgent, now));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return await BuildAuthResponseAsync(user, cancellationToken);
    }

    private static LoginHistory BuildLoginHistory(long? userId, string? email, string result, string? ipAddress, string? userAgent, DateTimeOffset occurredAt)
    {
        return new LoginHistory
        {
            UserId = userId,
            AttemptedEmail = Truncate(email, 255),
            Result = result,
            IpAddress = Truncate(ipAddress, 45),
            UserAgent = Truncate(userAgent, 255),
            OccurredAt = occurredAt
        };
    }

    /// <summary>
    /// Persists a login attempt. Failures are logged and swallowed so they never block authentication.
    /// </summary>
    private async Task RecordLoginAsync(long? userId, string? email, string result, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        try
        {
            _dbContext.LoginHistories.Add(BuildLoginHistory(userId, email, result, ipAddress, userAgent, DateTimeOffset.UtcNow));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record login history for {Email}", email);
        }
    }

    private static string? Truncate(string? value, int maxLength)
        => value == null || value.Length <= maxLength ? value : value[..maxLength];

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
            throw AppException.FromError(AuthErrors.UserNotFound);
        }

        return await MapToUserDtoAsync(user, cancellationToken: cancellationToken);
    }

    public async Task<CustomerProfileDto> GetProfileAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.CustomerProfile)
            .Include(u => u.EmployeeProfile)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw AppException.FromError(AuthErrors.UserNotFound);
        }

        return await BuildCustomerProfileDtoAsync(user, cancellationToken);
    }

    public async Task<CustomerProfileDto> UpdateProfileAsync(
        long userId,
        UpdateCustomerProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.CustomerProfile)
            .Include(u => u.EmployeeProfile)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw AppException.FromError(AuthErrors.UserNotFound);
        }

        var now = DateTimeOffset.UtcNow;
        var currentMonth = now.ToString("yyyy-MM");

        if (user.CustomerProfile == null)
        {
            user.CustomerProfile = new CustomerProfile
            {
                UserId = user.Id,
                FullName = user.EmployeeProfile?.FullName ?? user.Email,
                ProfileEditMonth = currentMonth,
                ProfileEditCount = 0,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        var profile = user.CustomerProfile;

        // Enforce locked CCCD rule; allow first-time set for Google accounts with empty CCCD
        if (!string.IsNullOrWhiteSpace(request.IdentityNumber))
        {
            var trimmedCccd = request.IdentityNumber.Trim();
            if (!string.IsNullOrWhiteSpace(profile.IdentityNumber))
            {
                if (!string.Equals(trimmedCccd, profile.IdentityNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    throw AppException.FromError(AuthErrors.IdentityNumberLocked);
                }
            }
            else
            {
                var cccdConflict = await _dbContext.CustomerProfiles
                    .AnyAsync(cp => cp.IdentityNumber == trimmedCccd && cp.UserId != userId, cancellationToken);
                if (cccdConflict)
                {
                    throw AppException.FromError(AuthErrors.IdentityNumberAlreadyInUse);
                }
                profile.IdentityNumber = trimmedCccd;
            }
        }

        var newFullName = request.FullName.Trim();
        var newPhone = request.PhoneNumber.Trim();
        var nameOrPhoneChanged =
            !string.Equals(profile.FullName, newFullName, StringComparison.Ordinal) ||
            !string.Equals(user.PhoneNumber ?? string.Empty, newPhone, StringComparison.Ordinal);

        if (profile.ProfileEditMonth != currentMonth)
        {
            profile.ProfileEditMonth = currentMonth;
            profile.ProfileEditCount = 0;
        }

        if (nameOrPhoneChanged)
        {
            if (profile.ProfileEditCount >= 2)
            {
                throw AppException.FromError(AuthErrors.MonthlyProfileEditLimitReached);
            }

            profile.FullName = newFullName;
            user.PhoneNumber = newPhone;
            if (user.EmployeeProfile != null)
            {
                user.EmployeeProfile.FullName = newFullName;
                user.EmployeeProfile.UpdatedAt = now;
            }
            profile.ProfileEditCount += 1;
        }

        if (request.Address != null)
        {
            profile.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        }

        profile.UpdatedAt = now;
        user.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await BuildCustomerProfileDtoAsync(user, cancellationToken);
    }

    private async Task<CustomerProfileDto> BuildCustomerProfileDtoAsync(User user, CancellationToken cancellationToken)
    {
        var currentMonth = DateTimeOffset.UtcNow.ToString("yyyy-MM");
        var profile = user.CustomerProfile;
        var editsUsed = profile != null && profile.ProfileEditMonth == currentMonth
            ? profile.ProfileEditCount
            : 0;
        var debtItems = await GetNegativeDebtInvoicesAsync(user.Id, cancellationToken);
        var totalNegativeDebt = debtItems.Sum(d => d.OutstandingAmount);

        return new CustomerProfileDto(
            UserId: user.Id,
            Email: user.Email,
            FullName: profile?.FullName ?? user.EmployeeProfile?.FullName ?? user.Email,
            PhoneNumber: user.PhoneNumber,
            IdentityNumber: profile?.IdentityNumber,
            Address: profile?.Address,
            IdentityNumberLocked: !string.IsNullOrWhiteSpace(profile?.IdentityNumber),
            CurrentEditMonth: currentMonth,
            EditsUsedThisMonth: editsUsed,
            MaxEditsPerMonth: 2,
            RemainingEditsThisMonth: Math.Max(0, 2 - editsUsed),
            HasNegativeDepositDebt: debtItems.Count > 0,
            TotalNegativeDebtAmount: totalNegativeDebt,
            NegativeDebtInvoices: debtItems);
    }

    private async Task<List<NegativeDebtInvoiceItemDto>> GetNegativeDebtInvoicesAsync(long userId, CancellationToken cancellationToken)
    {
        var negativeAgreements = await _dbContext.RentalAgreements
            .AsNoTracking()
            .Include(a => a.Facility)
            .Include(a => a.UnitAllocations)
                .ThenInclude(ua => ua.StorageUnit)
            .Include(a => a.Invoices)
            .Where(a => a.CustomerId == userId && a.DepositBalance < 0)
            .ToListAsync(cancellationToken);

        if (negativeAgreements.Count == 0)
        {
            return new List<NegativeDebtInvoiceItemDto>();
        }

        return negativeAgreements.Select(a =>
        {
            var unitCode = a.UnitAllocations
                .OrderByDescending(u => u.Id)
                .FirstOrDefault()?.StorageUnit?.UnitCode ?? "N/A";
            var unpaidDeficitInv = a.Invoices
                .Where(i => i.Status != InvoiceStatusConstants.Paid && i.Status != InvoiceStatusConstants.Voided)
                .OrderByDescending(i => i.Id)
                .FirstOrDefault();
            var outstanding = unpaidDeficitInv != null
                ? Math.Max(0m, unpaidDeficitInv.TotalAmount - unpaidDeficitInv.PaidAmount)
                : Math.Abs(a.DepositBalance);

            return new NegativeDebtInvoiceItemDto(
                AgreementId: a.Id,
                AgreementNo: a.AgreementNo,
                UnitCode: unitCode,
                FacilityName: a.Facility?.Name ?? "Chi nhánh",
                NegativeBalance: a.DepositBalance,
                DeficitInvoiceId: unpaidDeficitInv?.Id,
                DeficitInvoiceNo: unpaidDeficitInv?.InvoiceNo,
                OutstandingAmount: outstanding);
        }).Where(x => x.OutstandingAmount > 0).ToList();
    }

    private async Task<(bool HasOverdue, decimal TotalAmount, int AgreementCount)> GetOverdueRentSummaryAsync(
        long userId,
        CancellationToken cancellationToken)
    {
        var utcToday = DateOnly.FromDateTime(DateTime.UtcNow);
        var overdueAgreements = await _dbContext.RentalAgreements
            .AsNoTracking()
            .Include(a => a.Invoices)
            .Where(a => a.CustomerId == userId
                        && (a.Status == RentalAgreementStatusConstants.Overdue
                            || a.Status == "defaulted"
                            || a.Invoices.Any(i =>
                                (i.Status == InvoiceStatusConstants.Overdue
                                 || ((i.Status == InvoiceStatusConstants.Open || i.Status == InvoiceStatusConstants.PartiallyPaid) && i.DueDate < utcToday))
                                && i.TotalAmount > i.PaidAmount)))
            .ToListAsync(cancellationToken);

        if (overdueAgreements.Count == 0)
            return (false, 0m, 0);

        decimal totalAmount = 0m;
        foreach (var ag in overdueAgreements)
        {
            var invDebt = ag.Invoices
                .Where(i => i.Status is InvoiceStatusConstants.Overdue or InvoiceStatusConstants.Open or InvoiceStatusConstants.PartiallyPaid
                            && (i.Status == InvoiceStatusConstants.Overdue || i.DueDate < utcToday)
                            && i.TotalAmount > i.PaidAmount)
                .Sum(i => Math.Max(0m, i.TotalAmount - i.PaidAmount));
            totalAmount += invDebt > 0 ? invDebt : ag.MonthlyRateSnapshot;
        }

        return (true, totalAmount, overdueAgreements.Count);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(User user, CancellationToken cancellationToken)
    {
        var rawRoles = ExtractRawRoles(user);
        var fullName = user.CustomerProfile?.FullName ?? user.EmployeeProfile?.FullName ?? user.Email;
        var token = _jwtTokenService.GenerateToken(user, rawRoles, fullName);

        return new AuthResponse
        {
            AccessToken = token,
            TokenType = AuthConstants.BearerTokenType,
            ExpiresInMinutes = _jwtSettings.AccessTokenMinutes > 0 ? _jwtSettings.AccessTokenMinutes : AuthConstants.DefaultAccessTokenMinutes,
            User = await MapToUserDtoAsync(user, rawRoles, fullName, cancellationToken)
        };
    }

    private async Task<AuthUserDto> MapToUserDtoAsync(
        User user,
        List<string>? rawRoles = null,
        string? fullName = null,
        CancellationToken cancellationToken = default)
    {
        rawRoles ??= ExtractRawRoles(user);
        var canonicalRoles = rawRoles
            .Select(r => EnumDbMapper.TryParseRoleCode(r, out var parsed) ? parsed.ToString() : r)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var permissions = CapabilitiesService.ResolvePermissionsForRoles(rawRoles);
        var now = DateTimeOffset.UtcNow;
        var assignedFacilityIds = canonicalRoles.Any(r =>
                r.Equals("Staff", StringComparison.OrdinalIgnoreCase))
            ? await _dbContext.StaffFacilityAssignments
                .AsNoTracking()
                .Where(a => a.EmployeeId == user.Id && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
                .Select(a => a.FacilityId)
                .Distinct()
                .ToListAsync(cancellationToken)
            : canonicalRoles.Any(r =>
                r.Equals("Manager", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                ? await _dbContext.Facilities
                    .AsNoTracking()
                    .Select(f => f.Id)
                    .ToListAsync(cancellationToken)
                : new List<long>();

        fullName ??= user.CustomerProfile?.FullName ?? user.EmployeeProfile?.FullName ?? user.Email;

        var currentMonth = now.ToString("yyyy-MM");
        var editsUsed = user.CustomerProfile != null && user.CustomerProfile.ProfileEditMonth == currentMonth
            ? user.CustomerProfile.ProfileEditCount
            : 0;
        var debtItems = canonicalRoles.Any(r => r.Equals("Customer", StringComparison.OrdinalIgnoreCase))
            ? await GetNegativeDebtInvoicesAsync(user.Id, cancellationToken)
            : new List<NegativeDebtInvoiceItemDto>();

        var overdueSummary = canonicalRoles.Any(r => r.Equals("Customer", StringComparison.OrdinalIgnoreCase))
            ? await GetOverdueRentSummaryAsync(user.Id, cancellationToken)
            : (HasOverdue: false, TotalAmount: 0m, AgreementCount: 0);

        return new AuthUserDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = fullName,
            PhoneNumber = user.PhoneNumber,
            IdentityNumber = user.CustomerProfile?.IdentityNumber,
            Address = user.CustomerProfile?.Address,
            EditsUsedThisMonth = editsUsed,
            RemainingEditsThisMonth = Math.Max(0, 2 - editsUsed),
            HasNegativeDepositDebt = debtItems.Count > 0,
            TotalNegativeDebtAmount = debtItems.Sum(d => d.OutstandingAmount),
            NegativeDebtInvoices = debtItems,
            HasOverdueRentDebt = overdueSummary.HasOverdue,
            TotalOverdueRentAmount = overdueSummary.TotalAmount,
            OverdueAgreementCount = overdueSummary.AgreementCount,
            Roles = canonicalRoles,
            Permissions = permissions,
            AssignedFacilityIds = assignedFacilityIds,
            Status = user.Status
        };
    }

    private static List<string> ExtractRawRoles(User user)
    {
        var roles = user.UserRoleUsers
            .Where(ur => ur.Role != null)
            .Select(ur => ur.Role.Code)
            .ToList();

        if (roles.Count == 0)
            roles.Add(RoleConstants.Customer);

        return roles;
    }
}
