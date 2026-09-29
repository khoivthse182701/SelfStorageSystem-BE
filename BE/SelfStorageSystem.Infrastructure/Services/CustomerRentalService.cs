using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
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

    private readonly SelfStorageDbContext _dbContext;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<CustomerRentalService> _logger;

    public CustomerRentalService(
        SelfStorageDbContext dbContext,
        IOptions<JwtSettings> jwtOptions,
        ILogger<CustomerRentalService> logger)
    {
        _dbContext = dbContext;
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

        // Generate Time-based JWT Gate QR Token with short TTL (60 seconds)
        const int qrTtlSeconds = 60;
        var qrExpiresAt = now.AddSeconds(qrTtlSeconds);
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
            QrExpiresInSeconds = qrTtlSeconds,
            QrExpiresAt = qrExpiresAt,
            SuspendedReason = null
        };
    }

    public async Task<bool> ChangePinAsync(long customerId, long agreementId, ChangePinRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Validate PIN format: exactly 6 digits
        if (string.IsNullOrWhiteSpace(request.NewPin) || !Regex.IsMatch(request.NewPin, @"^\d{6}$"))
        {
            throw AppException.FromError(RentalErrors.InvalidPinFormat);
        }

        // 2. Prevent weak PINs: all same digits or simple ascending/descending sequences
        if (request.NewPin.Distinct().Count() == 1 || WeakPins.Contains(request.NewPin))
        {
            throw AppException.FromError(RentalErrors.PinTooSimple);
        }

        // 3. Verify agreement ownership
        var agreement = await _dbContext.RentalAgreements
            .Include(a => a.AccessCredentials)
            .Include(a => a.Invoices)
            .FirstOrDefaultAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (agreement is null)
        {
            throw AppException.FromError(RentalErrors.AgreementNotFound);
        }

        // 4. BR-REN-03: If access is suspended due to debt, reject PIN change
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

        // 5. Verify current PIN if credential exists
        var pinCred = agreement.AccessCredentials.FirstOrDefault(c => c.CredentialType == CredentialTypeConstants.Pin);

        if (pinCred != null && !string.IsNullOrWhiteSpace(pinCred.SecretDigest))
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPin) ||
                !BCrypt.Net.BCrypt.Verify(request.CurrentPin, pinCred.SecretDigest))
            {
                throw AppException.FromError(RentalErrors.IncorrectCurrentPin);
            }
        }

        // 6. Update PIN securely
        if (pinCred is null)
        {
            pinCred = new AccessCredential
            {
                AgreementId = agreement.Id,
                CredentialType = CredentialTypeConstants.Pin,
                SecretDigest = BCrypt.Net.BCrypt.HashPassword(request.NewPin),
                DisplayHint = request.NewPin,
                Status = CredentialStatusConstants.Active,
                IssuedAt = now,
                CreatedAt = now
            };
            _dbContext.AccessCredentials.Add(pinCred);
        }
        else
        {
            pinCred.SecretDigest = BCrypt.Net.BCrypt.HashPassword(request.NewPin);
            pinCred.DisplayHint = request.NewPin;
            pinCred.Status = CredentialStatusConstants.Active;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(RentalLogMessages.PinChanged, customerId, agreementId);
        return true;
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
                    PhotoUrl = item.PhotoUrl,
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

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateSecureNumericPin()
    {
        return Random.Shared.Next(100000, 999999).ToString();
    }
}
