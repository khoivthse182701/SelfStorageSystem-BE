using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Staff;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;

namespace SelfStorageSystem.Infrastructure.Services;

public partial class FacilityStaffService
{
    public async Task<IReadOnlyList<StaffReservationLookupDto>> LookupReservationsV1Async(
        long employeeUserId,
        long? facilityId = null,
        string? query = null,
        string? status = "confirmed",
        CancellationToken cancellationToken = default)
    {
        var targetFacilityId = await EnsureStrictFacilityAccessAsync(employeeUserId, facilityId, cancellationToken);
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? ReservationStatusConstants.Confirmed : status.Trim().ToLowerInvariant();

        var q = _dbContext.Reservations
            .AsNoTracking()
            .Include(r => r.Customer).ThenInclude(c => c.User)
            .Include(r => r.Facility)
            .Include(r => r.UnitType)
            .Include(r => r.UnitAllocation).ThenInclude(a => a!.StorageUnit)
            .Where(r => r.FacilityId == targetFacilityId);

        if (normalizedStatus != "all")
            q = q.Where(r => r.Status.ToLower() == normalizedStatus);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLowerInvariant();
            var hasNumericId = long.TryParse(query.Trim(), out var parsedId);
            q = q.Where(r =>
                r.ReservationCode.ToLower().Contains(term) ||
                r.Customer.FullName.ToLower().Contains(term) ||
                (r.Customer.User != null && r.Customer.User.PhoneNumber != null && r.Customer.User.PhoneNumber.ToLower().Contains(term)) ||
                (hasNumericId && r.Id == parsedId));
        }

        var list = await q
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var availableUnitsAtFacility = await _dbContext.StorageUnits
            .AsNoTracking()
            .Include(u => u.UnitType)
            .Where(u => u.FacilityId == targetFacilityId && u.IsListed && u.PhysicalStatus == StorageUnitStatusConstants.Available)
            .OrderBy(u => u.UnitCode)
            .ToListAsync(cancellationToken);

