using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Customer.Rentals;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public class CustomerRentalService : ICustomerRentalService
{
    private static readonly Regex PinFormatRegex = new(@"^\d{6}$", RegexOptions.Compiled);

    private readonly SelfStorageDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly JwtSettings _jwtSettings;
    private readonly RentalSettings _rentalSettings;
    private readonly PaymentSettings _paymentSettings;
    private readonly ILogger<CustomerRentalService> _logger;

    public CustomerRentalService(
        SelfStorageDbContext dbContext,
        IMemoryCache cache,
        IOptions<JwtSettings> jwtOptions,
        ILogger<CustomerRentalService> logger,
        IOptions<RentalSettings>? rentalOptions = null,
        IOptions<PaymentSettings>? paymentOptions = null)
    {
        _dbContext = dbContext;
        _cache = cache;
        _jwtSettings = jwtOptions.Value;
        _rentalSettings = rentalOptions?.Value ?? new RentalSettings();
        _paymentSettings = paymentOptions?.Value ?? new PaymentSettings();
        _logger = logger;
    }

    public async Task<IReadOnlyList<RentalSummaryDto>> GetMyRentalsAsync(long customerId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.DateTime);

        var agreements = await _dbContext.RentalAgreements
            .AsNoTracking()
            .Where(r => r.CustomerId == customerId &&
                        (r.Status == RentalAgreementStatusConstants.Active ||
                         r.Status == RentalAgreementStatusConstants.ExpiringSoon))
            .Include(r => r.Facility)
            .Include(r => r.UnitAllocations.Where(a => a.Status == AllocationStatusConstants.Active))
                .ThenInclude(a => a.StorageUnit)
                    .ThenInclude(u => u.UnitType)
            .Include(r => r.Invoices)
            .OrderByDescending(r => r.StartDate)
            .ToListAsync(cancellationToken);

        var result = agreements.Select(r =>
        {
            var alloc = r.UnitAllocations.FirstOrDefault(a => a.Status == AllocationStatusConstants.Active);
            var unit = alloc?.StorageUnit;
            var unitType = unit?.UnitType;

            var hasOverdueDebt = r.Invoices.Any(i =>
                i.DueDate < today.AddDays(-1) &&
                i.Status != InvoiceStatusConstants.Paid &&
                (i.TotalAmount - i.PaidAmount) > 0);

            var daysUntilExpiry = Math.Max(0, r.EndDate.DayNumber - today.DayNumber);

            var dimensions = unitType != null
                ? $"{unitType.WidthM:F1}m x {unitType.LengthM:F1}m x {unitType.HeightM:F1}m"
                : string.Empty;

            return new RentalSummaryDto
            {
                AgreementId = r.Id,
                AgreementNo = r.AgreementNo,
                FacilityId = r.FacilityId,
                FacilityName = r.Facility?.Name ?? string.Empty,
                FacilityAddress = r.Facility?.AddressLine ?? string.Empty,
                FacilityCity = r.Facility?.City ?? string.Empty,
                StorageUnitId = unit?.Id ?? 0,
                UnitCode = unit?.UnitCode ?? string.Empty,
                FloorLabel = unit?.FloorLabel,
                ZoneLabel = unit?.ZoneLabel,
                UnitTypeName = unitType?.Name ?? string.Empty,
                Dimensions = dimensions,
                AreaM2 = unitType?.AreaM2,
                VolumeM3 = unitType?.VolumeM3,
                StartDate = r.StartDate,
                EndDate = r.EndDate,
                ActualEndDate = r.ActualEndDate,
                MonthlyRate = r.MonthlyRateSnapshot,
                DepositBalance = r.DepositBalance,
                Status = r.Status,
                CheckedInAt = r.CheckedInAt,
                HasOverdueDebt = hasOverdueDebt,
                DaysUntilExpiry = daysUntilExpiry
            };
        }).ToList();

        _logger.LogInformation(RentalLogMessages.RentalsRetrieved, customerId, result.Count);
        return result;
    }

    public async Task<AccessCredentialDto> GetAccessCredentialsAsync(long customerId, long agreementId, CancellationToken cancellationToken = default)
    {
        var agreement = await _dbContext.RentalAgreements
            .Include(a => a.Facility)
            .Include(a => a.UnitAllocations.Where(ua => ua.Status == AllocationStatusConstants.Active))
                .ThenInclude(ua => ua.StorageUnit)
            .Include(a => a.AccessCredentials)
            .Include(a => a.Invoices)
            .FirstOrDefaultAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (agreement is null)
        {
            throw AppException.FromError(RentalErrors.AgreementNotFound);
        }

        // BR-RSV-04: Must have checked in before receiving credentials
        if (agreement.CheckedInAt == null)
        {
            throw AppException.FromError(RentalErrors.AgreementNotCheckedIn);
        }

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.DateTime);

        // Check Facility Business Hours (OpeningTime and ClosingTime) if enforced
        if (_rentalSettings.EnforceOperatingHours &&
            agreement.Facility?.OpeningTime != null &&
            agreement.Facility?.ClosingTime != null)
        {
            var tzId = string.IsNullOrWhiteSpace(agreement.Facility.Timezone) ? _rentalSettings.DefaultTimezone : agreement.Facility.Timezone;
            TimeZoneInfo tz;
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            }
            catch
            {
                tz = tzId.Equals(_rentalSettings.DefaultTimezone, StringComparison.OrdinalIgnoreCase)
                    ? TimeZoneInfo.FindSystemTimeZoneById(RentalDefaults.FallbackWindowsTimezone)
                    : TimeZoneInfo.Utc;
            }

            var localNow = TimeZoneInfo.ConvertTime(now, tz);
            var currentTime = TimeOnly.FromTimeSpan(localNow.TimeOfDay);
            var opening = agreement.Facility.OpeningTime.Value;
            var closing = agreement.Facility.ClosingTime.Value;

            bool isWithinHours = opening <= closing
                ? (currentTime >= opening && currentTime <= closing)
                : (currentTime >= opening || currentTime <= closing);

            if (!isWithinHours)
            {
                _logger.LogWarning(RentalLogMessages.OutsideBusinessHours,
                    agreementId, opening, closing, currentTime);
                throw AppException.FromError(RentalErrors.OutsideBusinessHours);
            }
        }

        // BR-REN-03: access is suspended from 00:00 on the day after an unpaid invoice is due.
        var isOverdue = agreement.Invoices.Any(i =>
            i.DueDate < today &&
            i.Status != InvoiceStatusConstants.Paid &&
            (i.TotalAmount - i.PaidAmount) > 0);

        var activeAlloc = agreement.UnitAllocations.FirstOrDefault(a => a.Status == AllocationStatusConstants.Active);
        var unitCode = activeAlloc?.StorageUnit?.UnitCode ?? string.Empty;
        var facilityName = agreement.Facility?.Name ?? string.Empty;

        var pinCred = agreement.AccessCredentials.FirstOrDefault(c => c.CredentialType == CredentialTypeConstants.Pin);

        if (isOverdue)
        {
            // Suspend credential status per BR-REN-03
            if (pinCred != null && pinCred.Status != CredentialStatusConstants.Suspended)
            {
                pinCred.Status = CredentialStatusConstants.Suspended;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogWarning(RentalLogMessages.AccessCredentialsRetrieved, customerId, agreementId, CredentialStatusConstants.Suspended);

            return new AccessCredentialDto
            {
                AgreementId = agreement.Id,
                AgreementNo = agreement.AgreementNo,
                UnitCode = unitCode,
                FacilityName = facilityName,
                Status = CredentialStatusConstants.Suspended,
                KeypadPin = null,
                GateQrToken = null,
                QrExpiresInSeconds = 0,
                QrExpiresAt = null,
                SuspendedReason = RentalSuspensionReasons.OverdueDebtExceeded
            };
        }

        // Ensure PIN credential exists
        if (pinCred is null)
        {
            var defaultPin = GenerateSecureNumericPin();
            pinCred = new AccessCredential
            {
                AgreementId = agreement.Id,
                CredentialType = CredentialTypeConstants.Pin,
                SecretDigest = BCrypt.Net.BCrypt.HashPassword(defaultPin),
                DisplayHint = defaultPin,
                Status = CredentialStatusConstants.Active,
                IssuedAt = now,
                CreatedAt = now
            };
            _dbContext.AccessCredentials.Add(pinCred);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (pinCred.Status == CredentialStatusConstants.Suspended)
        {
            // Restore active status once debt is settled
            pinCred.Status = CredentialStatusConstants.Active;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Generate Time-based JWT Gate QR Token with configurable TTL
        var qrExpiresAt = now.AddSeconds(_rentalSettings.QrTtlSeconds);
        var gateQrToken = GenerateGateAccessJwt(agreement, activeAlloc?.StorageUnitId ?? 0, qrExpiresAt);

        _logger.LogInformation(RentalLogMessages.AccessCredentialsRetrieved, customerId, agreementId, CredentialStatusConstants.Active);

        return new AccessCredentialDto
        {
            AgreementId = agreement.Id,
            AgreementNo = agreement.AgreementNo,
            UnitCode = unitCode,
            FacilityName = facilityName,
            Status = CredentialStatusConstants.Active,
            KeypadPin = pinCred.DisplayHint,
            GateQrToken = gateQrToken,
            QrExpiresInSeconds = _rentalSettings.QrTtlSeconds,
            QrExpiresAt = qrExpiresAt,
            SuspendedReason = null
        };
    }

    public async Task<ChangePinResponseDto> ChangePinAsync(long customerId, long agreementId, ChangePinRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Anti-brute force check on failed attempts per agreement
        var lockoutKey = $"{RentalDefaults.PinLockoutCacheKeyPrefix}{agreementId}";
        if (_cache.TryGetValue(lockoutKey, out int failedAttempts) && failedAttempts >= _rentalSettings.MaxFailedPinAttempts)
        {
            _logger.LogWarning(RentalLogMessages.PinLockout, agreementId);
            throw AppException.FromError(RentalErrors.TooManyFailedPinAttempts);
        }

        // 2. Validate PIN format: exactly 6 digits
        if (string.IsNullOrWhiteSpace(request.NewPin) || !PinFormatRegex.IsMatch(request.NewPin))
        {
            throw AppException.FromError(RentalErrors.InvalidPinFormat);
        }

        // 3. Prevent weak PINs: all same digits or simple ascending/descending sequences
        if (request.NewPin.Distinct().Count() == 1 || RentalSecurityConstants.WeakPins.Contains(request.NewPin))
        {
            throw AppException.FromError(RentalErrors.PinTooSimple);
        }

        // 4. Verify agreement ownership
        var agreement = await _dbContext.RentalAgreements
            .Include(a => a.AccessCredentials)
            .Include(a => a.Invoices)
            .FirstOrDefaultAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (agreement is null)
        {
            throw AppException.FromError(RentalErrors.AgreementNotFound);
        }

        // 5. BR-REN-03: If access is suspended due to debt, reject PIN change
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.DateTime);
        var isOverdue = agreement.Invoices.Any(i =>
            i.DueDate < today &&
            i.Status != InvoiceStatusConstants.Paid &&
            (i.TotalAmount - i.PaidAmount) > 0);

        if (isOverdue)
        {
            throw AppException.FromError(RentalErrors.AccessSuspendedDueToOverdue);
        }

        // 6. Verify current PIN if credential exists and current PIN is provided
        var pinCred = agreement.AccessCredentials.FirstOrDefault(c => c.CredentialType == CredentialTypeConstants.Pin);

        if (pinCred != null && !string.IsNullOrWhiteSpace(pinCred.SecretDigest) && !string.IsNullOrWhiteSpace(request.CurrentPin))
        {
            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPin, pinCred.SecretDigest))
            {
                var newCount = failedAttempts + 1;
                _cache.Set(lockoutKey, newCount, TimeSpan.FromMinutes(_rentalSettings.PinLockoutDurationMinutes));
                _logger.LogWarning(RentalLogMessages.FailedPinVerification,
                    agreementId, newCount, _rentalSettings.MaxFailedPinAttempts);
                throw AppException.FromError(RentalErrors.IncorrectCurrentPin);
            }
        }

        // On successful verification, clear failure counter
        _cache.Remove(lockoutKey);

        // 7. Update PIN securely and set status to Pending for hardware/IoT lock synchronization
        if (pinCred is null)
        {
            pinCred = new AccessCredential
            {
                AgreementId = agreement.Id,
                CredentialType = CredentialTypeConstants.Pin,
                SecretDigest = BCrypt.Net.BCrypt.HashPassword(request.NewPin),
                DisplayHint = request.NewPin,
                Status = CredentialStatusConstants.Pending,
                IssuedAt = now,
                CreatedAt = now
            };
            _dbContext.AccessCredentials.Add(pinCred);
        }
        else
        {
            pinCred.SecretDigest = BCrypt.Net.BCrypt.HashPassword(request.NewPin);
            pinCred.DisplayHint = request.NewPin;
            pinCred.Status = CredentialStatusConstants.Pending;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(RentalLogMessages.PinChanged, customerId, agreementId);

        return new ChangePinResponseDto
        {
            Success = true,
            SyncStatus = PinSyncStatusConstants.Pending,
            Message = RentalPinMessages.PinSyncPending,
            EstimatedSyncSeconds = _rentalSettings.EstimatedPinSyncSeconds
        };
    }

    public async Task<UnlockStorageUnitResponseDto> UnlockUnitAsync(long customerId, long agreementId, UnlockStorageUnitRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Anti-brute force check on failed unlock attempts per agreement
        var lockoutKey = $"{RentalDefaults.PinLockoutCacheKeyPrefix}unlock:{agreementId}";
        if (_cache.TryGetValue(lockoutKey, out int failedAttempts) && failedAttempts >= _rentalSettings.MaxFailedPinAttempts)
        {
            _logger.LogWarning(RentalLogMessages.UnlockLockout, agreementId);
            throw AppException.FromError(RentalErrors.TooManyFailedPinAttempts);
        }

        // 2. Validate PIN format: exactly 6 digits
        if (string.IsNullOrWhiteSpace(request.Pin) || !PinFormatRegex.IsMatch(request.Pin))
        {
            throw AppException.FromError(RentalErrors.InvalidPinFormat);
        }

        // 3. Verify agreement ownership & include necessary relations
        var agreement = await _dbContext.RentalAgreements
            .Include(a => a.Facility)
            .Include(a => a.UnitAllocations.Where(ua => ua.Status == AllocationStatusConstants.Active))
                .ThenInclude(ua => ua.StorageUnit)
            .Include(a => a.AccessCredentials)
            .Include(a => a.Invoices)
            .FirstOrDefaultAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (agreement is null)
        {
            throw AppException.FromError(RentalErrors.AgreementNotFound);
        }

        // 4. Must be checked in
        if (agreement.CheckedInAt == null)
        {
            throw AppException.FromError(RentalErrors.AgreementNotCheckedIn);
        }

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.DateTime);

        // 5. Check Facility Business Hours if enforced
        if (_rentalSettings.EnforceOperatingHours &&
            agreement.Facility?.OpeningTime != null &&
            agreement.Facility?.ClosingTime != null)
        {
            var tzId = string.IsNullOrWhiteSpace(agreement.Facility.Timezone) ? _rentalSettings.DefaultTimezone : agreement.Facility.Timezone;
            TimeZoneInfo tz;
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            }
            catch
            {
                tz = tzId.Equals(_rentalSettings.DefaultTimezone, StringComparison.OrdinalIgnoreCase)
                    ? TimeZoneInfo.FindSystemTimeZoneById(RentalDefaults.FallbackWindowsTimezone)
                    : TimeZoneInfo.Utc;
            }

            var localNow = TimeZoneInfo.ConvertTime(now, tz);
            var currentTime = TimeOnly.FromTimeSpan(localNow.TimeOfDay);
            var opening = agreement.Facility.OpeningTime.Value;
            var closing = agreement.Facility.ClosingTime.Value;

            bool isWithinHours = opening <= closing
                ? (currentTime >= opening && currentTime <= closing)
                : (currentTime >= opening || currentTime <= closing);

            if (!isWithinHours)
            {
                _logger.LogWarning(RentalLogMessages.OutsideBusinessHours,
                    agreementId, opening, closing, currentTime);
                throw AppException.FromError(RentalErrors.OutsideBusinessHours);
            }
        }

        // 6. BR-REN-03: Real-time debt check
        var isOverdue = agreement.Invoices.Any(i =>
            i.DueDate < today &&
            i.Status != InvoiceStatusConstants.Paid &&
            (i.TotalAmount - i.PaidAmount) > 0);

        if (isOverdue)
        {
            throw AppException.FromError(RentalErrors.AccessSuspendedDueToOverdue);
        }

        // 7. Verify PIN credential
        var pinCred = agreement.AccessCredentials.FirstOrDefault(c => c.CredentialType == CredentialTypeConstants.Pin);
        if (pinCred == null || pinCred.Status == CredentialStatusConstants.Suspended)
        {
            throw AppException.FromError(RentalErrors.AccessSuspendedDueToOverdue);
        }

        bool isPinValid = false;
        if (!string.IsNullOrWhiteSpace(pinCred.SecretDigest))
        {
            isPinValid = BCrypt.Net.BCrypt.Verify(request.Pin, pinCred.SecretDigest);
        }
        else if (!string.IsNullOrWhiteSpace(pinCred.DisplayHint))
        {
            isPinValid = request.Pin == pinCred.DisplayHint;
        }

        if (!isPinValid)
        {
            var newCount = failedAttempts + 1;
            _cache.Set(lockoutKey, newCount, TimeSpan.FromMinutes(_rentalSettings.PinLockoutDurationMinutes));
            _logger.LogWarning(RentalLogMessages.FailedUnlockVerification,
                agreementId, newCount, _rentalSettings.MaxFailedPinAttempts);
            throw AppException.FromError(RentalErrors.IncorrectPin);
        }

        // Reset lockout count on success
        _cache.Remove(lockoutKey);

        var activeAlloc = agreement.UnitAllocations.FirstOrDefault(a => a.Status == AllocationStatusConstants.Active);
        var unitCode = activeAlloc?.StorageUnit?.UnitCode ?? string.Empty;
        var facilityName = agreement.Facility?.Name ?? string.Empty;

        _logger.LogInformation(RentalLogMessages.UnitUnlockedSuccess,
            customerId, unitCode, agreementId);

        return new UnlockStorageUnitResponseDto
        {
            Success = true,
            AgreementId = agreement.Id,
            UnitCode = unitCode,
            FacilityName = facilityName,
            UnlockedAt = now,
            RelockAfterSeconds = RentalUnlockMessages.DefaultRelockAfterSeconds,
            Message = string.Format(RentalUnlockMessages.UnitUnlockedFormat, unitCode, facilityName, RentalUnlockMessages.DefaultRelockAfterSeconds)
        };
    }

    public async Task<HandoverRecordDto> GetHandoverRecordAsync(long customerId, long agreementId, CancellationToken cancellationToken = default)
    {
        var agreementExists = await _dbContext.RentalAgreements
            .AnyAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (!agreementExists)
        {
            throw AppException.FromError(RentalErrors.AgreementNotFound);
        }

        var handover = await _dbContext.HandoverRecords
            .Include(h => h.Agreement)
            .Include(h => h.HandledByNavigation)
            .Include(h => h.Inspection)
                .ThenInclude(i => i.InspectionItems)
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.AgreementId == agreementId, cancellationToken);

        if (handover is null)
        {
            throw AppException.FromError(RentalErrors.HandoverNotFound);
        }

        _logger.LogInformation(RentalLogMessages.HandoverRecordRetrieved, customerId, agreementId);

        return new HandoverRecordDto
        {
            HandoverId = handover.Id,
            AgreementId = handover.AgreementId,
            AgreementNo = handover.Agreement?.AgreementNo ?? string.Empty,
            HandoverType = handover.HandoverType,
            CustomerSignatureRef = handover.CustomerSignatureRef,
            CustomerSignedAt = handover.CustomerSignedAt,
            StaffSignatureRef = handover.StaffSignatureRef,
            StaffSignedAt = handover.StaffSignedAt,
            HandledByName = handover.HandledByNavigation != null ? handover.HandledByNavigation.FullName : string.Empty,
            HandoverNotes = handover.Notes,
            CreatedAt = handover.CreatedAt,
            Inspection = handover.Inspection == null ? null : new HandoverInspectionDto
            {
                InspectionId = handover.Inspection.Id,
                InspectionType = handover.Inspection.InspectionType,
                Status = handover.Inspection.Status,
                OverallCondition = handover.Inspection.OverallCondition,
                Summary = handover.Inspection.Summary,
                InspectedAt = handover.Inspection.InspectedAt,
                Items = handover.Inspection.InspectionItems.Select(item => new HandoverInspectionItemDto
                {
                    ItemId = item.Id,
                    ItemName = item.ItemName,
                    Condition = item.Condition,
                    Notes = item.Notes,
                    PhotoUrl = item.PhotoUrl, // URL only to prevent heavy Base64 payload
                    ChargeAmount = item.ChargeAmount
                }).ToList()
            }
        };
    }

    private string GenerateGateAccessJwt(RentalAgreement agreement, long unitId, DateTimeOffset expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, agreement.Id.ToString()),
            new Claim(RentalClaimTypes.AgreementId, agreement.Id.ToString()),
            new Claim(RentalClaimTypes.CustomerId, agreement.CustomerId.ToString()),
            new Claim(RentalClaimTypes.FacilityId, agreement.FacilityId.ToString()),
            new Claim(RentalClaimTypes.UnitId, unitId.ToString()),
            new Claim(RentalClaimTypes.AgreementNo, agreement.AgreementNo),
            new Claim(RentalClaimTypes.Type, RentalDefaults.GateAccessClaimType),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        // Leeway time prevents premature expiration due to clock drift between server and IoT scanners
        var notBefore = DateTime.UtcNow.AddSeconds(-_rentalSettings.ClockSkewLeewaySeconds);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            notBefore: notBefore,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateSecureNumericPin()
    {
        while (true)
        {
            var number = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000);
            var pin = number.ToString("D6");
            if (pin.Distinct().Count() > 1 && !RentalSecurityConstants.WeakPins.Contains(pin))
            {
                return pin;
            }
        }
    }

    public async Task<MoveOutResponseDto> RequestMoveOutAsync(
        long customerId,
        long agreementId,
        RequestMoveOutRequest request,
        CancellationToken cancellationToken = default)
    {
        var agreement = await _dbContext.RentalAgreements
            .FirstOrDefaultAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (agreement == null)
            throw AppException.FromError(RentalErrors.AgreementNotFound);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.RequestedMoveOutDate < today)
            throw AppException.FromError(RentalErrors.InvalidMoveOutDate);

        var existingPendingMoveOut = await _dbContext.MoveOutRequests
            .AnyAsync(m => m.AgreementId == agreementId && 
                           (m.Status == MoveOutStatusConstants.Requested || m.Status == MoveOutStatusConstants.Scheduled), 
                      cancellationToken);

        if (existingPendingMoveOut)
            throw AppException.FromError(RentalErrors.MoveOutAlreadyRequested);

        var moveOut = new MoveOutRequest
        {
            AgreementId = agreementId,
            RequestedBy = customerId,
            RequestedMoveOutDate = request.RequestedMoveOutDate,
            Reason = request.Reason?.Trim(),
            Status = MoveOutStatusConstants.Requested,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.MoveOutRequests.Add(moveOut);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new MoveOutResponseDto(
            Id: moveOut.Id,
            AgreementId: moveOut.AgreementId,
            RequestedMoveOutDate: moveOut.RequestedMoveOutDate,
            Status: moveOut.Status,
            Reason: moveOut.Reason,
            CreatedAt: moveOut.CreatedAt
        );
    }

    public async Task<RefundPreviewDto> GetRefundPreviewAsync(
        long customerId,
        long agreementId,
        CancellationToken cancellationToken = default)
    {
        var agreement = await _dbContext.RentalAgreements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (agreement == null)
            throw AppException.FromError(RentalErrors.AgreementNotFound);

        var deposit = agreement.DepositBalance;
        var estimatedCleaning = 0m;
        var estimatedRepair = 0m;
        var estimatedOverdue = 0m;

        // Check if there are overdue invoices
        var unpaidInvoices = await _dbContext.Invoices
            .AsNoTracking()
            .Where(i => i.AgreementId == agreementId && i.Status == InvoiceStatusConstants.Overdue)
            .ToListAsync(cancellationToken);

        if (unpaidInvoices.Any())
        {
            estimatedOverdue = unpaidInvoices.Sum(i => i.TotalAmount - i.PaidAmount);
        }

        var estimatedNetRefund = Math.Max(0m, deposit - (estimatedCleaning + estimatedRepair + estimatedOverdue));

        return new RefundPreviewDto(
            AgreementId: agreement.Id,
            AgreementNo: agreement.AgreementNo,
            DepositBalance: deposit,
            EstimatedCleaningFee: estimatedCleaning,
            EstimatedRepairFee: estimatedRepair,
            EstimatedOverdueCharges: estimatedOverdue,
            EstimatedNetRefund: estimatedNetRefund,
            Note: _rentalSettings.DefaultRefundPreviewNote
        );
    }

    public async Task<RenewAgreementResponseDto> RenewAgreementAsync(
        long customerId,
        long agreementId,
        RenewAgreementRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.RenewalMonths < _rentalSettings.MinRenewalMonths || request.RenewalMonths > _rentalSettings.MaxRenewalMonths)
            throw AppException.FromError(RentalErrors.InvalidRenewalMonths);

        var agreement = await _dbContext.RentalAgreements
            .FirstOrDefaultAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (agreement == null)
            throw AppException.FromError(RentalErrors.AgreementNotFound);

        var existingRenewals = await _dbContext.RentalRenewals
            .Where(r => r.AgreementId == agreementId && (r.Status == RenewalStatusConstants.PendingPayment || r.Status == RenewalStatusConstants.Paid))
            .ToListAsync(cancellationToken);

        var activePending = existingRenewals.FirstOrDefault(r => r.Status == RenewalStatusConstants.PendingPayment);
        if (activePending != null)
        {
            if (DateTimeOffset.UtcNow - activePending.CreatedAt > TimeSpan.FromMinutes(15))
            {
                // Expired after 15 minutes hold window
                activePending.Status = RenewalStatusConstants.Cancelled;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                throw AppException.FromError(RentalErrors.RenewalAlreadyPending);
            }
        }
        else if (existingRenewals.Any(r => r.Status == RenewalStatusConstants.Paid))
        {
            throw AppException.FromError(RentalErrors.RenewalAlreadyPending);
        }

        var newEndDate = agreement.EndDate.AddMonths(request.RenewalMonths);
        var totalAmount = agreement.MonthlyRateSnapshot * request.RenewalMonths;

        var renewal = new RentalRenewal
        {
            AgreementId = agreement.Id,
            RequestedBy = customerId,
            OldEndDate = agreement.EndDate,
            RequestedEndDate = newEndDate,
            OldMonthlyRate = agreement.MonthlyRateSnapshot,
            NewMonthlyRate = agreement.MonthlyRateSnapshot,
            Status = RenewalStatusConstants.PendingPayment,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.RentalRenewals.Add(renewal);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RenewAgreementResponseDto(
            RenewalId: renewal.Id,
            AgreementId: agreement.Id,
            OldEndDate: renewal.OldEndDate,
            NewEndDate: renewal.RequestedEndDate,
            MonthlyRate: agreement.MonthlyRateSnapshot,
            TotalRenewalAmount: totalAmount,
            Status: renewal.Status,
            CreatedAt: renewal.CreatedAt
        );
    }

    public async Task<IReadOnlyList<AuthorizedMemberDto>> GetAuthorizedMembersAsync(
        long customerId,
        long agreementId,
        CancellationToken cancellationToken = default)
    {
        var agreementExists = await _dbContext.RentalAgreements
            .AnyAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (!agreementExists)
            throw AppException.FromError(RentalErrors.AgreementNotFound);

        var members = await _dbContext.AuthorizedAccessMembers
            .AsNoTracking()
            .Where(m => m.AgreementId == agreementId && m.Status == AuthorizedMemberStatusConstants.Active)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return members.Select(m => new AuthorizedMemberDto(
            Id: m.Id,
            AgreementId: m.AgreementId,
            FullName: m.FullName,
            IdentityFingerprint: m.IdentityFingerprint,
            RelationshipToCustomer: m.RelationshipToCustomer,
            ValidFrom: m.ValidFrom,
            ValidTo: m.ValidTo,
            Status: m.Status,
            CreatedAt: m.CreatedAt
        )).ToList();
    }

    public async Task<AuthorizedMemberDto> AddAuthorizedMemberAsync(
        long customerId,
        long agreementId,
        CreateAuthorizedMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var agreementExists = await _dbContext.RentalAgreements
            .AnyAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (!agreementExists)
            throw AppException.FromError(RentalErrors.AgreementNotFound);

        if (string.IsNullOrWhiteSpace(request.FullName))
            throw AppException.FromError(RentalErrors.EmptyMemberName);

        var now = DateTimeOffset.UtcNow;
        var member = new AuthorizedAccessMember
        {
            AgreementId = agreementId,
            FullName = request.FullName.Trim(),
            IdentityFingerprint = request.IdentityFingerprint?.Trim(),
            RelationshipToCustomer = request.RelationshipToCustomer?.Trim(),
            ValidFrom = now,
            ValidTo = request.ValidTo,
            Status = AuthorizedMemberStatusConstants.Active,
            CreatedBy = customerId,
            CreatedAt = now
        };

        _dbContext.AuthorizedAccessMembers.Add(member);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AuthorizedMemberDto(
            Id: member.Id,
            AgreementId: member.AgreementId,
            FullName: member.FullName,
            IdentityFingerprint: member.IdentityFingerprint,
            RelationshipToCustomer: member.RelationshipToCustomer,
            ValidFrom: member.ValidFrom,
            ValidTo: member.ValidTo,
            Status: member.Status,
            CreatedAt: member.CreatedAt
        );
    }

    public async Task RevokeAuthorizedMemberAsync(
        long customerId,
        long agreementId,
        long memberId,
        CancellationToken cancellationToken = default)
    {
        var agreementExists = await _dbContext.RentalAgreements
            .AnyAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (!agreementExists)
            throw AppException.FromError(RentalErrors.AgreementNotFound);

        var member = await _dbContext.AuthorizedAccessMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.AgreementId == agreementId, cancellationToken);

        if (member == null || member.Status != AuthorizedMemberStatusConstants.Active)
            throw AppException.FromError(RentalErrors.MemberNotFound);

        member.Status = AuthorizedMemberStatusConstants.Revoked;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<UnitTransferRequestDto> RequestUnitTransferAsync(
        long customerId,
        long agreementId,
        CreateTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        var agreement = await _dbContext.RentalAgreements
            .Include(a => a.UnitAllocations)
            .FirstOrDefaultAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (agreement == null)
            throw AppException.FromError(RentalErrors.AgreementNotFound);

        var activeAllocation = agreement.UnitAllocations.FirstOrDefault(u => u.Status == AllocationStatusConstants.Active);
        if (activeAllocation == null)
            throw AppException.FromError(RentalErrors.AgreementNotCheckedIn);

        var targetType = await _dbContext.UnitTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.RequestedUnitTypeId, cancellationToken);

        if (targetType == null)
            throw AppException.FromError(RentalErrors.TargetUnitTypeNotFound);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.MoveOutDate < today || request.MoveOutDate > request.MoveInDate || request.MoveInDate.DayNumber - request.MoveOutDate.DayNumber > RentalPolicyConstants.MaxTransferTransitDays)
            throw AppException.FromError(RentalErrors.InvalidTransferSchedule);

        var targetFacilityExists = await _dbContext.Facilities
            .AsNoTracking()
            .AnyAsync(f => f.Id == request.RequestedFacilityId, cancellationToken);

        if (!targetFacilityExists)
            throw AppException.FromError(RentalErrors.TargetFacilityNotFound);

        var targetRate = await _dbContext.FacilityRates
            .AsNoTracking()
            .Where(r => r.FacilityId == request.RequestedFacilityId && r.UnitTypeId == request.RequestedUnitTypeId && r.ValidFrom <= today && (r.ValidTo == null || r.ValidTo >= today))
            .OrderByDescending(r => r.ValidFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (targetRate == null)
            throw AppException.FromError(RentalErrors.TargetUnitTypeNotFound);

        var remainingDays = Math.Max(0, agreement.EndDate.DayNumber - request.MoveOutDate.DayNumber);
        var diffRent = Math.Max(0, targetRate.MonthlyRate - agreement.MonthlyRateSnapshot) * remainingDays / 30m;
        var diffDeposit = Math.Max(0, targetRate.DepositAmount - agreement.DepositBalance);

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw AppException.FromError(RentalErrors.EmptyTransferReason);

        var pendingTransfer = await _dbContext.UnitTransferRequests
            .AnyAsync(t => t.AgreementId == agreementId && (t.Status == TransferRequestStatusConstants.PendingPayment || t.Status == TransferRequestStatusConstants.Paid || t.Status == TransferRequestStatusConstants.MovedOutInTransit || t.Status == TransferRequestStatusConstants.Pending || t.Status == TransferRequestStatusConstants.Approved || t.Status == TransferRequestStatusConstants.Scheduled), cancellationToken);

        if (pendingTransfer)
            throw AppException.FromError(RentalErrors.TransferRequestAlreadyPending);

        var targetUnit = await _dbContext.StorageUnits
            .FirstOrDefaultAsync(u => u.FacilityId == request.RequestedFacilityId &&
                                      u.UnitTypeId == request.RequestedUnitTypeId &&
                                      u.PhysicalStatus == StorageUnitStatusConstants.Available,
                cancellationToken);

        if (targetUnit is null)
            throw AppException.FromError(RentalErrors.TargetUnitTypeNotFound);

        var total = decimal.Round(diffRent, 0, MidpointRounding.AwayFromZero) + diffDeposit;
        var now = DateTimeOffset.UtcNow;
        var transfer = new UnitTransferRequest
        {
            AgreementId = agreementId,
            RequestedUnitTypeId = request.RequestedUnitTypeId,
            RequestedFacilityId = request.RequestedFacilityId,
            FromUnitId = activeAllocation.StorageUnitId,
            ToUnitId = targetUnit.Id,
            RequestedEffectiveDate = request.MoveOutDate,
            MoveOutDate = request.MoveOutDate,
            MoveInDate = request.MoveInDate,
            DiffRentAmount = decimal.Round(diffRent, 0, MidpointRounding.AwayFromZero),
            DiffDepositAmount = diffDeposit,
            TotalPaidAmount = total,
            Reason = request.Reason.Trim(),
            Status = total > 0 ? TransferRequestStatusConstants.PendingPayment : TransferRequestStatusConstants.Paid,
            RequestedBy = customerId,
            CreatedAt = now
        };

        // The destination is held immediately.  A pending-payment hold is released
        // by TransferExpiryWorker if no payment arrives within 15 minutes.
        targetUnit.PhysicalStatus = StorageUnitStatusConstants.Reserved;
        targetUnit.UpdatedAt = now;
        _dbContext.UnitTransferRequests.Add(transfer);
        await _dbContext.SaveChangesAsync(cancellationToken);

        long? invoiceId = null;
        string? paymentQrUrl = null;
        string? paymentContent = null;
        if (total > 0)
        {
            var invoice = new Invoice
            {
                InvoiceNo = $"INV-TRF-{now:yyyyMMdd}-{transfer.Id:D6}",
                CustomerId = agreement.CustomerId,
                AgreementId = agreement.Id,
                BillingPeriod = $"TRANSFER:{transfer.Id}",
                IssueDate = today,
                DueDate = today,
                Currency = PaymentConstants.CurrencyVnd,
                SubtotalAmount = total,
                TotalAmount = total,
                Status = InvoiceStatusConstants.Open,
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.Invoices.Add(invoice);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _dbContext.InvoiceLines.Add(new InvoiceLine { InvoiceId = invoice.Id, LineType = InvoiceLineTypeConstants.Rent, Description = $"Transfer price difference #{transfer.Id}", Quantity = 1, UnitPrice = transfer.DiffRentAmount, LineAmount = transfer.DiffRentAmount, Metadata = "{}" });
            _dbContext.InvoiceLines.Add(new InvoiceLine { InvoiceId = invoice.Id, LineType = InvoiceLineTypeConstants.Deposit, Description = $"Transfer deposit difference #{transfer.Id}", Quantity = 1, UnitPrice = transfer.DiffDepositAmount, LineAmount = transfer.DiffDepositAmount, Metadata = "{}" });
            await _dbContext.SaveChangesAsync(cancellationToken);
            invoiceId = invoice.Id;
            paymentContent = $"{_paymentSettings.SePay.InvoicePrefix}{invoice.Id}";
            var cfg = _paymentSettings.VietQr;
            paymentQrUrl = $"{cfg.BaseUrl.TrimEnd('/')}?bank={Uri.EscapeDataString(cfg.BankCode)}&acc={Uri.EscapeDataString(cfg.AccountNo)}&template={Uri.EscapeDataString(cfg.Template)}&showinfo={(cfg.ShowInfo ? "true" : "false")}&holder={Uri.EscapeDataString(cfg.AccountName)}&amount={(long)total}&memo={Uri.EscapeDataString(paymentContent)}";
        }

        var fromUnit = await _dbContext.StorageUnits.AsNoTracking().FirstOrDefaultAsync(u => u.Id == transfer.FromUnitId, cancellationToken);

        return new UnitTransferRequestDto(
            Id: transfer.Id,
            AgreementId: transfer.AgreementId,
            FromUnitId: transfer.FromUnitId,
            FromUnitCode: fromUnit?.UnitCode,
            RequestedUnitTypeId: transfer.RequestedUnitTypeId,
            RequestedUnitTypeName: targetType.Name,
            RequestedFacilityId: transfer.RequestedFacilityId,
            MoveOutDate: transfer.MoveOutDate,
            MoveInDate: transfer.MoveInDate,
            DiffRentAmount: transfer.DiffRentAmount,
            DiffDepositAmount: transfer.DiffDepositAmount,
            TotalPaidAmount: transfer.TotalPaidAmount,
            RescheduleCount: transfer.RescheduleCount,
            InvoiceId: invoiceId,
            PaymentQrUrl: paymentQrUrl,
            PaymentContent: paymentContent,
            Reason: transfer.Reason,
            Status: transfer.Status,
            CreatedAt: transfer.CreatedAt
        );
    }

    public async Task<UnitTransferRequestDto> RescheduleTransferAsync(
        long customerId,
        long transferRequestId,
        RescheduleTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _dbContext.UnitTransferRequests
            .Include(t => t.RequestedUnitType)
            .Include(t => t.FromUnit)
            .FirstOrDefaultAsync(t => t.Id == transferRequestId && t.RequestedBy == customerId, cancellationToken);

        if (transfer is null)
            throw AppException.FromError(RentalErrors.TransferRequestNotFound);

        if (transfer.Status == TransferRequestStatusConstants.MovedOutInTransit || transfer.Status == TransferRequestStatusConstants.Completed)
            throw AppException.FromError(RentalErrors.TransferMoveOutAlreadyConfirmed);

        if (transfer.Status != TransferRequestStatusConstants.PendingPayment &&
            transfer.Status != TransferRequestStatusConstants.Paid &&
            transfer.Status != TransferRequestStatusConstants.Pending &&
            transfer.Status != TransferRequestStatusConstants.Approved &&
            transfer.Status != TransferRequestStatusConstants.Scheduled)
            throw AppException.FromError(RentalErrors.TransferRequestNotFound);

        if (transfer.RescheduleCount >= RentalPolicyConstants.MaxTransferReschedules)
            throw AppException.FromError(RentalErrors.TransferRescheduleLimitReached);

        var now = DateTimeOffset.UtcNow;
        if (request.MoveOutDate != transfer.MoveOutDate && now > new DateTimeOffset(transfer.MoveOutDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddHours(-24))
            throw AppException.FromError(RentalErrors.TransferRescheduleDeadlinePassed);
        if (request.MoveInDate != transfer.MoveInDate && now > new DateTimeOffset(transfer.MoveInDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddHours(-24))
            throw AppException.FromError(RentalErrors.TransferRescheduleDeadlinePassed);

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if (request.MoveOutDate < today || request.MoveOutDate > request.MoveInDate ||
            request.MoveInDate.DayNumber - request.MoveOutDate.DayNumber > RentalPolicyConstants.MaxTransferTransitDays)
            throw AppException.FromError(RentalErrors.InvalidTransferSchedule);

        transfer.MoveOutDate = request.MoveOutDate;
        transfer.MoveInDate = request.MoveInDate;
        transfer.RequestedEffectiveDate = request.MoveOutDate;
        transfer.RescheduleCount++;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UnitTransferRequestDto(
            transfer.Id, transfer.AgreementId, transfer.FromUnitId, transfer.FromUnit?.UnitCode,
            transfer.RequestedUnitTypeId, transfer.RequestedUnitType?.Name, transfer.RequestedFacilityId,
            transfer.MoveOutDate, transfer.MoveInDate, transfer.DiffRentAmount, transfer.DiffDepositAmount,
            transfer.TotalPaidAmount, transfer.RescheduleCount, null, null, null, transfer.Reason, transfer.Status, transfer.CreatedAt);
    }
}
