using SelfStorageSystem.Infrastructure.Common;
using System.Text.Encodings.Web;
using System.Text.Json;
using SelfStorageSystem.Contracts.Customer;

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



public partial class CustomerRentalService : ICustomerRentalService

{

    private static readonly Regex PinFormatRegex = new(@"^\d{6}$", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions BankJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };



    private readonly SelfStorageDbContext _dbContext;

    private readonly IMemoryCache _cache;

    private readonly JwtSettings _jwtSettings;

    private readonly RentalSettings _rentalSettings;

    private readonly PaymentSettings _paymentSettings;

    private readonly ILogger<CustomerRentalService> _logger;

    private readonly IQrTokenService? _qrTokenService;

    private readonly IPricingEngine? _pricingEngine;

    private readonly IRealtimeNotifier? _realtimeNotifier;



    public CustomerRentalService(

        SelfStorageDbContext dbContext,

        IMemoryCache cache,

        IOptions<JwtSettings> jwtOptions,

        ILogger<CustomerRentalService> logger,

        IOptions<RentalSettings>? rentalOptions = null,

        IOptions<PaymentSettings>? paymentOptions = null,

        IQrTokenService? qrTokenService = null,

        IPricingEngine? pricingEngine = null,

        IRealtimeNotifier? realtimeNotifier = null)

    {

        _dbContext = dbContext;

        _cache = cache;

        _jwtSettings = jwtOptions.Value;

        _rentalSettings = rentalOptions?.Value ?? new RentalSettings();

        _paymentSettings = paymentOptions?.Value ?? new PaymentSettings();

        _logger = logger;

        _qrTokenService = qrTokenService;

        _pricingEngine = pricingEngine;

        _realtimeNotifier = realtimeNotifier;

    }



    public async Task<IReadOnlyList<RentalSummaryDto>> GetMyRentalsAsync(long customerId, CancellationToken cancellationToken = default)
        => await GetMyRentalsV1Async(customerId, null, cancellationToken);

    private async Task<IReadOnlyList<RentalSummaryDto>> QueryMyRentalsAsync(long customerId, string? statusFilter, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var allowedStatuses = ParseRentalStatusFilter(statusFilter);
        var agreements = await _dbContext.RentalAgreements
            .AsNoTracking()
            .Where(r => r.CustomerId == customerId &&
                        (allowedStatuses == null || allowedStatuses.Contains(r.Status)))

            .Include(r => r.Facility)

            .Include(r => r.UnitAllocations)

                .ThenInclude(a => a.StorageUnit)

                    .ThenInclude(u => u.UnitType)

            .Include(r => r.Invoices)
                .ThenInclude(i => i.InvoiceLines)
            .Include(r => r.Inspections)
                .ThenInclude(i => i.InspectionItems)

            .OrderByDescending(r => r.StartDate)

            .ToListAsync(cancellationToken);



        var reservationIds = agreements.Where(r => r.ReservationId > 0).Select(r => r.ReservationId).Distinct().ToList();

        var reservationsMap = reservationIds.Count > 0

            ? await _dbContext.Reservations.AsNoTracking()

                .Where(res => reservationIds.Contains(res.Id))

                .Include(res => res.UnitAllocation).ThenInclude(ua => ua!.StorageUnit).ThenInclude(u => u.UnitType)

                .Include(res => res.UnitType)

                .ToDictionaryAsync(res => res.Id, cancellationToken)

            : new Dictionary<long, Reservation>();



        var result = agreements.Select(r =>

        {

            var reservation = r.ReservationId > 0 && reservationsMap.TryGetValue(r.ReservationId, out var resVal) ? resVal : null;

            var alloc = r.UnitAllocations.FirstOrDefault(a => a.Status == AllocationStatusConstants.Active)
                     ?? r.UnitAllocations.OrderByDescending(a => a.Id).FirstOrDefault()
                     ?? reservation?.UnitAllocation;

            var unit = alloc?.StorageUnit;

            var unitType = unit?.UnitType ?? reservation?.UnitType;



            var facilityTz = TimeZoneHelper.ResolveTimeZone(r.Facility?.Timezone, _rentalSettings.DefaultTimezone);
            var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, facilityTz).DateTime);

            var hasOverdueDebt = r.Invoices.Any(i => IsOverdueRentalDebt(i, localToday.AddDays(-1)))
                                 || string.Equals(r.Status, RentalAgreementStatusConstants.Overdue, StringComparison.OrdinalIgnoreCase);

            var daysUntilExpiry = Math.Max(0, r.EndDate.DayNumber - localToday.DayNumber);
            var isExpiringSoon = string.Equals(r.Status, RentalAgreementStatusConstants.ExpiringSoon, StringComparison.OrdinalIgnoreCase)
                                 || (daysUntilExpiry <= 7 && (r.Status == RentalAgreementStatusConstants.Active || r.Status == "extended"));



            var dimensions = unitType != null

                ? $"{unitType.WidthM:F1}m x {unitType.LengthM:F1}m x {unitType.HeightM:F1}m"

                : string.Empty;



            var penaltyInvoice = r.Invoices?
                .Where(i => (i.Status == InvoiceStatusConstants.Open || i.Status == InvoiceStatusConstants.Overdue) &&
                            (i.InvoiceNo.StartsWith("INV-PEN-", StringComparison.OrdinalIgnoreCase) ||
                             i.InvoiceLines.Any(l => string.Equals(l.LineType, "damage", StringComparison.OrdinalIgnoreCase) ||
                                                     string.Equals(l.LineType, "penalty", StringComparison.OrdinalIgnoreCase))))
                .OrderByDescending(i => i.Id)
                .FirstOrDefault();

            var penaltyAmount = penaltyInvoice != null ? Math.Max(0m, penaltyInvoice.TotalAmount - penaltyInvoice.PaidAmount) : 0m;
            var latestInspection = r.Inspections?
                .OrderByDescending(i => i.CreatedAt)
                .FirstOrDefault();

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
                DaysUntilExpiry = daysUntilExpiry,
                IsExpiringSoon = isExpiringSoon,
                ImageUrl = !string.IsNullOrWhiteSpace(unit?.Notes) && System.Text.RegularExpressions.Regex.Match(unit.Notes, @"\[IMG:([^\]]+)\]").Success
                    ? System.Text.RegularExpressions.Regex.Match(unit.Notes, @"\[IMG:([^\]]+)\]").Groups[1].Value.Trim()
                    : null,
                PenaltyInvoiceId = penaltyInvoice?.Id,
                PenaltyInvoiceNo = penaltyInvoice?.InvoiceNo,
                PenaltyAmount = penaltyAmount,
                LatestInspectionSummary = latestInspection?.Summary
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
        var tzCred = TimeZoneHelper.ResolveTimeZone(agreement.Facility?.Timezone, _rentalSettings.DefaultTimezone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, tzCred).DateTime);



        // Check Facility Business Hours (OpeningTime and ClosingTime) if enforced

        if (_rentalSettings.EnforceOperatingHours &&

            agreement.Facility?.OpeningTime != null &&

            agreement.Facility?.ClosingTime != null)

