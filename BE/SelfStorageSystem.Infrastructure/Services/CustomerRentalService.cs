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
    private static readonly HashSet<string> WeakPins = new(StringComparer.Ordinal)
    {
        "012345", "123456", "234567", "345678", "456789", "567890",
        "987654", "876543", "765432", "654321", "543210", "098765"
    };

    private const int MaxFailedPinAttempts = 5;
    private static readonly TimeSpan PinLockoutDuration = TimeSpan.FromMinutes(15);
    private const int QrTtlSeconds = 120; // 2 minutes to provide adequate scan time and clock skew buffer
    private const int ClockSkewLeewaySeconds = 30;

    private readonly SelfStorageDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<CustomerRentalService> _logger;

    public CustomerRentalService(
        SelfStorageDbContext dbContext,
        IMemoryCache cache,
        IOptions<JwtSettings> jwtOptions,
        ILogger<CustomerRentalService> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _jwtSettings = jwtOptions.Value;
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

        // Check Facility Business Hours (OpeningTime and ClosingTime)
        if (agreement.Facility?.OpeningTime != null && agreement.Facility?.ClosingTime != null)
        {
            var tzId = string.IsNullOrWhiteSpace(agreement.Facility.Timezone) ? "Asia/Ho_Chi_Minh" : agreement.Facility.Timezone;
            TimeZoneInfo tz;
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            }
            catch
            {
                tz = tzId.Equals("Asia/Ho_Chi_Minh", StringComparison.OrdinalIgnoreCase)
                    ? TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")
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
                _logger.LogWarning("Access credential request rejected for agreement {AgreementId}. Outside business hours ({Opening}-{Closing}). Current facility time: {CurrentTime}",
                    agreementId, opening, closing, currentTime);
                throw AppException.FromError(RentalErrors.OutsideBusinessHours);
            }
        }

        // BR-REN-03: Real-time debt check: overdue by > 1 day triggers suspended status
        var isOverdue = agreement.Invoices.Any(i =>
            i.DueDate < today.AddDays(-1) &&
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

        // Generate Time-based JWT Gate QR Token with 120 seconds TTL and 30s clock skew leeway
        var qrExpiresAt = now.AddSeconds(QrTtlSeconds);
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
            QrExpiresInSeconds = QrTtlSeconds,
            QrExpiresAt = qrExpiresAt,
            SuspendedReason = null
        };
    }

    public async Task<ChangePinResponseDto> ChangePinAsync(long customerId, long agreementId, ChangePinRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Anti-brute force check on failed attempts per agreement
        var lockoutKey = $"pin_change_failed_attempts:{agreementId}";
        if (_cache.TryGetValue(lockoutKey, out int failedAttempts) && failedAttempts >= MaxFailedPinAttempts)
        {
            _logger.LogWarning("Agreement {AgreementId} locked out from changing PIN due to too many failed attempts.", agreementId);
            throw AppException.FromError(RentalErrors.TooManyFailedPinAttempts);
        }

        // 2. Validate PIN format: exactly 6 digits
        if (string.IsNullOrWhiteSpace(request.NewPin) || !Regex.IsMatch(request.NewPin, @"^\d{6}$"))
        {
            throw AppException.FromError(RentalErrors.InvalidPinFormat);
        }

        // 3. Prevent weak PINs: all same digits or simple ascending/descending sequences
        if (request.NewPin.Distinct().Count() == 1 || WeakPins.Contains(request.NewPin))
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
            i.DueDate < today.AddDays(-1) &&
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
                _cache.Set(lockoutKey, newCount, PinLockoutDuration);
                _logger.LogWarning("Failed current PIN verification for agreement {AgreementId}. Attempt {AttemptCount}/{MaxAttempts}",
                    agreementId, newCount, MaxFailedPinAttempts);
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
            EstimatedSyncSeconds = 60
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
            new Claim("agreementId", agreement.Id.ToString()),
            new Claim("customerId", agreement.CustomerId.ToString()),
            new Claim("facilityId", agreement.FacilityId.ToString()),
            new Claim("unitId", unitId.ToString()),
            new Claim("agreementNo", agreement.AgreementNo),
            new Claim("type", "GATE_ACCESS"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        // Leeway time of ClockSkewLeewaySeconds (30s) prevents premature expiration due to clock drift between server and IoT scanners
        var notBefore = DateTime.UtcNow.AddSeconds(-ClockSkewLeewaySeconds);

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
            if (pin.Distinct().Count() > 1 && !WeakPins.Contains(pin))
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
            Note: "Estimated net refund is subject to staff final on-site inspection and deduction of any pending damages/overdue balances per BR-FIN-02."
        );
    }

    public async Task<RenewAgreementResponseDto> RenewAgreementAsync(
        long customerId,
        long agreementId,
        RenewAgreementRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.RenewalMonths < 1 || request.RenewalMonths > 12)
            throw AppException.FromError(RentalErrors.InvalidRenewalMonths);

        var agreement = await _dbContext.RentalAgreements
            .FirstOrDefaultAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (agreement == null)
            throw AppException.FromError(RentalErrors.AgreementNotFound);

        var pendingRenewal = await _dbContext.RentalRenewals
            .AnyAsync(r => r.AgreementId == agreementId && r.Status == RenewalStatusConstants.Requested, cancellationToken);

        if (pendingRenewal)
            throw AppException.FromError(RentalErrors.RenewalAlreadyPending);

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
            Status = RenewalStatusConstants.Requested,
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

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw AppException.FromError(RentalErrors.EmptyTransferReason);

        var pendingTransfer = await _dbContext.UnitTransferRequests
            .AnyAsync(t => t.AgreementId == agreementId && t.Status == TransferRequestStatusConstants.Requested, cancellationToken);

        if (pendingTransfer)
            throw AppException.FromError(RentalErrors.TransferRequestAlreadyPending);

        var transfer = new UnitTransferRequest
        {
            AgreementId = agreementId,
            RequestedUnitTypeId = request.RequestedUnitTypeId,
            FromUnitId = activeAllocation.StorageUnitId,
            RequestedEffectiveDate = request.RequestedEffectiveDate,
            Reason = request.Reason.Trim(),
            Status = TransferRequestStatusConstants.Requested,
            RequestedBy = customerId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.UnitTransferRequests.Add(transfer);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var fromUnit = await _dbContext.StorageUnits.AsNoTracking().FirstOrDefaultAsync(u => u.Id == transfer.FromUnitId, cancellationToken);

        return new UnitTransferRequestDto(
            Id: transfer.Id,
            AgreementId: transfer.AgreementId,
            FromUnitId: transfer.FromUnitId,
            FromUnitCode: fromUnit?.UnitCode,
            RequestedUnitTypeId: transfer.RequestedUnitTypeId,
            RequestedUnitTypeName: targetType.Name,
            RequestedEffectiveDate: transfer.RequestedEffectiveDate,
            Reason: transfer.Reason,
            Status: transfer.Status,
            CreatedAt: transfer.CreatedAt
        );
    }
}