        return list.Select(r =>
        {
            var backupUnits = availableUnitsAtFacility
                .Where(u => u.UnitTypeId == r.UnitTypeId && u.Id != r.UnitAllocation?.StorageUnitId)
                .Select(u => new BackupStorageUnitDto(
                    u.Id,
                    u.UnitCode,
                    u.UnitTypeId,
                    u.UnitType.Name,
                    u.UnitType.WidthM,
                    u.UnitType.LengthM,
                    u.UnitType.HeightM,
                    u.UnitType.AreaM2,
                    u.FloorLabel,
                    u.ZoneLabel,
                    u.PhysicalStatus))
                .ToList();

            return new StaffReservationLookupDto(
                r.Id,
                r.ReservationCode,
                r.CustomerId,
                r.Customer.FullName,
                r.Customer.User?.PhoneNumber,
                r.FacilityId,
                r.Facility.Code,
                r.UnitTypeId,
                r.UnitType.Name,
                r.UnitAllocation?.StorageUnitId,
                r.UnitAllocation?.StorageUnit?.UnitCode,
                r.StartDate,
                r.EndDate,
                r.Status,
                r.QuotedTotal,
                r.DepositSnapshot,
                r.CreatedAt,
                MaskIdentityNumber(r.Customer.IdentityNumber),
                backupUnits);
        }).ToList();
    }

    public async Task<VerifyCheckInQrResponse> VerifyCheckInQrAsync(
        long employeeUserId,
        VerifyCheckInQrRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureStrictFacilityAccessAsync(employeeUserId, request.FacilityId, cancellationToken);

        if (_qrTokenService == null ||
            !_qrTokenService.TryVerifyToken(request.QrToken, "checkin", out var tokenPayload) ||
            tokenPayload?.RsvId is not > 0)
        {
            throw AppException.FromError(StaffErrors.QrCodeInvalid);
        }

        if (tokenPayload.FacId != request.FacilityId)
            throw AppException.FromError(StaffErrors.FacilityMismatch);

        var reservation = await _dbContext.Reservations
            .AsNoTracking()
            .Include(r => r.Customer).ThenInclude(c => c.User)
            .Include(r => r.Facility)
            .Include(r => r.UnitType)
            .Include(r => r.RentalAgreement)
            .Include(r => r.UnitAllocation).ThenInclude(a => a!.StorageUnit)
            .FirstOrDefaultAsync(r => r.Id == tokenPayload.RsvId.Value, cancellationToken)
            ?? throw AppException.FromError(StaffErrors.ReservationNotFound);

        if (reservation.FacilityId != request.FacilityId)
            throw AppException.FromError(StaffErrors.FacilityMismatch);

        if (tokenPayload.CustId != reservation.CustomerId)
            throw AppException.FromError(StaffErrors.QrCodeInvalid);

        if (!string.Equals(reservation.Status, ReservationStatusConstants.Confirmed, StringComparison.OrdinalIgnoreCase))
            throw AppException.FromError(StaffErrors.ReservationNotConfirmed);

        var registeredCccd = reservation.Customer.IdentityNumber?.Trim();
        var identityVerified = false;
        if (!string.IsNullOrWhiteSpace(request.CustomerIdentityNumber))
        {
            var enteredCccd = request.CustomerIdentityNumber.Trim();
            if (!string.IsNullOrWhiteSpace(registeredCccd) &&
                !string.Equals(enteredCccd, registeredCccd, StringComparison.OrdinalIgnoreCase))
            {
                throw AppException.FromError(StaffErrors.IdentityNumberMismatch);
            }
            identityVerified = true;
        }

        var unit = reservation.UnitAllocation?.StorageUnit;
        var agreement = reservation.RentalAgreement;

        var backupUnits = await GetBackupUnitsForReservationAsync(reservation, cancellationToken);

        return new VerifyCheckInQrResponse(
            ActionType: StaffActionConstants.CheckIn,
            Valid: true,
            Customer: new StaffCustomerSummaryDto(
                reservation.CustomerId,
                reservation.Customer.FullName,
                reservation.Customer.User?.PhoneNumber,
                reservation.Customer.User?.Email,
                MaskIdentityNumber(registeredCccd),
                registeredCccd),
            Reservation: new StaffReservationSummaryDto(
                reservation.Id,
                reservation.ReservationCode,
                reservation.StartDate,
                reservation.EndDate,
                reservation.Status),
            Agreement: agreement == null
                ? null
                : new StaffAgreementSummaryDto(
                    agreement.Id,
                    agreement.AgreementNo,
                    agreement.FacilityId,
                    reservation.Facility.Name,
                    unit?.Id ?? 0,
                    unit?.UnitCode ?? "Unassigned",
                    agreement.StartDate,
                    agreement.EndDate,
                    agreement.Status),
            Unit: unit == null
                ? null
                : new StaffUnitSummaryDto(
                    unit.Id,
                    unit.UnitCode,
                    unit.FacilityId,
                    reservation.Facility.Name,
                    reservation.UnitType.Name,
                    unit.PhysicalStatus),
            AllowedActions: ["reassign_unit", "confirm_checkin", "report_issue"],
            IdentityVerified: identityVerified,
            AvailableBackupUnits: backupUnits);
    }

    public async Task<AssignReservationUnitResponse> AssignReservationUnitV1Async(
        long employeeUserId,
        long reservationId,
        AssignReservationUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        IDbContextTransaction? transaction = _dbContext.Database.IsRelational() && _dbContext.Database.CurrentTransaction == null
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var reservation = await _dbContext.Reservations
                .Include(r => r.UnitAllocation).ThenInclude(a => a!.StorageUnit)
                .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken)
                ?? throw AppException.FromError(StaffErrors.ReservationNotFound);

            await EnsureStrictFacilityAccessAsync(employeeUserId, reservation.FacilityId, cancellationToken);

            if (!string.Equals(reservation.Status, ReservationStatusConstants.Confirmed, StringComparison.OrdinalIgnoreCase))
                throw AppException.FromError(StaffErrors.ReservationNotConfirmed);

            StorageUnit? newUnit;
            if (_dbContext.Database.IsSqlServer())
            {
                newUnit = await _dbContext.StorageUnits
                    .FromSqlInterpolated($"SELECT * FROM [core].[storage_units] WITH (UPDLOCK, ROWLOCK) WHERE id = {request.StorageUnitId}")
                    .FirstOrDefaultAsync(cancellationToken);
            }
            else
            {
                newUnit = await _dbContext.StorageUnits
                    .FirstOrDefaultAsync(u => u.Id == request.StorageUnitId, cancellationToken);
            }

            if (newUnit == null)
                throw AppException.FromError(StaffErrors.UnitNotFound);

            if (newUnit.FacilityId != reservation.FacilityId)
                throw AppException.FromError(StaffErrors.FacilityMismatch);

            if (newUnit.UnitTypeId != reservation.UnitTypeId)
                throw AppException.FromError(StaffErrors.UnitTypeMismatch);

            await EnsureUnitAvailableForReservationWindowAsync(newUnit, reservation, employeeUserId, cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var oldUnit = reservation.UnitAllocation?.StorageUnit;
            long? oldUnitId = oldUnit?.Id;

            if (oldUnit != null && oldUnit.Id != newUnit.Id)
            {
                var hasOtherActiveAlloc = await _dbContext.UnitAllocations.AnyAsync(a =>
                    a.StorageUnitId == oldUnit.Id &&
                    a.Id != reservation.UnitAllocation!.Id &&
                    a.Status == AllocationStatusConstants.Active, cancellationToken);

                if (!hasOtherActiveAlloc && string.Equals(oldUnit.PhysicalStatus, StorageUnitStatusConstants.Reserved, StringComparison.OrdinalIgnoreCase))
                {
                    oldUnit.PhysicalStatus = StorageUnitStatusConstants.Available;
                    oldUnit.UpdatedAt = now;
                    _dbContext.UnitStatusHistories.Add(new UnitStatusHistory
                    {
                        StorageUnitId = oldUnit.Id,
                        OldStatus = StorageUnitStatusConstants.Reserved,
                        NewStatus = StorageUnitStatusConstants.Available,
                        Reason = request.Reason ?? $"Swapped away during reservation {reservation.ReservationCode} assignment.",
                        ChangedBy = employeeUserId,
                        ChangedAt = now
                    });
                }
            }

            if (reservation.UnitAllocation != null)
            {
                reservation.UnitAllocation.StorageUnitId = newUnit.Id;
            }
            else
            {
                _dbContext.UnitAllocations.Add(new UnitAllocation
                {
                    StorageUnitId = newUnit.Id,
                    ReservationId = reservation.Id,
                    AllocationKind = AllocationKindConstants.Reservation,
                    AllocationStartDate = reservation.StartDate,
                    AllocationEndDate = reservation.EndDate,
                    Status = AllocationStatusConstants.Active,
                    CreatedAt = now
                });
            }

            if (!string.Equals(newUnit.PhysicalStatus, StorageUnitStatusConstants.Reserved, StringComparison.OrdinalIgnoreCase))
            {
                var prev = newUnit.PhysicalStatus;
                newUnit.PhysicalStatus = StorageUnitStatusConstants.Reserved;
                newUnit.UpdatedAt = now;
                _dbContext.UnitStatusHistories.Add(new UnitStatusHistory
                {
                    StorageUnitId = newUnit.Id,
                    OldStatus = prev,
                    NewStatus = StorageUnitStatusConstants.Reserved,
                    Reason = request.Reason ?? $"Assigned to reservation {reservation.ReservationCode}.",
                    ChangedBy = employeeUserId,
                    ChangedAt = now
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (transaction != null)
                await transaction.CommitAsync(cancellationToken);

            if (_realtimeNotifier != null)
            {
                await _realtimeNotifier.SendUnitStatusChangeAsync(reservation.FacilityId, new
                {
                    unitId = newUnit.Id,
                    unitCode = newUnit.UnitCode,
                    physicalStatus = newUnit.PhysicalStatus,
                    reservationId = reservation.Id
                });
            }

            return new AssignReservationUnitResponse(reservation.Id, oldUnitId, newUnit.Id, newUnit.UnitCode, newUnit.PhysicalStatus);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            throw AppException.FromError(StaffErrors.UnitNotAvailable);
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    public async Task<ConfirmCheckInResponse> ConfirmCheckInAsync(
        long employeeUserId,
        ConfirmCheckInRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.IdentityVerification.FullNameMatched)
            throw AppException.FromError(StaffErrors.IdentityVerificationFailed);

        var now = DateTimeOffset.UtcNow;

        IDbContextTransaction? transaction = _dbContext.Database.IsRelational() && _dbContext.Database.CurrentTransaction == null
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var reservation = await _dbContext.Reservations
                .Include(r => r.Customer)
                .Include(r => r.RentalAgreement)
                .Include(r => r.UnitAllocation).ThenInclude(ua => ua!.StorageUnit)
                .FirstOrDefaultAsync(r => r.Id == request.ReservationId, cancellationToken)
                ?? throw AppException.FromError(StaffErrors.ReservationNotFound);

            await EnsureStrictFacilityAccessAsync(employeeUserId, reservation.FacilityId, cancellationToken);
            await GetEmployeeProfileAsync(employeeUserId, cancellationToken);

            if (!string.Equals(reservation.Status, ReservationStatusConstants.Confirmed, StringComparison.OrdinalIgnoreCase))
                throw AppException.FromError(StaffErrors.ReservationNotConfirmed);

            StorageUnit? unit;
            var holdAlloc = reservation.UnitAllocation;

            if (request.OverrideUnitId.HasValue && request.OverrideUnitId.Value != holdAlloc?.StorageUnitId)
            {
                unit = await _dbContext.StorageUnits
                    .FirstOrDefaultAsync(u => u.Id == request.OverrideUnitId.Value, cancellationToken)
                    ?? throw AppException.FromError(StaffErrors.UnitNotFound);

                if (unit.FacilityId != reservation.FacilityId)
                    throw AppException.FromError(StaffErrors.FacilityMismatch);

                if (unit.UnitTypeId != reservation.UnitTypeId)
                    throw AppException.FromError(StaffErrors.UnitTypeMismatch);

                await EnsureUnitAvailableForReservationWindowAsync(unit, reservation, employeeUserId, cancellationToken);

                if (holdAlloc?.StorageUnit != null &&
                    string.Equals(holdAlloc.StorageUnit.PhysicalStatus, StorageUnitStatusConstants.Reserved, StringComparison.OrdinalIgnoreCase))
                {
                    var hasOtherActiveAlloc = await _dbContext.UnitAllocations.AnyAsync(a =>
                        a.StorageUnitId == holdAlloc.StorageUnit.Id &&
                        a.Id != holdAlloc.Id &&
                        a.Status == AllocationStatusConstants.Active, cancellationToken);

                    if (!hasOtherActiveAlloc)
                    {
                        holdAlloc.StorageUnit.PhysicalStatus = StorageUnitStatusConstants.Available;
                        holdAlloc.StorageUnit.UpdatedAt = now;
                        _dbContext.UnitStatusHistories.Add(new UnitStatusHistory
                        {
                            StorageUnitId = holdAlloc.StorageUnit.Id,
                            OldStatus = StorageUnitStatusConstants.Reserved,
                            NewStatus = StorageUnitStatusConstants.Available,
                            Reason = Truncate($"Released during check-in override on reservation {reservation.ReservationCode}.", 255)!,
                            ChangedBy = employeeUserId,
                            ChangedAt = now
                        });
                    }
                }
            }
            else
            {
                unit = holdAlloc?.StorageUnit;
                if (unit == null)
                    throw AppException.FromError(StaffErrors.UnitNotAvailable);
            }

            var agreement = reservation.RentalAgreement;
            if (agreement != null && await _dbContext.HandoverRecords.AnyAsync(h =>
                    h.AgreementId == agreement.Id && h.HandoverType == HandoverTypeConstants.CheckIn, cancellationToken))
            {
                throw AppException.FromError(StaffErrors.HandoverAlreadyExists);
            }

            // 1. Identity verification record (Dual Verification: QR + CCCD match)
            var enteredDocNumber = request.IdentityVerification.DocumentNumber?.Trim() ?? string.Empty;
            var registeredCccd = reservation.Customer.IdentityNumber?.Trim();
            if (!string.IsNullOrWhiteSpace(registeredCccd) &&
                !string.Equals(enteredDocNumber, registeredCccd, StringComparison.OrdinalIgnoreCase))
            {
                throw AppException.FromError(StaffErrors.IdentityNumberMismatch);
            }

            var docFingerprint = ComputeMaskedDocumentFingerprint(enteredDocNumber);
            var kyc = new IdentityVerification
            {
                ReservationId = reservation.Id,
                VerifiedBy = employeeUserId,
                VerificationMethod = request.IdentityVerification.Method.Trim().ToLowerInvariant(),
                DocumentFingerprint = docFingerprint,
                Result = "verified",
                Notes = Truncate(request.IdentityVerification.Notes, 255),
                VerifiedAt = now
            };
            _dbContext.IdentityVerifications.Add(kyc);

            // 2. Consume hold allocation before inserting active rental allocation
            if (holdAlloc != null && holdAlloc.Status == AllocationStatusConstants.Active)
            {
                holdAlloc.Status = AllocationStatusConstants.Consumed;
                holdAlloc.EndedAt = now;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // 3. Create or activate RentalAgreement
            if (agreement == null)
            {
                var policyId = await _dbContext.PolicyVersions
                    .Where(p => p.PolicyType == "rental_terms")
                    .OrderByDescending(p => p.ValidFrom)
                    .Select(p => p.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (policyId <= 0)
                {
                    policyId = await _dbContext.PolicyVersions
                        .OrderByDescending(p => p.ValidFrom)
                        .Select(p => p.Id)
                        .FirstOrDefaultAsync(cancellationToken);
                }
                if (policyId <= 0) policyId = 1;

                agreement = new RentalAgreement
                {
                    AgreementNo = $"{StaffConstants.AgreementPrefix}{now:yyyyMMdd}-{reservation.Id:D4}",
                    CustomerId = reservation.CustomerId,
                    FacilityId = reservation.FacilityId,
                    ReservationId = reservation.Id,
                    PolicyVersionId = policyId,
                    StartDate = reservation.StartDate,
                    EndDate = reservation.EndDate,
                    MonthlyRateSnapshot = reservation.MonthlyRateSnapshot,
                    DepositSnapshot = reservation.DepositSnapshot,
                    DepositBalance = reservation.DepositSnapshot,
                    Status = RentalAgreementStatusConstants.Draft,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.RentalAgreements.Add(agreement);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            if (string.Equals(agreement.Status, RentalAgreementStatusConstants.Draft, StringComparison.OrdinalIgnoreCase))
            {
                agreement.Status = RentalAgreementStatusConstants.Scheduled;
                agreement.UpdatedAt = now;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            agreement.Status = RentalAgreementStatusConstants.Active;
            agreement.SignedAt ??= now;
            agreement.CheckedInAt = now;
            agreement.UpdatedAt = now;

            // 4. Create active rental UnitAllocation & mark StorageUnit occupied
            var rentalAlloc = new UnitAllocation
            {
                AgreementId = agreement.Id,
                StorageUnitId = unit.Id,
                AllocationKind = AllocationKindConstants.Rental,
                AllocationStartDate = agreement.StartDate,
                AllocationEndDate = agreement.EndDate,
                Status = AllocationStatusConstants.Active,
                CreatedAt = now
            };
            _dbContext.UnitAllocations.Add(rentalAlloc);

            var oldUnitStatus = unit.PhysicalStatus;
            unit.PhysicalStatus = StorageUnitStatusConstants.Occupied;
            unit.UpdatedAt = now;
            _dbContext.UnitStatusHistories.Add(new UnitStatusHistory
            {
                StorageUnitId = unit.Id,
                OldStatus = oldUnitStatus,
                NewStatus = StorageUnitStatusConstants.Occupied,
                Reason = $"Checked in for agreement {agreement.AgreementNo}.",
                ChangedBy = employeeUserId,
                ChangedAt = now
            });

            // 5. Convert reservation
            reservation.Status = ReservationStatusConstants.Converted;
            reservation.UpdatedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // 6. Check-in inspection + items
            var inspection = new Inspection
            {
                FacilityId = reservation.FacilityId,
                StorageUnitId = unit.Id,
                ReservationId = reservation.Id,
                AgreementId = agreement.Id,
                InspectedBy = employeeUserId,
                InspectionType = "check_in",
                Status = InspectionStatusConstants.Completed,
                OverallCondition = request.Inspection.OverallCondition.Trim().ToLowerInvariant(),
                Summary = Truncate(request.Inspection.Summary ?? request.Notes, 255) ?? "Check-in unit inspection completed.",
                InspectedAt = now,
                CreatedAt = now
            };
            _dbContext.Inspections.Add(inspection);
            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var item in request.Inspection.Items)
            {
                _dbContext.InspectionItems.Add(new InspectionItem
                {
                    InspectionId = inspection.Id,
                    ItemName = Truncate(item.ItemName.Trim(), 150) ?? item.ItemName.Trim(),
                    Condition = item.Condition.Trim().ToLowerInvariant(),
                    Notes = Truncate(item.Notes, 255),
                    PhotoUrl = Truncate(item.PhotoUrl, 255),
                    ChargeAmount = 0m
                });
            }

            // 7. HandoverRecord
            var handover = new HandoverRecord
            {
                AgreementId = agreement.Id,
                UnitAllocationId = rentalAlloc.Id,
                InspectionId = inspection.Id,
                HandledBy = employeeUserId,
                HandoverType = HandoverTypeConstants.CheckIn,
                CustomerSignatureRef = Truncate(request.CustomerSignatureRef, 255),
                StaffSignatureRef = Truncate(request.StaffSignatureRef, 255),
                CustomerSignedAt = now,
                StaffSignedAt = now,
                Notes = Truncate(request.Notes, 255) ?? "Counter check-in handover completed.",
                CreatedAt = now
            };
            _dbContext.HandoverRecords.Add(handover);

            // 8. One-time 6-digit AccessCredential PIN
            var stalePins = await _dbContext.AccessCredentials
                .Where(c => c.AgreementId == agreement.Id && c.CredentialType == CredentialTypeConstants.Pin && c.Status == CredentialStatusConstants.Active)
                .ToListAsync(cancellationToken);
            foreach (var stale in stalePins)
            {
                stale.Status = CredentialStatusConstants.Revoked;
                stale.RevokedAt = now;
            }

            var issuedPin = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
            _dbContext.AccessCredentials.Add(new AccessCredential
            {
                AgreementId = agreement.Id,
                CredentialType = CredentialTypeConstants.Pin,
                SecretDigest = BCrypt.Net.BCrypt.HashPassword(issuedPin),
                DisplayHint = PinProtectionHelper.Encrypt(issuedPin),
                IssuedAt = now,
                ExpiresAt = agreement.EndDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc),
                Status = CredentialStatusConstants.Active,
                CreatedAt = now
            });

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (transaction != null)
                await transaction.CommitAsync(cancellationToken);

            if (_realtimeNotifier != null)
            {
                await _realtimeNotifier.SendUnitStatusChangeAsync(reservation.FacilityId, new
                {
                    unitId = unit.Id,
                    unitCode = unit.UnitCode,
                    physicalStatus = unit.PhysicalStatus,
                    agreementId = agreement.Id
                });
                await _realtimeNotifier.SendNotificationToUserAsync(
                    reservation.CustomerId,
                    "Check-in completed",
                    $"Unit {unit.UnitCode} is now active under agreement {agreement.AgreementNo}.",
                    new { agreementId = agreement.Id, unitId = unit.Id, unitCode = unit.UnitCode });
            }

            return new ConfirmCheckInResponse(
                handover.Id,
                inspection.Id,
                kyc.Id,
                agreement.Id,
                agreement.AgreementNo,
                agreement.Status,
                reservation.Status,
                unit.Id,
                unit.UnitCode,
                unit.PhysicalStatus,
                issuedPin,
                now);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            throw AppException.FromError(StaffErrors.UnitNotAvailable);
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    private async Task<long> EnsureStrictFacilityAccessAsync(long employeeUserId, long? facilityId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        if (facilityId.HasValue)
        {
            var isAssigned = await _dbContext.StaffFacilityAssignments
                .AsNoTracking()
                .AnyAsync(a => a.EmployeeId == employeeUserId
                            && a.FacilityId == facilityId.Value
                            && a.StartsAt <= now
                            && (a.EndsAt == null || a.EndsAt > now), ct);

            if (!isAssigned)
                throw AppException.FromError(StaffErrors.StaffNotAssignedToFacility);

            return facilityId.Value;
        }

        var assignedFacilityId = await _dbContext.StaffFacilityAssignments
            .AsNoTracking()
            .Where(a => a.EmployeeId == employeeUserId
                     && a.StartsAt <= now
                     && (a.EndsAt == null || a.EndsAt > now))
            .OrderBy(a => a.Id)
            .Select(a => (long?)a.FacilityId)
            .FirstOrDefaultAsync(ct);

        return assignedFacilityId ?? throw AppException.FromError(StaffErrors.StaffNotAssignedToFacility);
    }

    private async Task EnsureUnitAvailableForReservationWindowAsync(
        StorageUnit unit,
        Reservation reservation,
        long employeeUserId,
        CancellationToken ct)
    {
        var allowedStatus =
            string.Equals(unit.PhysicalStatus, StorageUnitStatusConstants.Available, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unit.PhysicalStatus, StorageUnitStatusConstants.Reserved, StringComparison.OrdinalIgnoreCase);

        if (!allowedStatus || (_dbContext.Database.IsSqlServer() && !unit.IsListed))
            throw AppException.FromError(StaffErrors.UnitNotAvailable);

        var currentHoldId = reservation.UnitAllocation?.Id ?? 0;
        var hasOverlap = await _dbContext.UnitAllocations.AnyAsync(a =>
            a.StorageUnitId == unit.Id &&
            a.Id != currentHoldId &&
            a.Status == AllocationStatusConstants.Active &&
            a.AllocationStartDate < reservation.EndDate &&
            a.AllocationEndDate > reservation.StartDate, ct);

        if (hasOverlap)
            throw AppException.FromError(StaffErrors.UnitNotAvailable);
    }

    private async Task<List<BackupStorageUnitDto>> GetBackupUnitsForReservationAsync(
        Reservation reservation,
        CancellationToken ct)
    {
        var currentUnitId = reservation.UnitAllocation?.StorageUnitId ?? 0;
        var candidateUnits = await _dbContext.StorageUnits
            .AsNoTracking()
            .Include(u => u.UnitType)
            .Where(u => u.FacilityId == reservation.FacilityId &&
                        u.UnitTypeId == reservation.UnitTypeId &&
                        u.IsListed &&
                        u.Id != currentUnitId &&
                        u.PhysicalStatus == StorageUnitStatusConstants.Available)
            .OrderBy(u => u.UnitCode)
            .ToListAsync(ct);

        if (candidateUnits.Count == 0)
            return new List<BackupStorageUnitDto>();

        var candidateIds = candidateUnits.Select(u => u.Id).ToList();
        var busyUnitIds = await _dbContext.UnitAllocations
            .AsNoTracking()
            .Where(a => candidateIds.Contains(a.StorageUnitId) &&
                        a.Status == AllocationStatusConstants.Active &&
                        a.AllocationStartDate < reservation.EndDate &&
                        a.AllocationEndDate > reservation.StartDate)
            .Select(a => a.StorageUnitId)
            .Distinct()
            .ToListAsync(ct);

        var busySet = busyUnitIds.ToHashSet();
        return candidateUnits
            .Where(u => !busySet.Contains(u.Id))
            .Select(u => new BackupStorageUnitDto(
                u.Id,
                u.UnitCode,
                u.UnitTypeId,
                u.UnitType.Name,
                u.UnitType.WidthM,
                u.UnitType.LengthM,
                u.UnitType.HeightM,
                u.UnitType.AreaM2,
                u.FloorLabel,
                u.ZoneLabel,
                u.PhysicalStatus))
            .ToList();
    }

    private static string? MaskIdentityNumber(string? identityNumber)
    {
        if (string.IsNullOrWhiteSpace(identityNumber))
            return null;
        var trimmed = identityNumber.Trim();
        if (trimmed.Length <= 4)
            return trimmed;
        return new string('*', trimmed.Length - 4) + trimmed[^4..];
    }

    private static string ComputeMaskedDocumentFingerprint(string documentNumber)
    {
        var trimmed = documentNumber.Trim();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trimmed)))[..16];
        var tail = trimmed.Length >= 4 ? trimmed[^4..] : trimmed;
        return $"DOC-{tail}-{hash}";
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