        {

            var tz = TimeZoneHelper.ResolveTimeZone(agreement.Facility?.Timezone, _rentalSettings.DefaultTimezone);
            var localNow = TimeZoneInfo.ConvertTime(now, tz);

            var currentTime = TimeOnly.FromTimeSpan(localNow.TimeOfDay);

            var opening = agreement.Facility!.OpeningTime.Value;

            var closing = agreement.Facility!.ClosingTime.Value;



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

        var isOverdue = agreement.Invoices.Any(i => IsOverdueRentalDebt(i, today))
                        || string.Equals(agreement.Status, RentalAgreementStatusConstants.Overdue, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(agreement.Status, "defaulted", StringComparison.OrdinalIgnoreCase);



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
                CredentialId = pinCred?.Id ?? 0,
                CredentialType = pinCred?.CredentialType ?? CredentialTypeConstants.Pin,
                DisplayHint = null,
                AgreementId = agreement.Id,
                AgreementNo = agreement.AgreementNo,
                UnitCode = unitCode,
                FacilityName = facilityName,
                Status = CredentialStatusConstants.Suspended,
                IssuedAt = pinCred?.IssuedAt ?? agreement.CreatedAt,
                ExpiresAt = pinCred?.ExpiresAt,
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

                DisplayHint = PinProtectionHelper.Encrypt(defaultPin),

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



        // Generate Gate QR Token (canonical HMAC v1 token when IQrTokenService is available)
        var qrExpiresAt = now.AddSeconds(_rentalSettings.QrTtlSeconds);
        var gateQrToken = _qrTokenService != null
            ? _qrTokenService.IssueToken(
                "gate",
                agreement.ReservationId > 0 ? agreement.ReservationId : null,
                agreement.Id,
                customerId,
                agreement.FacilityId,
                TimeSpan.FromSeconds(_rentalSettings.QrTtlSeconds))
            : GenerateGateAccessJwt(agreement, activeAlloc?.StorageUnitId ?? 0, qrExpiresAt);

        var decryptedPin = PinProtectionHelper.Decrypt(pinCred.DisplayHint);
        if (string.IsNullOrEmpty(decryptedPin))
        {
            decryptedPin = GenerateSecureNumericPin();
            pinCred.SecretDigest = BCrypt.Net.BCrypt.HashPassword(decryptedPin);
            pinCred.DisplayHint = PinProtectionHelper.Encrypt(decryptedPin);
            pinCred.Status = CredentialStatusConstants.Active;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(RentalLogMessages.AccessCredentialsRetrieved, customerId, agreementId, CredentialStatusConstants.Active);

        return new AccessCredentialDto
        {
            CredentialId = pinCred.Id,
            CredentialType = pinCred.CredentialType,
            DisplayHint = !string.IsNullOrEmpty(decryptedPin) && decryptedPin.Length >= 2
                ? $"PIN ****{decryptedPin[^2..]}"
                : null,
            AgreementId = agreement.Id,
            AgreementNo = agreement.AgreementNo,
            UnitCode = unitCode,
            FacilityName = facilityName,
            Status = CredentialStatusConstants.Active,
            IssuedAt = pinCred.IssuedAt,
            ExpiresAt = pinCred.ExpiresAt,
            KeypadPin = decryptedPin,
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

        var facilityTzStr = await _dbContext.Facilities
            .AsNoTracking()
            .Where(f => f.Id == agreement.FacilityId)
            .Select(f => f.Timezone)
            .FirstOrDefaultAsync(cancellationToken);

        // 5. BR-REN-03: If access is suspended due to debt/overdue/suspended credential, reject PIN change
        var now = DateTimeOffset.UtcNow;
        var tzCred = TimeZoneHelper.ResolveTimeZone(facilityTzStr, _rentalSettings.DefaultTimezone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, tzCred).DateTime);

        var pinCred = agreement.AccessCredentials.FirstOrDefault(c => c.CredentialType == CredentialTypeConstants.Pin && c.AuthorizedMemberId == null)
                      ?? agreement.AccessCredentials.FirstOrDefault(c => c.CredentialType == CredentialTypeConstants.Pin);

        var isOverdue = agreement.Invoices.Any(i => IsOverdueRentalDebt(i, today))
                        || string.Equals(agreement.Status, RentalAgreementStatusConstants.Overdue, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(agreement.Status, "defaulted", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(agreement.Status, RentalAgreementStatusConstants.Terminated, StringComparison.OrdinalIgnoreCase)
                        || pinCred?.Status == CredentialStatusConstants.Suspended;

        if (isOverdue)
        {
            throw AppException.FromError(RentalErrors.AccessSuspendedDueToOverdue);
        }

        // 6. Verify current PIN if credential exists and current PIN is provided



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



        // 7. Update PIN securely and activate immediately
        if (pinCred is null)
        {
            pinCred = new AccessCredential
            {
                AgreementId = agreement.Id,
                CredentialType = CredentialTypeConstants.Pin,
                SecretDigest = BCrypt.Net.BCrypt.HashPassword(request.NewPin),
                DisplayHint = PinProtectionHelper.Encrypt(request.NewPin),
                Status = CredentialStatusConstants.Active,
                IssuedAt = now,
                CreatedAt = now
            };
            _dbContext.AccessCredentials.Add(pinCred);
        }
        else
        {
            pinCred.SecretDigest = BCrypt.Net.BCrypt.HashPassword(request.NewPin);
            pinCred.DisplayHint = PinProtectionHelper.Encrypt(request.NewPin);
            pinCred.Status = CredentialStatusConstants.Active;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(RentalLogMessages.PinChanged, customerId, agreementId);

        return new ChangePinResponseDto
        {
            Success = true,
            SyncStatus = PinSyncStatusConstants.Synced,
            Message = RentalPinMessages.PinSyncCompleted,
            EstimatedSyncSeconds = 0,
            ChangedAt = now
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
        var tzCred = TimeZoneHelper.ResolveTimeZone(agreement.Facility?.Timezone, _rentalSettings.DefaultTimezone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, tzCred).DateTime);



        // 5. Check Facility Business Hours if enforced

        if (_rentalSettings.EnforceOperatingHours &&

            agreement.Facility?.OpeningTime != null &&

            agreement.Facility?.ClosingTime != null)

        {

            var tz = TimeZoneHelper.ResolveTimeZone(agreement.Facility?.Timezone, _rentalSettings.DefaultTimezone);
            var localNow = TimeZoneInfo.ConvertTime(now, tz);

            var currentTime = TimeOnly.FromTimeSpan(localNow.TimeOfDay);

            var opening = agreement.Facility!.OpeningTime.Value;

            var closing = agreement.Facility!.ClosingTime.Value;



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

        var isOverdue = agreement.Invoices.Any(i => IsOverdueRentalDebt(i, today))
                        || string.Equals(agreement.Status, RentalAgreementStatusConstants.Overdue, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(agreement.Status, "defaulted", StringComparison.OrdinalIgnoreCase);



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

            var decryptedHint = PinProtectionHelper.Decrypt(pinCred.DisplayHint);
            isPinValid = !string.IsNullOrEmpty(decryptedHint) && request.Pin == decryptedHint;

        }



        var accessPoint = await ResolveAccessPointForUnlockAsync(agreement.FacilityId, request.AccessPointId, cancellationToken);

        if (!isPinValid)
        {
            var newCount = failedAttempts + 1;
            _cache.Set(lockoutKey, newCount, TimeSpan.FromMinutes(_rentalSettings.PinLockoutDurationMinutes));
            if (accessPoint != null)
            {
                await RecordUnlockAccessEventAsync(pinCred.Id, accessPoint.Id, "denied", "Invalid PIN", now, cancellationToken);
            }
            _logger.LogWarning(RentalLogMessages.FailedUnlockVerification,
                agreementId, newCount, _rentalSettings.MaxFailedPinAttempts);
            throw AppException.FromError(RentalErrors.IncorrectPin);
        }

        // Reset lockout count on success
        _cache.Remove(lockoutKey);

        var activeAlloc = agreement.UnitAllocations.FirstOrDefault(a => a.Status == AllocationStatusConstants.Active);
        var unitCode = activeAlloc?.StorageUnit?.UnitCode ?? string.Empty;
        var facilityName = agreement.Facility?.Name ?? string.Empty;

        AccessEvent? accessEvent = null;
        if (accessPoint != null)
        {
            accessEvent = await RecordUnlockAccessEventAsync(
                pinCred.Id,
                accessPoint.Id,
                "granted",
                $"Unit {unitCode} unlocked by customer",
                now,
                cancellationToken);
        }

        _logger.LogInformation(RentalLogMessages.UnitUnlockedSuccess,
            customerId, unitCode, agreementId);

        return new UnlockStorageUnitResponseDto
        {
            Success = true,
            AgreementId = agreement.Id,
            UnitCode = unitCode,
            FacilityName = facilityName,
            UnlockedAt = now,
            AccessEventId = accessEvent?.Id,
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

            .Include(h => h.Agreement).ThenInclude(a => a.Facility)
            .Include(h => h.Agreement).ThenInclude(a => a.Customer).ThenInclude(c => c.User)
            .Include(h => h.UnitAllocation).ThenInclude(a => a.StorageUnit).ThenInclude(u => u.UnitType)
            .Include(h => h.UnitAllocation).ThenInclude(a => a.StorageUnit).ThenInclude(u => u.Facility)

            .Include(h => h.HandledByNavigation)

            .Include(h => h.Inspection)

                .ThenInclude(i => i.InspectionItems)

            .AsNoTracking()

            .Where(h => h.AgreementId == agreementId)
            .OrderByDescending(h => h.CreatedAt)
            .ThenByDescending(h => h.Id)
            .FirstOrDefaultAsync(cancellationToken);




        if (handover is null)

        {

            throw AppException.FromError(RentalErrors.HandoverNotFound);

        }



        _logger.LogInformation(RentalLogMessages.HandoverRecordRetrieved, customerId, agreementId);



        var unit = handover.UnitAllocation?.StorageUnit;
        var unitType = unit?.UnitType;
        return new HandoverRecordDto

        {

            HandoverId = handover.Id,

            AgreementId = handover.AgreementId,

            AgreementNo = handover.Agreement?.AgreementNo ?? string.Empty,

            HandoverType = handover.HandoverType,
            CustomerName = handover.Agreement?.Customer?.FullName,
            CustomerEmail = handover.Agreement?.Customer?.User?.Email,
            CustomerPhone = handover.Agreement?.Customer?.User?.PhoneNumber,
            FacilityName = unit?.Facility?.Name ?? handover.Agreement?.Facility?.Name,
            FacilityAddress = unit?.Facility?.AddressLine ?? handover.Agreement?.Facility?.AddressLine,
            FacilityCity = unit?.Facility?.City ?? handover.Agreement?.Facility?.City,
            UnitCode = unit?.UnitCode,
            UnitTypeName = unitType?.Name,
            FloorLabel = unit?.FloorLabel,
            ZoneLabel = unit?.ZoneLabel,
            Dimensions = unitType == null ? null : $"{unitType.WidthM:0.##}m × {unitType.LengthM:0.##}m × {unitType.HeightM:0.##}m",
            AreaM2 = unitType?.AreaM2,
            VolumeM3 = unitType?.VolumeM3,
            RentalStartDate = handover.Agreement?.StartDate,
            RentalEndDate = handover.Agreement?.EndDate,
            StaffEmployeeCode = handover.HandledByNavigation?.EmployeeCode,


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

                Items = handover.Inspection.InspectionItems.OrderBy(item => item.Id).Select(item => new HandoverInspectionItemDto

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



    private static bool IsOverdueRentalDebt(Invoice i, DateOnly cutoffDate)
    {
        if (i.DueDate >= cutoffDate)
        {
            return false;
        }

        // Only active, unpaid invoices constitute debt; voided, draft or paid invoices never block access
        if (i.Status != InvoiceStatusConstants.Open &&
            i.Status != InvoiceStatusConstants.PartiallyPaid &&
            i.Status != InvoiceStatusConstants.Overdue)
        {
            return false;
        }

        if ((i.TotalAmount - i.PaidAmount) <= 0)
        {
            return false;
        }

        // Transfer invoices are conditional payment for requesting a destination unit.
        // If unpaid, the transfer request is cancelled/expired, but it does NOT lock the active unit.
        var isTransfer = (!string.IsNullOrEmpty(i.BillingPeriod) && i.BillingPeriod.StartsWith(TransferConstants.BillingPeriodPrefix, StringComparison.OrdinalIgnoreCase))
                      || (!string.IsNullOrEmpty(i.InvoiceNo) && i.InvoiceNo.StartsWith(TransferConstants.InvoicePrefix, StringComparison.OrdinalIgnoreCase));
        if (isTransfer)
        {
            return false;
        }

        return true;
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

        var moveFacilityTz = await _dbContext.Facilities
            .AsNoTracking()
            .Where(f => f.Id == agreement.FacilityId)
            .Select(f => f.Timezone)
            .FirstOrDefaultAsync(cancellationToken);
        var tzMove = TimeZoneHelper.ResolveTimeZone(moveFacilityTz, _rentalSettings.DefaultTimezone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tzMove).DateTime);

        if (request.RequestedMoveOutDate < today)

            throw AppException.FromError(RentalErrors.InvalidMoveOutDate);



        var existingPendingMoveOut = await _dbContext.MoveOutRequests

            .AnyAsync(m => m.AgreementId == agreementId && 

                           (m.Status == MoveOutStatusConstants.Requested || m.Status == MoveOutStatusConstants.Scheduled), 

                      cancellationToken);



        if (existingPendingMoveOut)

            throw AppException.FromError(RentalErrors.MoveOutAlreadyRequested);



        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;

        if (_dbContext.Database.IsSqlServer())

        {

            transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        }



        try

        {

            var nowMoveOut = DateTimeOffset.UtcNow;
            var bankName = request.BankName?.Trim();
            var bankAccountNo = request.ResolvedBankAccountNo;
            var bankAccountHolder = request.BankAccountHolder?.Trim();

            string? moveOutReason = request.Reason?.Trim();
            if (!string.IsNullOrWhiteSpace(bankName) || !string.IsNullOrWhiteSpace(bankAccountNo))
            {
                var note = request.Reason?.Trim();
                var bankData = new
                {
                    bank = bankName,
                    acc = bankAccountNo,
                    holder = bankAccountHolder,
                    note = note
                };
                var json = JsonSerializer.Serialize(bankData, BankJsonOptions);
                if (json.Length <= 250)
                {
                    moveOutReason = json;
                }
                else
                {
                    int availableForNote = Math.Max(0, 250 - (json.Length - (note?.Length ?? 0)));
                    if (availableForNote > 10 && !string.IsNullOrEmpty(note))
                    {
                        var truncatedData = new
                        {
                            bank = bankName,
                            acc = bankAccountNo,
                            holder = bankAccountHolder,
                            note = note.Substring(0, Math.Min(note.Length, availableForNote - 5))
                        };
                        var truncatedJson = JsonSerializer.Serialize(truncatedData, BankJsonOptions);
                        if (truncatedJson.Length <= 250)
                        {
                            moveOutReason = truncatedJson;
                        }
                    }

                    if (moveOutReason == null || moveOutReason.Length > 250 || !moveOutReason.StartsWith("{"))
                    {
                        var compact = $"BANK:{bankName}|ACC:{bankAccountNo}|NAME:{bankAccountHolder}";
                        moveOutReason = compact.Length <= 250 ? compact : compact.Substring(0, 250);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(moveOutReason) && moveOutReason.Length > 250)
            {
                moveOutReason = moveOutReason.Substring(0, 250);
            }

            var moveOut = new MoveOutRequest
            {
                AgreementId = agreementId,
                RequestedBy = customerId,
                RequestedMoveOutDate = request.RequestedMoveOutDate,
                Reason = moveOutReason,
                BankName = bankName,
                BankAccountNo = bankAccountNo,
                BankAccountHolder = bankAccountHolder,
                BankSubmittedAt = (!string.IsNullOrWhiteSpace(bankName) || !string.IsNullOrWhiteSpace(bankAccountNo)) ? nowMoveOut : null,
                Status = MoveOutStatusConstants.Requested,
                CreatedAt = nowMoveOut,
                UpdatedAt = nowMoveOut
            };

            _dbContext.MoveOutRequests.Add(moveOut);

            var existingPendingRefund = await _dbContext.Refunds
                .Where(r => r.AgreementId == agreementId && (r.Status == RefundStatusConstants.Pending || r.Status == RefundStatusConstants.Requested))
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingPendingRefund != null && moveOutReason != null && moveOutReason.StartsWith("{"))
            {
                existingPendingRefund.Reason = moveOutReason;
                existingPendingRefund.UpdatedAt = nowMoveOut;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            var moveOutQrToken = _qrTokenService?.IssueToken(
                "move_out",
                agreement.ReservationId > 0 ? agreement.ReservationId : null,
                agreement.Id,
                customerId,
                agreement.FacilityId,
                TimeSpan.FromDays(14));

            return new MoveOutResponseDto(
                Id: moveOut.Id,
                AgreementId: moveOut.AgreementId,
                RequestedMoveOutDate: moveOut.RequestedMoveOutDate,
                Status: moveOut.Status,
                Reason: moveOut.Reason,
                CreatedAt: moveOut.CreatedAt,
                MoveOutRequestId: moveOut.Id,
                MoveOutQrToken: moveOutQrToken,
                BankName: moveOut.BankName,
                BankAccountNo: moveOut.BankAccountNo,
                BankAccountHolder: moveOut.BankAccountHolder
            );

        }

        catch

        {

            if (transaction != null)

            {

                await transaction.RollbackAsync(cancellationToken);

            }

            throw;

        }

        finally

        {

            if (transaction != null)

            {

                await transaction.DisposeAsync();

            }

        }

    }



    public Task<RefundPreviewDto> GetRefundPreviewAsync(
        long customerId,
        long agreementId,
        CancellationToken cancellationToken = default)
        => GetRefundPreviewAsync(customerId, agreementId, null, cancellationToken);

    public Task<RenewAgreementResponseDto> RenewAgreementAsync(
        long customerId,
        long agreementId,
        RenewAgreementRequest request,
        CancellationToken cancellationToken = default)
        => RenewAgreementV1InternalAsync(customerId, agreementId, request, cancellationToken);



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



    public Task<AuthorizedMemberDto> AddAuthorizedMemberAsync(
        long customerId,
        long agreementId,
        CreateAuthorizedMemberRequest request,
        CancellationToken cancellationToken = default)
        => AddAuthorizedMemberV1InternalAsync(customerId, agreementId, request, cancellationToken);

    public async Task RevokeAuthorizedMemberAsync(
        long customerId,
        long agreementId,
        long memberId,
        CancellationToken cancellationToken = default)
    {
        await RevokeAuthorizedMemberV1Async(customerId, agreementId, memberId, cancellationToken);
    }



    public async Task<TransferPreviewDto> GetTransferPreviewAsync(
        long customerId,
        long agreementId,
        long? requestedFacilityId = null,
        long? requestedUnitTypeId = null,
        DateOnly? moveOutDate = null,
        CancellationToken cancellationToken = default)
    {
        var agreement = await _dbContext.RentalAgreements
            .Include(a => a.Facility)
            .Include(a => a.UnitAllocations)
                .ThenInclude(u => u.StorageUnit)
                    .ThenInclude(su => su.UnitType)
            .FirstOrDefaultAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (agreement == null)
            throw AppException.FromError(RentalErrors.AgreementNotFound);

        var activeAllocation = agreement.UnitAllocations.FirstOrDefault(u => u.Status == AllocationStatusConstants.Active);
        var currentUnitType = activeAllocation?.StorageUnit?.UnitType;
        var currentFacilityName = agreement.Facility?.Name ?? "Cơ sở hiện tại";

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneHelper.ResolveTimeZone(_rentalSettings.DefaultTimezone)).DateTime);
        var hasOverdueDebt = await _dbContext.Invoices.AnyAsync(i =>
            i.AgreementId == agreementId &&
            (i.Status == InvoiceStatusConstants.Overdue || (i.Status == InvoiceStatusConstants.Open && i.DueDate < today)) &&
            (i.BillingPeriod == null || !i.BillingPeriod.StartsWith(TransferConstants.BillingPeriodPrefix)) &&
            (i.InvoiceNo == null || !i.InvoiceNo.StartsWith(TransferConstants.InvoicePrefix)),
            cancellationToken);

        var transferCount = await _dbContext.UnitTransferRequests
            .CountAsync(t => t.AgreementId == agreementId 
                          && t.Status != TransferRequestStatusConstants.Cancelled 
                          && t.Status != TransferRequestStatusConstants.Rejected 
                          && t.Status != TransferRequestStatusConstants.CancelledOverdueMoveOut, 
                        cancellationToken);

        var pendingTransfer = await _dbContext.UnitTransferRequests
            .AnyAsync(t => t.AgreementId == agreementId 
                        && t.CompletedAt == null 
                        && t.Status != TransferRequestStatusConstants.Completed 
                        && t.Status != TransferRequestStatusConstants.Cancelled 
                        && t.Status != TransferRequestStatusConstants.Rejected 
                        && t.Status != TransferRequestStatusConstants.CancelledOverdueMoveOut, 
                      cancellationToken);

        bool canTransfer = true;
        string? disabledReason = null;

        if (activeAllocation == null)
        {
            canTransfer = false;
            disabledReason = "Hợp đồng chưa nhận bàn giao ô kho (Chưa check-in).";
        }
        else if (hasOverdueDebt)
        {
            canTransfer = false;
            disabledReason = "Hợp đồng đang có hóa đơn quá hạn chưa thanh toán.";
        }
        else if (pendingTransfer)
        {
            canTransfer = false;
            disabledReason = "Hợp đồng đang có yêu cầu chuyển kho đang chờ xử lý.";
        }
        else if (transferCount >= _rentalSettings.MaxTransfersPerAgreement)
        {
            canTransfer = false;
            disabledReason = $"Hợp đồng đã đạt giới hạn tối đa {_rentalSettings.MaxTransfersPerAgreement} lần chuyển kho.";
        }

        long resolvedFacilityId = requestedFacilityId ?? agreement.FacilityId;
        long? targetFacilityId = resolvedFacilityId;
        string? targetFacilityName = null;
        long? targetUnitTypeId = requestedUnitTypeId;
        string? targetUnitTypeName = null;
        decimal? targetMonthlyRate = null;
        decimal? targetDeposit = null;
        decimal diffRent = 0m;
        decimal diffDeposit = 0m;
        decimal cleaningFee = 0m;
        decimal total = 0m;
        bool isDowngrade = false;
        bool isCrossFacility = resolvedFacilityId != agreement.FacilityId;

        var targetFacility = await _dbContext.Facilities.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == resolvedFacilityId, cancellationToken);
        targetFacilityName = targetFacility?.Name ?? currentFacilityName;

        int availableUnitsCount = 0;
        if (requestedUnitTypeId.HasValue)
        {
            var targetType = await _dbContext.UnitTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == requestedUnitTypeId.Value, cancellationToken);
            targetUnitTypeName = targetType?.Name;

            var targetRate = await _dbContext.FacilityRates
                .AsNoTracking()
                .Where(r => r.FacilityId == resolvedFacilityId && r.UnitTypeId == requestedUnitTypeId.Value && r.ValidFrom <= today && (r.ValidTo == null || r.ValidTo >= today))
                .OrderByDescending(r => r.ValidFrom)
                .FirstOrDefaultAsync(cancellationToken);

            if (targetRate != null)
            {
                targetMonthlyRate = targetRate.MonthlyRate;
                targetDeposit = targetRate.DepositAmount;
            }
            else
            {
                // Fallback 1: Lấy mức giá chuẩn từ PriceRanges
                var priceRange = await _dbContext.PriceRanges
                    .AsNoTracking()
                    .Where(p => p.UnitTypeId == requestedUnitTypeId.Value && p.ValidFrom <= today && (p.ValidTo == null || p.ValidTo >= today))
                    .OrderByDescending(p => p.ValidFrom)
                    .FirstOrDefaultAsync(cancellationToken);

                if (priceRange != null)
                {
                    targetMonthlyRate = priceRange.MinMonthlyRate > 0 ? priceRange.MinMonthlyRate : priceRange.MaxMonthlyRate;
                    targetDeposit = targetMonthlyRate;
                }
                else
                {
                    // Fallback 2: Lấy FacilityRate của cơ sở khác cùng loại kho
                    var anyRate = await _dbContext.FacilityRates
                        .AsNoTracking()
                        .Where(r => r.UnitTypeId == requestedUnitTypeId.Value && r.ValidFrom <= today && (r.ValidTo == null || r.ValidTo >= today))
                        .OrderByDescending(r => r.ValidFrom)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (anyRate != null)
                    {
                        targetMonthlyRate = anyRate.MonthlyRate;
                        targetDeposit = anyRate.DepositAmount;
                    }
                    else
                    {
                        // Fallback 3: Dùng giá hợp đồng hiện tại
                        targetMonthlyRate = agreement.MonthlyRateSnapshot;
                        targetDeposit = agreement.DepositBalance;
                    }
                }
            }

            if (targetMonthlyRate.HasValue)
            {
                var effectiveDeposit = targetDeposit ?? targetMonthlyRate.Value;
                var effectiveMoveOut = moveOutDate ?? today;
                var remainingDays = Math.Max(0, agreement.EndDate.DayNumber - effectiveMoveOut.DayNumber);

                diffRent = Math.Max(0, targetMonthlyRate.Value - agreement.MonthlyRateSnapshot) * remainingDays / 30m;
                diffDeposit = Math.Max(0, effectiveDeposit - agreement.DepositBalance);
                cleaningFee = isCrossFacility ? _rentalSettings.CrossFacilityCleaningFee : 0m;
                total = decimal.Round(diffRent, 0, MidpointRounding.AwayFromZero) + diffDeposit + cleaningFee;
                isDowngrade = targetMonthlyRate.Value < agreement.MonthlyRateSnapshot;
            }

            availableUnitsCount = await _dbContext.StorageUnits
                .AsNoTracking()
                .CountAsync(u => u.FacilityId == resolvedFacilityId 
                              && u.UnitTypeId == requestedUnitTypeId.Value 
                              && u.PhysicalStatus == StorageUnitStatusConstants.Available, 
                            cancellationToken);
        }

        return new TransferPreviewDto(
            AgreementId: agreement.Id,
            AgreementNo: agreement.AgreementNo,
            CurrentFacilityId: agreement.FacilityId,
            CurrentFacilityName: currentFacilityName,
            CurrentUnitTypeId: currentUnitType?.Id ?? 0,
            CurrentUnitTypeName: currentUnitType?.Name ?? "Hiện tại",
            CurrentMonthlyRate: agreement.MonthlyRateSnapshot,
            CurrentDeposit: agreement.DepositBalance,
            TransferCount: transferCount,
            MaxTransfers: _rentalSettings.MaxTransfersPerAgreement,
            CanTransfer: canTransfer,
            DisabledReason: disabledReason,
            TargetFacilityId: targetFacilityId,
            TargetFacilityName: targetFacilityName,
            TargetUnitTypeId: targetUnitTypeId,
            TargetUnitTypeName: targetUnitTypeName,
            TargetMonthlyRate: targetMonthlyRate,
            TargetDeposit: targetDeposit,
            DiffRentAmount: decimal.Round(diffRent, 0, MidpointRounding.AwayFromZero),
            DiffDepositAmount: diffDeposit,
            CleaningFee: cleaningFee,
            TotalRequiredPayment: total,
            IsDowngrade: isDowngrade,
            IsCrossFacility: isCrossFacility,
            AvailableUnitsCount: availableUnitsCount
        );
    }

    public async Task<UnitTransferRequestDto?> GetLatestTransferRequestAsync(
        long customerId, long agreementId, CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.RentalAgreements.AnyAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken))
            throw AppException.FromError(RentalErrors.AgreementNotFound);

        var transfer = await _dbContext.UnitTransferRequests.AsNoTracking()
            .Include(t => t.FromUnit)
            .Include(t => t.RequestedUnitType)
            .Where(t => t.AgreementId == agreementId)
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (transfer == null) return null;

        var period = $"{TransferConstants.BillingPeriodPrefix}{transfer.Id}";
        var invoice = await _dbContext.Invoices.AsNoTracking()
            .Where(i => i.CustomerId == customerId && i.AgreementId == agreementId && i.BillingPeriod == period)
            .OrderByDescending(i => i.Id).FirstOrDefaultAsync(cancellationToken);
        var amountDue = invoice == null || invoice.Status == InvoiceStatusConstants.Voided
            ? 0 : Math.Max(0, invoice.TotalAmount - invoice.PaidAmount);
        var holdUntil = transfer.CreatedAt.AddMinutes(_rentalSettings.TransferUnpaidHoldMinutes);
        var prefix = string.IsNullOrWhiteSpace(_paymentSettings.SePay.InvoicePrefix)
            ? SePayConstants.DefaultInvoicePrefix : _paymentSettings.SePay.InvoicePrefix.Trim();
        var content = invoice == null ? null : $"{prefix}{invoice.Id}";
        var cfg = _paymentSettings.VietQr;
        var qr = transfer.Status == TransferRequestStatusConstants.PendingPayment && amountDue > 0 && holdUntil > DateTimeOffset.UtcNow
            ? VietQrUrlBuilder.Build(cfg, amountDue, content!) : null;
        return new UnitTransferRequestDto(
            Id: transfer.Id, AgreementId: transfer.AgreementId,
            FromUnitId: transfer.FromUnitId, FromUnitCode: transfer.FromUnit?.UnitCode,
            RequestedUnitTypeId: transfer.RequestedUnitTypeId, RequestedUnitTypeName: transfer.RequestedUnitType?.Name,
            RequestedFacilityId: transfer.RequestedFacilityId, MoveOutDate: transfer.MoveOutDate, MoveInDate: transfer.MoveInDate,
            DiffRentAmount: transfer.DiffRentAmount, DiffDepositAmount: transfer.DiffDepositAmount,
            CleaningFee: transfer.CleaningFee, TotalPaidAmount: transfer.TotalPaidAmount, RescheduleCount: transfer.RescheduleCount,
            InvoiceId: invoice?.Id, PaymentQrUrl: qr, PaymentContent: content, Reason: transfer.Reason,
            Status: transfer.Status, CreatedAt: transfer.CreatedAt,
            PaymentBankCode: cfg.BankCode, PaymentAccountNo: cfg.AccountNo, PaymentAccountName: cfg.AccountName,
            AmountDue: amountDue, PaidAmount: invoice?.PaidAmount ?? 0, HoldUntil: holdUntil);
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
        {
            agreement = await _dbContext.RentalAgreements
                .Include(a => a.UnitAllocations)
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync(a => a.CustomerId == customerId
                    && a.UnitAllocations.Any(ua => ua.StorageUnitId == agreementId || ua.Id == agreementId), cancellationToken);
        }

        if (agreement == null)
            throw AppException.FromError(RentalErrors.AgreementNotFound);

        agreementId = agreement.Id;



        var activeAllocation = agreement.UnitAllocations.FirstOrDefault(u => u.Status == AllocationStatusConstants.Active);
        if (activeAllocation == null)
            throw AppException.FromError(RentalErrors.AgreementNotCheckedIn);

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneHelper.ResolveTimeZone(_rentalSettings.DefaultTimezone)).DateTime);
        var hasOverdueDebt = await _dbContext.Invoices.AnyAsync(i =>
            i.AgreementId == agreementId && (i.Status == InvoiceStatusConstants.Overdue || (i.Status == InvoiceStatusConstants.Open && i.DueDate < today)), cancellationToken);
        if (hasOverdueDebt)
        {
            throw AppException.FromError(RentalErrors.TransferBlockedByOverdueDebt);
        }

        var transferCount = await _dbContext.UnitTransferRequests
            .CountAsync(t => t.AgreementId == agreementId 
                          && t.Status != TransferRequestStatusConstants.Cancelled 
                          && t.Status != TransferRequestStatusConstants.Rejected 
                          && t.Status != TransferRequestStatusConstants.CancelledOverdueMoveOut, 
                        cancellationToken);

        if (transferCount >= _rentalSettings.MaxTransfersPerAgreement)
            throw AppException.FromError(RentalErrors.TransferLimitReached);

        // Auto-cancel any previous un-paid transfer request for this agreement
        var uncompletedTransfers = await _dbContext.UnitTransferRequests
            .Include(t => t.ToUnit)
            .Where(t => t.AgreementId == agreementId 
                     && t.CompletedAt == null 
                     && t.Status != TransferRequestStatusConstants.Completed 
                     && t.Status != TransferRequestStatusConstants.Cancelled 
                     && t.Status != TransferRequestStatusConstants.Rejected 
                     && t.Status != TransferRequestStatusConstants.CancelledOverdueMoveOut)
            .ToListAsync(cancellationToken);

        foreach (var oldTransfer in uncompletedTransfers)
        {
            if (oldTransfer.Status == TransferRequestStatusConstants.PendingPayment)
            {
                oldTransfer.Status = TransferRequestStatusConstants.Cancelled;
                if (oldTransfer.ToUnit != null && oldTransfer.ToUnit.PhysicalStatus == StorageUnitStatusConstants.Reserved)
                {
                    oldTransfer.ToUnit.PhysicalStatus = StorageUnitStatusConstants.Available;
                    oldTransfer.ToUnit.UpdatedAt = DateTimeOffset.UtcNow;
                }
                var oldPeriod = $"{TransferConstants.BillingPeriodPrefix}{oldTransfer.Id}";
                var oldInvoices = await _dbContext.Invoices
                    .Where(i => i.BillingPeriod == oldPeriod && i.Status != InvoiceStatusConstants.Paid)
                    .ToListAsync(cancellationToken);
                foreach (var inv in oldInvoices)
                {
                    inv.Status = InvoiceStatusConstants.Voided;
                    inv.VoidedAt = DateTimeOffset.UtcNow;
                    inv.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }
            else
            {
                // A paid or scheduled transfer is already ongoing
                throw AppException.FromError(RentalErrors.TransferRequestAlreadyPending);
            }
        }

        if (uncompletedTransfers.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (request.MoveOutDate < today || request.MoveOutDate > request.MoveInDate || request.MoveInDate.DayNumber - request.MoveOutDate.DayNumber > RentalPolicyConstants.MaxTransferTransitDays)
            throw AppException.FromError(RentalErrors.InvalidTransferSchedule);

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw AppException.FromError(RentalErrors.EmptyTransferReason);

        var targetType = await _dbContext.UnitTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.RequestedUnitTypeId, cancellationToken);

        if (targetType == null)
            throw AppException.FromError(RentalErrors.TargetUnitTypeNotFound);

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

        decimal targetMonthlyRate;
        decimal targetDeposit;

        if (targetRate != null)
        {
            targetMonthlyRate = targetRate.MonthlyRate;
            targetDeposit = targetRate.DepositAmount;
        }
        else
        {
            // Fallback 1: Lấy mức giá chuẩn từ PriceRanges
            var priceRange = await _dbContext.PriceRanges
                .AsNoTracking()
                .Where(p => p.UnitTypeId == request.RequestedUnitTypeId && p.ValidFrom <= today && (p.ValidTo == null || p.ValidTo >= today))
                .OrderByDescending(p => p.ValidFrom)
                .FirstOrDefaultAsync(cancellationToken);

            if (priceRange != null)
            {
                targetMonthlyRate = priceRange.MinMonthlyRate > 0 ? priceRange.MinMonthlyRate : priceRange.MaxMonthlyRate;
                targetDeposit = targetMonthlyRate;
            }
            else
            {
                // Fallback 2: Lấy FacilityRate của cơ sở khác cùng loại kho
                var anyRate = await _dbContext.FacilityRates
                    .AsNoTracking()
                    .Where(r => r.UnitTypeId == request.RequestedUnitTypeId && r.ValidFrom <= today && (r.ValidTo == null || r.ValidTo >= today))
                    .OrderByDescending(r => r.ValidFrom)
                    .FirstOrDefaultAsync(cancellationToken);

                if (anyRate != null)
                {
                    targetMonthlyRate = anyRate.MonthlyRate;
                    targetDeposit = anyRate.DepositAmount;
                }
                else
                {
                    // Fallback 3: Dùng giá snapshot hợp đồng hiện tại
                    targetMonthlyRate = agreement.MonthlyRateSnapshot;
                    targetDeposit = agreement.DepositBalance;
                }
            }
        }

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        if (_dbContext.Database.IsSqlServer())
        {
            transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        long? invoiceId = null;
        string? paymentQrUrl = null;
        string? paymentContent = null;
        UnitTransferRequest transfer;

        try
        {
            StorageUnit? targetUnit = null;
            if (_dbContext.Database.IsSqlServer())
            {
                targetUnit = await _dbContext.StorageUnits
                    .FromSqlInterpolated($"SELECT TOP 1 * FROM [core].[storage_units] WITH (UPDLOCK, ROWLOCK, READPAST) WHERE facility_id = {request.RequestedFacilityId} AND unit_type_id = {request.RequestedUnitTypeId} AND is_listed = 1 AND physical_status = {StorageUnitStatusConstants.Available}")
                    .FirstOrDefaultAsync(cancellationToken);
            }
            else
            {
                targetUnit = await _dbContext.StorageUnits
                    .FirstOrDefaultAsync(u => u.FacilityId == request.RequestedFacilityId &&
                                              u.UnitTypeId == request.RequestedUnitTypeId &&
                                              u.PhysicalStatus == StorageUnitStatusConstants.Available,
                        cancellationToken);
            }

            if (targetUnit is null)
                throw AppException.FromError(RentalErrors.TargetUnitUnavailable);

            var hasActiveAllocation = await _dbContext.UnitAllocations
                .AnyAsync(ua => ua.StorageUnitId == targetUnit.Id &&
                                (ua.Status == AllocationStatusConstants.Active),
                          cancellationToken);
            if (hasActiveAllocation)
                throw AppException.FromError(RentalErrors.TargetUnitUnavailable);

            var remainingDays = Math.Max(0, agreement.EndDate.DayNumber - request.MoveOutDate.DayNumber);
            var diffRent = Math.Max(0, targetMonthlyRate - agreement.MonthlyRateSnapshot) * remainingDays / 30m;
            var diffDeposit = Math.Max(0, targetDeposit - agreement.DepositBalance);
            var isCrossFacility = request.RequestedFacilityId != agreement.FacilityId;
            var cleaningFee = isCrossFacility ? _rentalSettings.CrossFacilityCleaningFee : 0m;
            var total = decimal.Round(diffRent, 0, MidpointRounding.AwayFromZero) + diffDeposit + cleaningFee;
            var isDowngrade = targetMonthlyRate < agreement.MonthlyRateSnapshot && total <= 0m;

            var now = DateTimeOffset.UtcNow;

            transfer = new UnitTransferRequest
            {
                AgreementId = agreementId,
                RequestedUnitTypeId = request.RequestedUnitTypeId,
                RequestedFacilityId = request.RequestedFacilityId,
                FromUnitId = activeAllocation.StorageUnitId,
                ToUnitId = targetUnit.Id,
                TransferType = isDowngrade ? "downgrade" : (total > 0 ? "upgrade" : "same_tier"),
                RequestedEffectiveDate = request.MoveOutDate,
                MoveOutDate = request.MoveOutDate,
                MoveInDate = request.MoveInDate,
                DiffRentAmount = decimal.Round(diffRent, 0, MidpointRounding.AwayFromZero),
                DiffDepositAmount = diffDeposit,
                CleaningFee = cleaningFee,
                TotalPaidAmount = total,
                Reason = request.Reason.Trim(),
                Status = total > 0 ? TransferRequestStatusConstants.PendingPayment : TransferRequestStatusConstants.Completed,
                CompletedAt = total > 0 ? null : now,
                RequestedBy = customerId,
                CreatedAt = now
            };

            _dbContext.UnitTransferRequests.Add(transfer);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (total <= 0)
            {
                // Downgrade / 0 VND switch: execute unit transfer immediately without Manager approval
                var oldUnit = await _dbContext.StorageUnits
                    .FirstOrDefaultAsync(u => u.Id == activeAllocation.StorageUnitId, cancellationToken);
                if (oldUnit != null)
                {
                    oldUnit.PhysicalStatus = StorageUnitStatusConstants.Available;
                    oldUnit.UpdatedAt = now;
                }

                activeAllocation.Status = AllocationStatusConstants.Released;
                activeAllocation.EndedAt = now;
                await _dbContext.SaveChangesAsync(cancellationToken);

                targetUnit.PhysicalStatus = StorageUnitStatusConstants.Occupied;
                targetUnit.UpdatedAt = now;

                _dbContext.UnitAllocations.Add(new UnitAllocation
                {
                    AgreementId = agreement.Id,
                    StorageUnitId = targetUnit.Id,
                    AllocationKind = AllocationKindConstants.Rental,
                    AllocationStartDate = today,
                    AllocationEndDate = agreement.EndDate,
                    Status = AllocationStatusConstants.Active,
                    CreatedAt = now
                });

                agreement.FacilityId = targetUnit.FacilityId;
                agreement.MonthlyRateSnapshot = targetMonthlyRate;
                agreement.UpdatedAt = now;

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                // Upgrade: hold target unit until SePay webhook confirms payment
                targetUnit.PhysicalStatus = StorageUnitStatusConstants.Reserved;
                targetUnit.UpdatedAt = now;

                var invoice = new Invoice
                {
                    InvoiceNo = $"{TransferConstants.InvoicePrefix}{now:yyyyMMdd}-{transfer.Id:D6}",
                    CustomerId = agreement.CustomerId,
                    AgreementId = agreement.Id,
                    BillingPeriod = $"{TransferConstants.BillingPeriodPrefix}{transfer.Id}",
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

                transfer.TargetInvoiceId = invoice.Id;

                _dbContext.InvoiceLines.Add(new InvoiceLine { InvoiceId = invoice.Id, LineType = InvoiceLineTypeConstants.Rent, Description = $"Transfer price difference #{transfer.Id}", Quantity = 1, UnitPrice = transfer.DiffRentAmount, LineAmount = transfer.DiffRentAmount, Metadata = "{}" });
                _dbContext.InvoiceLines.Add(new InvoiceLine { InvoiceId = invoice.Id, LineType = InvoiceLineTypeConstants.Deposit, Description = $"Transfer deposit difference #{transfer.Id}", Quantity = 1, UnitPrice = transfer.DiffDepositAmount, LineAmount = transfer.DiffDepositAmount, Metadata = "{}" });
                if (cleaningFee > 0)
                {
                    _dbContext.InvoiceLines.Add(new InvoiceLine { InvoiceId = invoice.Id, LineType = InvoiceLineTypeConstants.Fee, Description = $"Phí chuyển đổi / vệ sinh kho liên cơ sở #{transfer.Id}", Quantity = 1, UnitPrice = cleaningFee, LineAmount = cleaningFee, Metadata = "{}" });
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                invoiceId = invoice.Id;
                paymentContent = $"{_paymentSettings.SePay.InvoicePrefix}{invoice.Id}";
                var cfg = _paymentSettings.VietQr;
                paymentQrUrl = VietQrUrlBuilder.Build(cfg, total, paymentContent);
            }

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
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

            CleaningFee: transfer.CleaningFee,

            TotalPaidAmount: transfer.TotalPaidAmount,

            RescheduleCount: transfer.RescheduleCount,

            InvoiceId: invoiceId,

            PaymentQrUrl: paymentQrUrl,

            PaymentContent: paymentContent,

            Reason: transfer.Reason,

            Status: transfer.Status,

            CreatedAt: transfer.CreatedAt,
            PaymentBankCode: _paymentSettings.VietQr.BankCode,
            PaymentAccountNo: _paymentSettings.VietQr.AccountNo,
            PaymentAccountName: _paymentSettings.VietQr.AccountName,
            AmountDue: transfer.TotalPaidAmount,
            PaidAmount: 0,
            HoldUntil: transfer.CreatedAt.AddMinutes(_rentalSettings.TransferUnpaidHoldMinutes)

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

        if (request.MoveOutDate != transfer.MoveOutDate && now > new DateTimeOffset(transfer.MoveOutDate.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).AddHours(-_rentalSettings.TransferRescheduleCutoffHours))

            throw AppException.FromError(RentalErrors.TransferRescheduleDeadlinePassed);

        if (request.MoveInDate != transfer.MoveInDate && now > new DateTimeOffset(transfer.MoveInDate.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).AddHours(-_rentalSettings.TransferRescheduleCutoffHours))

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

            transfer.MoveOutDate, transfer.MoveInDate, transfer.DiffRentAmount, transfer.DiffDepositAmount, transfer.CleaningFee,

            transfer.TotalPaidAmount, transfer.RescheduleCount, null, null, null, transfer.Reason, transfer.Status, transfer.CreatedAt);

    }



    public async Task<bool> CancelTransferRequestAsync(
        long customerId,
        long transferRequestId,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _dbContext.UnitTransferRequests
            .Include(t => t.Agreement)
            .Include(t => t.ToUnit)
            .FirstOrDefaultAsync(t => t.Id == transferRequestId, cancellationToken);

        if (transfer == null)
            throw AppException.FromError(RentalErrors.TransferRequestNotFound);

        var isOwner = transfer.RequestedBy == customerId
                   || (transfer.Agreement != null && transfer.Agreement.CustomerId == customerId);
        if (!isOwner)
            throw AppException.FromError(RentalErrors.TransferRequestNotFound);

        if (transfer.Status == TransferRequestStatusConstants.Cancelled)
            return true;

        if (transfer.Status != TransferRequestStatusConstants.PendingPayment)
            throw AppException.FromError(RentalErrors.CannotCancelActiveTransfer);

        var now = DateTimeOffset.UtcNow;
        transfer.Status = TransferRequestStatusConstants.Cancelled;

        if (transfer.ToUnit != null && transfer.ToUnit.PhysicalStatus == StorageUnitStatusConstants.Reserved)
        {
            transfer.ToUnit.PhysicalStatus = StorageUnitStatusConstants.Available;
            transfer.ToUnit.UpdatedAt = now;
        }

        var billingPeriod = $"{TransferConstants.BillingPeriodPrefix}{transfer.Id}";
        var invoices = await _dbContext.Invoices
            .Where(i => i.BillingPeriod == billingPeriod && i.Status != InvoiceStatusConstants.Paid)
            .ToListAsync(cancellationToken);

        foreach (var inv in invoices)
        {
            inv.Status = InvoiceStatusConstants.Voided;
            inv.VoidedAt = now;
            inv.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CancelRenewalAsync(
        long customerId,
        long renewalId,
        CancellationToken cancellationToken = default)
    {
        var renewal = await _dbContext.RentalRenewals
            .Include(r => r.Agreement)
            .FirstOrDefaultAsync(r => r.Id == renewalId, cancellationToken);

        if (renewal == null)
            throw AppException.FromError(RentalErrors.RenewalNotFound);

        var isOwner = renewal.RequestedBy == customerId
                   || (renewal.Agreement != null && renewal.Agreement.CustomerId == customerId);
        if (!isOwner)
            throw AppException.FromError(RentalErrors.RenewalNotFound);

        if (renewal.Status == RenewalStatusConstants.Cancelled)
            return true;

        if (renewal.Status != RenewalStatusConstants.PendingPayment)
            throw AppException.FromError(RentalErrors.CannotCancelActiveRenewal);

        var now = DateTimeOffset.UtcNow;
        renewal.Status = RenewalStatusConstants.Cancelled;

        var renewalBillingPeriod = $"{PaymentConstants.RenewalInvoicePrefix}{renewal.Id}";
        var invoices = await _dbContext.Invoices
            .Where(i => (i.BillingPeriod == renewalBillingPeriod || i.BillingPeriod == $"renewal-{renewal.Id}") && i.Status != InvoiceStatusConstants.Paid)
            .ToListAsync(cancellationToken);

        foreach (var inv in invoices)
        {
            inv.Status = InvoiceStatusConstants.Voided;
            inv.VoidedAt = now;
            inv.UpdatedAt = now;
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CancelInvoiceAsync(
        long customerId,
        long invoiceId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices
            .Include(i => i.Agreement)
            .Include(i => i.Reservation)
                .ThenInclude(r => r!.UnitAllocation)
                    .ThenInclude(ua => ua!.StorageUnit)
            .Include(i => i.Reservation)
                .ThenInclude(r => r!.PromotionRedemptions)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

        if (invoice == null)
            throw AppException.FromError(RentalErrors.InvoiceNotFound);

        var isOwner = invoice.CustomerId == customerId
                   || (invoice.Agreement != null && invoice.Agreement.CustomerId == customerId)
                   || (invoice.Reservation != null && invoice.Reservation.CustomerId == customerId);
        if (!isOwner)
            throw AppException.FromError(RentalErrors.InvoiceNotFound);

        if (invoice.Status == InvoiceStatusConstants.Paid)
            throw AppException.FromError(RentalErrors.CannotCancelPaidInvoice);

        if (invoice.InvoiceNo != null && (invoice.InvoiceNo.StartsWith("INV-PEN", StringComparison.OrdinalIgnoreCase) || invoice.TicketChargeProposalId.HasValue))
            throw AppException.FromError(RentalErrors.CannotCancelPenaltyInvoice);

        var now = DateTimeOffset.UtcNow;

        if (!string.IsNullOrEmpty(invoice.BillingPeriod) && invoice.BillingPeriod.StartsWith(TransferConstants.BillingPeriodPrefix))
        {
            var trfIdStr = invoice.BillingPeriod.Substring(TransferConstants.BillingPeriodPrefix.Length);
            if (long.TryParse(trfIdStr, out var trfId))
            {
                var transfer = await _dbContext.UnitTransferRequests
                    .Include(t => t.ToUnit)
                    .FirstOrDefaultAsync(t => t.Id == trfId, cancellationToken);
                if (transfer != null && transfer.Status == TransferRequestStatusConstants.PendingPayment)
                {
                    transfer.Status = TransferRequestStatusConstants.Cancelled;
                    if (transfer.ToUnit != null && transfer.ToUnit.PhysicalStatus == StorageUnitStatusConstants.Reserved)
                    {
                        transfer.ToUnit.PhysicalStatus = StorageUnitStatusConstants.Available;
                        transfer.ToUnit.UpdatedAt = now;
                    }
                }
            }
        }

        // Auto-cancel reservation and immediately release storage unit back to inventory
        if (invoice.Reservation != null &&
            (invoice.Reservation.Status == ReservationStatusConstants.Pending ||
             invoice.Reservation.Status == ReservationStatusConstants.AwaitingDeposit))
        {
            var res = invoice.Reservation;
            res.Status = ReservationStatusConstants.Cancelled;
            res.CancelledAt = now;
            res.CancellationReason = "Cancelled via invoice voiding";
            res.UpdatedAt = now;

            var allocations = await _dbContext.UnitAllocations
                .Include(ua => ua.StorageUnit)
                .Where(ua => ua.ReservationId == res.Id && ua.Status != AllocationStatusConstants.Cancelled)
                .ToListAsync(cancellationToken);

            if (res.UnitAllocation != null && !allocations.Any(a => a.Id == res.UnitAllocation.Id))
            {
                allocations.Add(res.UnitAllocation);
            }

            foreach (var alloc in allocations)
            {
                alloc.Status = AllocationStatusConstants.Cancelled;
                alloc.EndedAt = now;
                if (alloc.StorageUnit != null && alloc.StorageUnit.PhysicalStatus == StorageUnitStatusConstants.Reserved)
                {
                    alloc.StorageUnit.PhysicalStatus = StorageUnitStatusConstants.Available;
                    alloc.StorageUnit.UpdatedAt = now;
                }
            }

            if (res.PromotionRedemptions != null)
            {
                foreach (var pr in res.PromotionRedemptions.Where(p => p.Status == PromotionRedemptionStatusConstants.Reserved))
                {
                    pr.Status = PromotionRedemptionStatusConstants.Released;
                }
            }
        }

        invoice.Status = InvoiceStatusConstants.Voided;
        invoice.VoidedAt = now;
        invoice.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CustomerRefundBankInfoDto> SubmitRefundBankInfoAsync(

        long customerId,

        long refundId,

        CustomerSubmitRefundBankInfoRequest request,

        CancellationToken cancellationToken = default)

    {

        var refund = await _dbContext.Refunds

            .Include(r => r.Agreement)

                .ThenInclude(a => a!.Facility)

            .FirstOrDefaultAsync(r => r.Id == refundId, cancellationToken);



        if (refund == null)

            throw AppException.FromError(RentalErrors.AgreementNotFound);



        var isOwner = (refund.Agreement != null && refund.Agreement.CustomerId == customerId)
                   || (refund.Agreement == null && refund.RequestedBy.HasValue && refund.RequestedBy.Value == customerId);
        if (!isOwner)
            throw AppException.FromError(RentalErrors.AgreementNotFound);



        if (refund.Amount <= 0m)

            throw AppException.FromError(ManagerErrors.RefundAmountInvalid);



        if (refund.Status == RefundStatusConstants.Completed)

            throw AppException.FromError(ManagerErrors.RefundAlreadyCompleted);



        var now = DateTimeOffset.UtcNow;

        var bankData = new

        {

            bank = request.BankName.Trim(),

            acc = request.BankAccountNumber.Trim(),

            holder = request.BankAccountHolder.Trim(),

            note = request.Note?.Trim()

        };

        var json = JsonSerializer.Serialize(bankData, BankJsonOptions);



        refund.Provider = RefundPayoutMethodConstants.BankTransfer;

        refund.Status = RefundStatusConstants.PendingManagerPayout;

        string refundReason = json.Length <= 250 ? json : $"BANK:{request.BankName.Trim()}|ACC:{request.BankAccountNumber.Trim()}|NAME:{request.BankAccountHolder.Trim()}";
        if (refundReason.Length > 250)
        {
            refundReason = refundReason.Substring(0, 250);
        }
        refund.Reason = refundReason;

        refund.UpdatedAt = now;



        if (refund.Agreement != null)

        {

            var facilityId = refund.Agreement.FacilityId;

            var managerUsers = await _dbContext.StaffFacilityAssignments

                .Where(sfa => sfa.FacilityId == facilityId && (sfa.AssignmentRole == "manager" || sfa.AssignmentRole == "facility_manager") && (sfa.EndsAt == null || sfa.EndsAt > now))

                .Select(sfa => sfa.Employee.UserId)

                .Distinct()

                .ToListAsync(cancellationToken);



            foreach (var mgrId in managerUsers)

            {

                _dbContext.Notifications.Add(new Notification

                {

                    UserId = mgrId,

                    Channel = "in_app",

                    TemplateCode = "REFUND_PENDING_MANAGER_PAYOUT",

                    Subject = $"[Hoàn cọc #{refund.Id}] Khách hàng đã gửi thông tin chuyển khoản",
                    DeduplicationKey = $"refund-pending-payout-{refund.Id}-{mgrId}",

                    Payload = System.Text.Json.JsonSerializer.Serialize(new

                    {

                        refundId = refund.Id,

                        agreementId = refund.AgreementId,

                        agreementNo = refund.Agreement.AgreementNo,

                        amount = refund.Amount,

                        bankName = request.BankName,

                        accountNumber = request.BankAccountNumber,

                        accountHolder = request.BankAccountHolder,

                        message = "Khách hàng đã cung cấp thông tin ngân hàng. Vui lòng Quản lý cơ sở chuyển khoản và xác nhận trên hệ thống."

                    }),

                    Status = "pending",

                    ScheduledAt = now,

                    CreatedAt = now

                });

            }

        }



        await _dbContext.SaveChangesAsync(cancellationToken);



        return new CustomerRefundBankInfoDto

        {

            RefundId = refund.Id,

            AgreementId = refund.AgreementId,

            Amount = refund.Amount,

            Status = refund.Status,

            PayoutMethod = RefundPayoutMethodConstants.BankTransfer,

            BankName = request.BankName,

            BankAccountNumber = request.BankAccountNumber,

            BankAccountHolder = request.BankAccountHolder,

            SubmittedAt = now,

            Message = "Thông tin tài khoản ngân hàng đã được ghi nhận. Quản lý cơ sở sẽ tiến hành chuyển khoản hoàn cọc cho bạn."

        };

    }

    public async Task<IReadOnlyList<CustomerPendingRefundDto>> GetPendingBankInfoRefundsAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        // Refund still "pending" = manager has not received bank info yet.
        var refunds = await _dbContext.Refunds
            .AsNoTracking()
            .Include(r => r.Agreement)
                .ThenInclude(a => a!.Facility)
            .Include(r => r.Agreement)
                .ThenInclude(a => a!.UnitAllocations)
                    .ThenInclude(ua => ua.StorageUnit)
            .Where(r => r.Status == RefundStatusConstants.Pending
                        && r.Amount > 0m
                        && ((r.Agreement != null && r.Agreement.CustomerId == customerId)
                            || (r.Agreement == null && r.RequestedBy == customerId)))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return refunds.Select(r => new CustomerPendingRefundDto
        {
            RefundId = r.Id,
            AgreementId = r.AgreementId,
            AgreementNo = r.Agreement?.AgreementNo,
            UnitCode = r.Agreement?.UnitAllocations
                .OrderByDescending(ua => ua.Id)
                .Select(ua => ua.StorageUnit.UnitCode)
                .FirstOrDefault(),
            FacilityName = r.Agreement?.Facility?.Name,
            Amount = r.Amount,
            Status = r.Status,
            CreatedAt = r.CreatedAt
        }).ToList();
    }

}