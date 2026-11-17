using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Staff;
using SelfStorageSystem.Domain.Common;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public partial class FacilityStaffService : IFacilityStaffService
{
    // ==========================================
    // 🏢 0. STAFF ASSIGNED FACILITY INFO
    // ==========================================

    public async Task<StaffFacilityInfoDto> GetMyFacilityAsync(long employeeUserId, CancellationToken cancellationToken = default)
    {
        var targetFacilityId = await EnsureStrictFacilityAccessAsync(employeeUserId, null, cancellationToken);
        var facility = await _dbContext.Facilities
            .AsNoTracking()
            .Include(f => f.StorageUnits)
            .FirstOrDefaultAsync(f => f.Id == targetFacilityId, cancellationToken);

        if (facility == null)
            throw AppException.FromError(StaffErrors.FacilityNotFound);

        return new StaffFacilityInfoDto(
            FacilityId: facility.Id,
            FacilityName: facility.Name,
            FacilityCode: facility.Code,
            Address: facility.AddressLine,
            City: facility.City,
            TotalUnits: facility.StorageUnits.Count
        );
    }

    // ==========================================
    // 🏢 NHÓM 1: XEM HIỆN TRẠNG SƠ ĐỒ KHO (Unit Availability)
    // ==========================================

    public async Task<IReadOnlyList<StaffUnitGridDto>> GetUnitsAsync(
        long employeeUserId,
        long? facilityId = null,
        string? status = null,
        long? unitTypeId = null,
        string? sizeCategory = null,
        CancellationToken cancellationToken = default)
    {
        var targetFacilityId = await EnsureStrictFacilityAccessAsync(employeeUserId, facilityId, cancellationToken);

        var query = _dbContext.StorageUnits
            .AsNoTracking()
            .Include(u => u.Facility)
            .Include(u => u.UnitType)
            .Include(u => u.UnitMapPosition)
            .Include(u => u.UnitAllocations.Where(a => a.Status == AllocationStatusConstants.Active))
                .ThenInclude(a => a.Agreement)
                    .ThenInclude(ag => ag!.Customer)
            .Where(u => u.FacilityId == targetFacilityId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToLowerInvariant();
            query = query.Where(u => u.PhysicalStatus.ToLower() == normalizedStatus);
        }

        if (unitTypeId.HasValue)
        {
            query = query.Where(u => u.UnitTypeId == unitTypeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(sizeCategory))
        {
            var cat = sizeCategory.Trim().ToLowerInvariant();
            if (cat == "small")
                query = query.Where(u => u.UnitType.AreaM2.HasValue && u.UnitType.AreaM2.Value <= 5);
            else if (cat == "medium")
                query = query.Where(u => u.UnitType.AreaM2.HasValue && u.UnitType.AreaM2.Value > 5 && u.UnitType.AreaM2.Value <= 15);
            else if (cat == "large")
                query = query.Where(u => u.UnitType.AreaM2.HasValue && u.UnitType.AreaM2.Value > 15);
        }

        var units = await query.OrderBy(u => u.UnitCode).ToListAsync(cancellationToken);

        // Fetch applicable facility rates
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rates = await _dbContext.FacilityRates
            .AsNoTracking()
            .Where(fr => fr.FacilityId == targetFacilityId && fr.ValidFrom <= today && (!fr.ValidTo.HasValue || fr.ValidTo.Value > today))
            .OrderByDescending(fr => fr.ValidFrom)
            .ToListAsync(cancellationToken);

        return units.Select(u =>
        {
            var activeAlloc = u.UnitAllocations.FirstOrDefault();
            var applicableRate = rates.FirstOrDefault(r => r.UnitTypeId == u.UnitTypeId);

            StaffUnitMapPositionDto? mapDto = null;
            if (u.UnitMapPosition != null)
            {
                mapDto = new StaffUnitMapPositionDto(
                    AreaId: u.UnitMapPosition.AreaId,
                    X: u.UnitMapPosition.X,
                    Y: u.UnitMapPosition.Y,
                    Width: u.UnitMapPosition.Width,
                    Height: u.UnitMapPosition.Height,
                    RotationDegrees: u.UnitMapPosition.RotationDegrees
                );
            }

            return new StaffUnitGridDto(
                UnitId: u.Id,
                UnitCode: u.UnitCode,
                FacilityId: u.FacilityId,
                FacilityName: u.Facility.Name,
                UnitTypeId: u.UnitTypeId,
                UnitTypeName: u.UnitType.Name,
                WidthM: u.UnitType.WidthM,
                LengthM: u.UnitType.LengthM,
                HeightM: u.UnitType.HeightM,
                AreaM2: u.UnitType.AreaM2,
                VolumeM3: u.UnitType.VolumeM3,
                PhysicalStatus: u.PhysicalStatus,
                IsListed: u.IsListed,
                FloorLabel: u.FloorLabel,
                ZoneLabel: u.ZoneLabel,
                CurrentRate: applicableRate?.MonthlyRate,
                CurrentAgreementNo: activeAlloc?.Agreement?.AgreementNo,
                CustomerName: activeAlloc?.Agreement?.Customer?.FullName,
                MapPosition: mapDto,
                Notes: u.Notes
            );
        }).ToList();
    }

    public async Task<StaffUnitDetailDto> GetUnitDetailAsync(
        long employeeUserId,
        long unitId,
        CancellationToken cancellationToken = default)
    {
        var unit = await _dbContext.StorageUnits
            .AsNoTracking()
            .Include(u => u.Facility)
            .Include(u => u.UnitType)
            .Include(u => u.UnitAllocations.Where(a => a.Status == AllocationStatusConstants.Active))
                .ThenInclude(a => a.Agreement)
                    .ThenInclude(ag => ag!.Customer)
                        .ThenInclude(c => c.User)
            .FirstOrDefaultAsync(u => u.Id == unitId, cancellationToken);

        if (unit == null)
            throw AppException.FromError(StaffErrors.UnitNotFound);

        await EnsureStrictFacilityAccessAsync(employeeUserId, unit.FacilityId, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rate = await _dbContext.FacilityRates
            .AsNoTracking()
            .Where(fr => fr.FacilityId == unit.FacilityId && fr.UnitTypeId == unit.UnitTypeId && fr.ValidFrom <= today && (!fr.ValidTo.HasValue || fr.ValidTo.Value > today))
            .OrderByDescending(fr => fr.ValidFrom)
            .FirstOrDefaultAsync(cancellationToken);

        StaffCurrentAgreementInfoDto? currentAgDto = null;
        var activeAlloc = unit.UnitAllocations.FirstOrDefault();
        if (activeAlloc?.Agreement != null)
        {
            var ag = activeAlloc.Agreement;
            currentAgDto = new StaffCurrentAgreementInfoDto(
                AgreementId: ag.Id,
                AgreementNo: ag.AgreementNo,
                CustomerId: ag.CustomerId,
                CustomerName: ag.Customer.FullName,
                CustomerPhone: ag.Customer.User?.PhoneNumber,
                CustomerEmail: ag.Customer.User?.Email,
                StartDate: ag.StartDate,
                EndDate: ag.EndDate,
                Status: ag.Status
            );
        }

        return new StaffUnitDetailDto(
            UnitId: unit.Id,
            UnitCode: unit.UnitCode,
            FacilityId: unit.FacilityId,
            FacilityName: unit.Facility.Name,
            FloorLabel: unit.FloorLabel,
            ZoneLabel: unit.ZoneLabel,
            PhysicalStatus: unit.PhysicalStatus,
            IsListed: unit.IsListed,
            Notes: unit.Notes,
            UnitTypeId: unit.UnitTypeId,
            UnitTypeName: unit.UnitType.Name,
            WidthM: unit.UnitType.WidthM,
            LengthM: unit.UnitType.LengthM,
            HeightM: unit.UnitType.HeightM,
            AreaM2: unit.UnitType.AreaM2,
            VolumeM3: unit.UnitType.VolumeM3,
            ClimateControlled: unit.UnitType.ClimateControlled,
            MonthlyRate: rate?.MonthlyRate ?? 0m,
            DepositAmount: rate?.DepositAmount ?? (rate?.MonthlyRate ?? 0m),
            BookingFee: rate?.BookingFee ?? 0m,
            CurrentAgreement: currentAgDto
        );
    }

    // ==========================================
    // 🛒 NHÓM 2: ĐẶT THUÊ KHO TẠI QUẦY (Walk-in Booking)
    // ==========================================

    
    public Task<StaffWalkInBookingResponse> CreateWalkInBookingAsync(
        long employeeUserId,
        StaffWalkInBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Tinh nang dat thue tai quay da bi vo hieu hoa.");
    }

    // ==========================================
    // 📷 NHÓM 3: QUÉT QR (Check-in / Check-out / Ra vào)
    // ==========================================

    public async Task<StaffQrVerifyResponse> VerifyQrAsync(
        long employeeUserId,
        StaffQrVerifyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.QrCode))
            throw AppException.FromError(StaffErrors.QrCodeInvalid);

        var raw = request.QrCode.Trim();

        // Parse compact format "CHK|unit|agrNo|rsvCode|facId" or legacy FE format
        if (raw.StartsWith("CHK|", StringComparison.OrdinalIgnoreCase))
        {
            var parts = raw.Split('|');
            var candidateAgr = parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) && parts[2].StartsWith("AGR-", StringComparison.OrdinalIgnoreCase)
                ? parts[2].Trim()
                : null;
            var candidateRsv = parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3]) && parts[3].StartsWith("RSV-", StringComparison.OrdinalIgnoreCase)
                ? parts[3].Trim()
                : null;

            if (!string.IsNullOrWhiteSpace(candidateAgr))
            {
                var agRecord = await _dbContext.RentalAgreements.AsNoTracking().FirstOrDefaultAsync(a => a.AgreementNo == candidateAgr, cancellationToken);
                if (agRecord != null && agRecord.CheckedInAt != null)
                {
                    raw = candidateAgr;
                }
                else if (!string.IsNullOrWhiteSpace(candidateRsv))
                {
                    raw = candidateRsv;
                }
                else
                {
                    raw = candidateAgr;
                }
            }
            else if (!string.IsNullOrWhiteSpace(candidateRsv))
            {
                raw = candidateRsv;
            }
            else if (parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3]))
            {
                raw = parts[3].Trim();
            }
            else if (parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]))
            {
                raw = parts[2].Trim();
            }
        }

        // 1. Check if QR is a Reservation Check-In QR (e.g. "CHECKIN:RSV-1234:10:1" or "RSV-1234")
        if (raw.StartsWith("CHECKIN:", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("RSV-", StringComparison.OrdinalIgnoreCase))
        {
            var rsvCode = raw.StartsWith("CHECKIN:", StringComparison.OrdinalIgnoreCase)
                ? raw.Split(':')[1]
                : raw;

            var reservation = await _dbContext.Reservations
                .Include(r => r.Customer).ThenInclude(c => c.User)
                .Include(r => r.Facility)
                .Include(r => r.UnitType)
                .Include(r => r.UnitAllocation).ThenInclude(a => a!.StorageUnit)
                .Include(r => r.RentalAgreement)
                .FirstOrDefaultAsync(r => r.ReservationCode == rsvCode, cancellationToken);

            if (reservation != null)
            {
                await ResolveFacilityIdAsync(employeeUserId, reservation.FacilityId, cancellationToken);

                var customerDto = new StaffCustomerSummaryDto(
                    reservation.CustomerId,
                    reservation.Customer.FullName,
                    reservation.Customer.User?.PhoneNumber,
                    reservation.Customer.User?.Email
                );

                var rsvDto = new StaffReservationSummaryDto(
                    reservation.Id,
                    reservation.ReservationCode,
                    reservation.StartDate,
                    reservation.EndDate,
                    reservation.Status
                );

                StaffUnitSummaryDto? unitSummary = null;
                var allocatedUnit = reservation.UnitAllocation?.StorageUnit;
                if (allocatedUnit != null)
                {
                    unitSummary = new StaffUnitSummaryDto(
                        allocatedUnit.Id,
                        allocatedUnit.UnitCode,
                        allocatedUnit.FacilityId,
                        reservation.Facility.Name,
                        reservation.UnitType.Name,
                        allocatedUnit.PhysicalStatus
                    );
                }

                StaffAgreementSummaryDto? agSummary = null;
                if (reservation.RentalAgreement != null)
                {
                    var ag = reservation.RentalAgreement;
                    agSummary = new StaffAgreementSummaryDto(
                        ag.Id,
                        ag.AgreementNo,
                        ag.FacilityId,
                        reservation.Facility.Name,
                        allocatedUnit?.Id ?? 0,
                        allocatedUnit?.UnitCode ?? "N/A",
                        ag.StartDate,
                        ag.EndDate,
                        ag.Status
                    );
                }

                return new StaffQrVerifyResponse(
                    ActionType: StaffActionConstants.CheckIn,
                    Title: $"Khách {reservation.Customer.FullName} - Nhận kho {reservation.Facility.Name}",
                    Description: $"Đơn đặt chỗ {reservation.ReservationCode} ({reservation.UnitType.Name} - {allocatedUnit?.UnitCode ?? "Chưa chỉ định kho"}).",
                    Customer: customerDto,
                    Agreement: agSummary,
                    Reservation: rsvDto,
                    TransferRequest: null,
                    Unit: unitSummary,
                    AllowedActions: new List<string> { "confirm_checkin" }
                );
            }
        }

        // 2. Check if QR is a Transfer Request ("TRANSFER:123" or ID or Agreement Transfer)
        if (raw.StartsWith("TRANSFER:", StringComparison.OrdinalIgnoreCase) || long.TryParse(raw, out _))
        {
            var transferIdStr = raw.StartsWith("TRANSFER:", StringComparison.OrdinalIgnoreCase)
                ? raw.Split(':')[1]
                : raw;

            if (long.TryParse(transferIdStr, out var transferId))
            {
                var transfer = await _dbContext.UnitTransferRequests
                    .Include(t => t.Agreement).ThenInclude(a => a.Customer).ThenInclude(c => c.User)
                    .Include(t => t.FromUnit).ThenInclude(u => u.Facility)
                    .Include(t => t.ToUnit)
                    .FirstOrDefaultAsync(t => t.Id == transferId, cancellationToken);

                if (transfer != null)
                {
                    var transferFacilityId = transfer.Status == TransferRequestStatusConstants.MovedOutInTransit
                        ? transfer.RequestedFacilityId
                        : transfer.FromUnit.FacilityId;
                    await ResolveFacilityIdAsync(employeeUserId, transferFacilityId, cancellationToken);

                    var reqFacility = await _dbContext.Facilities.FindAsync(transfer.RequestedFacilityId);
                    return BuildTransferQrResponse(employeeUserId, transfer, reqFacility?.Name ?? "Cơ sở mới");
                }
            }
        }

        // 3. Check if QR is Move-out Request ("MOVEOUT:123")
        if (raw.StartsWith("MOVEOUT:", StringComparison.OrdinalIgnoreCase))
        {
            var moveOutIdStr = raw.Split(':')[1];
            if (long.TryParse(moveOutIdStr, out var moveOutId))
            {
                var moveOut = await _dbContext.MoveOutRequests
                    .Include(m => m.Agreement).ThenInclude(a => a.Customer).ThenInclude(c => c.User)
                    .Include(m => m.Agreement).ThenInclude(a => a.Facility)
                    .FirstOrDefaultAsync(m => m.Id == moveOutId, cancellationToken);

                if (moveOut != null)
                {
                    await ResolveFacilityIdAsync(employeeUserId, moveOut.Agreement.FacilityId, cancellationToken);

                    var customerDto = new StaffCustomerSummaryDto(
                        moveOut.Agreement.CustomerId,
                        moveOut.Agreement.Customer.FullName,
                        moveOut.Agreement.Customer.User?.PhoneNumber,
                        moveOut.Agreement.Customer.User?.Email
                    );

                    return new StaffQrVerifyResponse(
                        ActionType: StaffActionConstants.MoveOut,
                        Title: $"Khách {moveOut.Agreement.Customer.FullName} - Trả kho {moveOut.Agreement.Facility.Name}",
                        Description: $"Yêu cầu dọn kho hợp đồng {moveOut.Agreement.AgreementNo} ngày {moveOut.RequestedMoveOutDate:yyyy-MM-dd}.",
                        Customer: customerDto,
                        Agreement: new StaffAgreementSummaryDto(moveOut.AgreementId, moveOut.Agreement.AgreementNo, moveOut.Agreement.FacilityId, moveOut.Agreement.Facility.Name, 0, "N/A", moveOut.Agreement.StartDate, moveOut.Agreement.EndDate, moveOut.Agreement.Status),
                        Reservation: null,
                        TransferRequest: null,
                        Unit: null,
                        AllowedActions: new List<string> { "confirm_moveout" }
                    );
                }
            }
        }

        // 4. Check if QR is an Agreement No or Access Token
        string? agreementNo = raw.StartsWith("AGR-", StringComparison.OrdinalIgnoreCase) ? raw : null;

        // Try decode JWT Gate Token
        if (agreementNo == null && raw.Length > 20 && raw.Contains('.'))
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(raw))
                {
                    var jwt = handler.ReadJwtToken(raw);
                    var agClaim = jwt.Claims.FirstOrDefault(c => c.Type == "agreement_id" || c.Type == "agreementId");
                    if (agClaim != null && long.TryParse(agClaim.Value, out var agId))
                    {
                        var agRecord = await _dbContext.RentalAgreements.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agId, cancellationToken);
                        if (agRecord != null) agreementNo = agRecord.AgreementNo;
                    }
                }
            }
            catch
            {
                // Ignore parse errors, proceed to fallback search
            }
        }

        if (!string.IsNullOrWhiteSpace(agreementNo))
        {
            var agreement = await _dbContext.RentalAgreements
                .Include(a => a.Customer).ThenInclude(c => c.User)
                .Include(a => a.Facility)
                .Include(a => a.UnitAllocations.Where(ua => ua.Status == AllocationStatusConstants.Active)).ThenInclude(ua => ua.StorageUnit).ThenInclude(u => u.UnitType)
                .FirstOrDefaultAsync(a => a.AgreementNo == agreementNo, cancellationToken);

            if (agreement != null)
            {
                // Check if this agreement has an active transfer request
                var activeTransfer = await _dbContext.UnitTransferRequests
                    .Include(t => t.Agreement).ThenInclude(a => a.Customer).ThenInclude(c => c.User)
                    .Include(t => t.FromUnit).ThenInclude(u => u.Facility)
                    .Include(t => t.ToUnit)
                    .FirstOrDefaultAsync(t => t.AgreementId == agreement.Id &&
                        (t.Status == TransferRequestStatusConstants.PendingPayment ||
                         t.Status == TransferRequestStatusConstants.Paid ||
                         t.Status == TransferRequestStatusConstants.Pending ||
                         t.Status == TransferRequestStatusConstants.Approved ||
                         t.Status == TransferRequestStatusConstants.Scheduled ||
                         t.Status == TransferRequestStatusConstants.MovedOutInTransit), cancellationToken);

                if (activeTransfer != null)
                {
                    var transferFacilityId = activeTransfer.Status == TransferRequestStatusConstants.MovedOutInTransit
                        ? activeTransfer.RequestedFacilityId
                        : activeTransfer.FromUnit.FacilityId;
                    await ResolveFacilityIdAsync(employeeUserId, transferFacilityId, cancellationToken);

                    var reqFacility = await _dbContext.Facilities.FindAsync(activeTransfer.RequestedFacilityId);
                    return BuildTransferQrResponse(employeeUserId, activeTransfer, reqFacility?.Name ?? "Cơ sở mới");
                }

                await ResolveFacilityIdAsync(employeeUserId, agreement.FacilityId, cancellationToken);

                // Check if this agreement has an active move-out request
                var activeMoveOut = await _dbContext.MoveOutRequests
                    .Include(m => m.Agreement).ThenInclude(a => a.Customer).ThenInclude(c => c.User)
                    .Include(m => m.Agreement).ThenInclude(a => a.Facility)
                    .FirstOrDefaultAsync(m => m.AgreementId == agreement.Id &&
                        (m.Status == MoveOutStatusConstants.Requested || m.Status == MoveOutStatusConstants.Scheduled || m.Status == MoveOutStatusConstants.InProgress), cancellationToken);

                if (activeMoveOut != null)
                {
                    return new StaffQrVerifyResponse(
                        ActionType: StaffActionConstants.MoveOut,
                        Title: $"Khách {agreement.Customer.FullName} - Trả kho {agreement.Facility.Name}",
                        Description: $"Khách hàng có lịch dọn kho vào ngày {activeMoveOut.RequestedMoveOutDate:yyyy-MM-dd}.",
                        Customer: new StaffCustomerSummaryDto(agreement.CustomerId, agreement.Customer.FullName, agreement.Customer.User?.PhoneNumber, agreement.Customer.User?.Email),
                        Agreement: new StaffAgreementSummaryDto(agreement.Id, agreement.AgreementNo, agreement.FacilityId, agreement.Facility.Name, 0, "N/A", agreement.StartDate, agreement.EndDate, agreement.Status),
                        Reservation: null,
                        TransferRequest: null,
                        Unit: null,
                        AllowedActions: new List<string> { "confirm_moveout" }
                    );
                }

                var unitAlloc = agreement.UnitAllocations.FirstOrDefault();
                var unitSummary = unitAlloc?.StorageUnit != null
                    ? new StaffUnitSummaryDto(unitAlloc.StorageUnit.Id, unitAlloc.StorageUnit.UnitCode, agreement.FacilityId, agreement.Facility.Name, unitAlloc.StorageUnit.UnitType.Name, unitAlloc.StorageUnit.PhysicalStatus)
                    : null;

                var isPendingCheckIn = agreement.CheckedInAt == null || agreement.Status != RentalAgreementStatusConstants.Active;
                var actionType = isPendingCheckIn ? "check_in" : "gate_access";
                var title = isPendingCheckIn
                    ? $"Khách {agreement.Customer.FullName} - Nhận kho {agreement.Facility.Name}"
                    : $"Khách {agreement.Customer.FullName} - Ra vào kho {unitSummary?.UnitCode ?? ""}";

                return new StaffQrVerifyResponse(
                    ActionType: actionType,
                    Title: title,
                    Description: $"Hợp đồng {agreement.AgreementNo} ({agreement.Status}). Hiệu lực: {agreement.StartDate:yyyy-MM-dd} đến {agreement.EndDate:yyyy-MM-dd}.",
                    Customer: new StaffCustomerSummaryDto(agreement.CustomerId, agreement.Customer.FullName, agreement.Customer.User?.PhoneNumber, agreement.Customer.User?.Email),
                    Agreement: new StaffAgreementSummaryDto(agreement.Id, agreement.AgreementNo, agreement.FacilityId, agreement.Facility.Name, unitSummary?.UnitId ?? 0, unitSummary?.UnitCode ?? "N/A", agreement.StartDate, agreement.EndDate, agreement.Status),
                    Reservation: null,
                    TransferRequest: null,
                    Unit: unitSummary,
                    AllowedActions: isPendingCheckIn ? new List<string> { "confirm_checkin" } : new List<string> { "gate_allowed" }
                );
            }
        }

        throw AppException.FromError(StaffErrors.QrCodeInvalid);
    }

    private StaffQrVerifyResponse BuildTransferQrResponse(long employeeUserId, UnitTransferRequest transfer, string requestedFacilityName)
    {
        var customerDto = new StaffCustomerSummaryDto(
            transfer.Agreement.CustomerId,
            transfer.Agreement.Customer.FullName,
            transfer.Agreement.Customer.User?.PhoneNumber,
            transfer.Agreement.Customer.User?.Email
        );

        var transferDto = new StaffTransferSummaryDto(
            TransferRequestId: transfer.Id,
            AgreementId: transfer.AgreementId,
            AgreementNo: transfer.Agreement.AgreementNo,
            FromFacilityId: transfer.FromUnit.FacilityId,
            FromFacilityName: transfer.FromUnit.Facility.Name,
            ToFacilityId: transfer.RequestedFacilityId,
            ToFacilityName: requestedFacilityName,
            FromUnitId: transfer.FromUnitId,
            FromUnitCode: transfer.FromUnit.UnitCode,
            ToUnitId: transfer.ToUnitId,
            ToUnitCode: transfer.ToUnit?.UnitCode,
            Status: transfer.Status
        );

        // If transfer is already moved out and in transit, next step is receiving at destination
        if (transfer.Status == TransferRequestStatusConstants.MovedOutInTransit)
        {
            return new StaffQrVerifyResponse(
                ActionType: StaffActionConstants.TransferMoveIn,
                Title: $"Khách {transfer.Agreement.Customer.FullName} - Nhận kho {requestedFacilityName} khi chuyển cơ sở",
                Description: $"Đã dọn kho đi từ {transfer.FromUnit.Facility.Name}. Sẵn sàng nhận ô kho tại {requestedFacilityName}.",
                Customer: customerDto,
                Agreement: null,
                Reservation: null,
                TransferRequest: transferDto,
                Unit: transfer.ToUnit != null ? new StaffUnitSummaryDto(transfer.ToUnit.Id, transfer.ToUnit.UnitCode, transfer.RequestedFacilityId, requestedFacilityName, "N/A", transfer.ToUnit.PhysicalStatus) : null,
                AllowedActions: new List<string> { "confirm_transfer_movein" }
            );
        }

        // Otherwise, move-out from origin facility
        return new StaffQrVerifyResponse(
            ActionType: "transfer_move_out",
            Title: $"Khách {transfer.Agreement.Customer.FullName} - Dọn kho {transfer.FromUnit.Facility.Name} chuyển đi",
            Description: $"Dọn kho chuyển cơ sở từ {transfer.FromUnit.Facility.Name} (kho {transfer.FromUnit.UnitCode}) sang {requestedFacilityName}.",
            Customer: customerDto,
            Agreement: null,
            Reservation: null,
            TransferRequest: transferDto,
            Unit: new StaffUnitSummaryDto(transfer.FromUnitId, transfer.FromUnit.UnitCode, transfer.FromUnit.FacilityId, transfer.FromUnit.Facility.Name, "N/A", transfer.FromUnit.PhysicalStatus),
            AllowedActions: new List<string> { "confirm_transfer_moveout" }
        );
    }

    public async Task<StaffConfirmQrActionResultDto> ConfirmQrActionAsync(
        long employeeUserId,
        StaffConfirmQrActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var action = request.ActionType.Trim().ToLowerInvariant();

        // Normalize defensive IDs: treat non-positive values (<= 0) as null
        long? reservationId = request.ReservationId > 0 ? request.ReservationId : null;
        long? agreementId = request.AgreementId > 0 ? request.AgreementId : null;
        long? transferRequestId = request.TransferRequestId > 0 ? request.TransferRequestId : null;
        long? moveOutId = request.MoveOutId > 0 ? request.MoveOutId : null;
        long? unitId = request.UnitId > 0 ? request.UnitId : null;

        await using var transaction = _dbContext.Database.CurrentTransaction == null
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            StaffConfirmQrActionResultDto? result = null;

        // ----------------------------------------------------
        // ACTION 0: RA VÀO KHO (gate_access / gate_allowed)
        // ----------------------------------------------------
        if (action == "gate_allowed" || action == "gate_access")
        {
            result = new StaffConfirmQrActionResultDto(
                Success: true,
                ActionType: "gate_access",
                Message: "Xác nhận khách hàng ra vào kho hợp lệ.",
                AgreementId: agreementId,
                AgreementStatus: RentalAgreementStatusConstants.Active,
                UnitId: unitId,
                UnitStatus: StorageUnitStatusConstants.Occupied
            );
        }

        // ----------------------------------------------------
        // ACTION 1: CHECK-IN NHẬN KHO
        // ----------------------------------------------------
        if (action == StaffActionConstants.CheckIn || action == StaffActionConstants.ConfirmCheckIn)
        {
            RentalAgreement? agreement = null;
            StorageUnit? unit = null;

            if (reservationId.HasValue)
            {
                var reservation = await _dbContext.Reservations
                    .Include(r => r.RentalAgreement)
                    .Include(r => r.UnitAllocation).ThenInclude(a => a!.StorageUnit)
                    .FirstOrDefaultAsync(r => r.Id == reservationId.Value, cancellationToken);

                if (reservation == null) throw AppException.FromError(StaffErrors.ReservationNotFound);

                await ResolveFacilityIdAsync(employeeUserId, reservation.FacilityId, cancellationToken);

                if (reservation.Status != ReservationStatusConstants.Confirmed)
                {
                    throw AppException.FromError(StaffErrors.ReservationNotConfirmed);
                }

                reservation.Status = ReservationStatusConstants.Converted;
                reservation.UpdatedAt = now;

                unit = reservation.UnitAllocation?.StorageUnit;
                if (unit == null && unitId.HasValue)
                {
                    unit = await _dbContext.StorageUnits.FindAsync(new object[] { unitId.Value }, cancellationToken);
                    if (unit == null)
                        throw AppException.FromError(StaffErrors.UnitNotFound);

                    if (unit.FacilityId != reservation.FacilityId)
                        throw AppException.FromError(Error.Validation("Staff.FacilityMismatch", "Ô kho được chọn không thuộc cùng cơ sở với đơn đặt chỗ."));

                    if (unit.UnitTypeId != reservation.UnitTypeId)
                        throw AppException.FromError(StaffErrors.UnitTypeMismatch);

                    if (!string.Equals(unit.PhysicalStatus, StorageUnitStatusConstants.Available, StringComparison.OrdinalIgnoreCase))
                        throw AppException.FromError(StaffErrors.UnitNotAvailable);
                }

                agreement = reservation.RentalAgreement;
                if (agreement == null)
                {
                    var agreementNo = $"{StaffConstants.AgreementPrefix}{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
                    agreement = new RentalAgreement
                    {
                        AgreementNo = agreementNo,
                        CustomerId = reservation.CustomerId,
                        FacilityId = reservation.FacilityId,
                        ReservationId = reservation.Id,
                        PolicyVersionId = 1,
                        StartDate = reservation.StartDate,
                        EndDate = reservation.EndDate,
                        MonthlyRateSnapshot = reservation.MonthlyRateSnapshot,
                        DepositSnapshot = reservation.DepositSnapshot,
                        DepositBalance = reservation.DepositSnapshot,
                        Status = RentalAgreementStatusConstants.Active,
                        SignedAt = now,
                        CheckedInAt = now,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _dbContext.RentalAgreements.Add(agreement);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }
            else if (agreementId.HasValue)
            {
                agreement = await _dbContext.RentalAgreements
                    .Include(a => a.UnitAllocations.Where(ua => ua.Status == AllocationStatusConstants.Active)).ThenInclude(ua => ua.StorageUnit)
                    .FirstOrDefaultAsync(a => a.Id == agreementId.Value, cancellationToken);

                if (agreement == null) throw AppException.FromError(StaffErrors.AgreementNotFound);
                unit = agreement.UnitAllocations.FirstOrDefault()?.StorageUnit;

                await ResolveFacilityIdAsync(employeeUserId, agreement.FacilityId, cancellationToken);
            }

            if (agreement == null)
                throw AppException.FromError(StaffErrors.AgreementNotFound);

            if (string.Equals(agreement.Status, RentalAgreementStatusConstants.Draft, StringComparison.OrdinalIgnoreCase))
            {
                agreement.Status = RentalAgreementStatusConstants.Scheduled;
                agreement.UpdatedAt = now;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            agreement.Status = RentalAgreementStatusConstants.Active;
            agreement.CheckedInAt = now;
            agreement.UpdatedAt = now;

            UnitAllocation? alloc = null;
            if (unit != null)
            {
                unit.PhysicalStatus = StorageUnitStatusConstants.Occupied;
                unit.UpdatedAt = now;

                var existingRentalAlloc = await _dbContext.UnitAllocations
                    .FirstOrDefaultAsync(a => a.AgreementId == agreement.Id && a.Status == AllocationStatusConstants.Active, cancellationToken);
                if (existingRentalAlloc == null)
                {
                    var holdAlloc = await _dbContext.UnitAllocations
                        .FirstOrDefaultAsync(a => a.ReservationId == agreement.ReservationId && a.Status == AllocationStatusConstants.Active, cancellationToken);
                    if (holdAlloc != null)
                    {
                        holdAlloc.Status = AllocationStatusConstants.Consumed;
                        holdAlloc.EndedAt = now;
                        await _dbContext.SaveChangesAsync(cancellationToken);
                    }

                    var newRentalAlloc = new UnitAllocation
                    {
                        AgreementId = agreement.Id,
                        StorageUnitId = unit.Id,
                        AllocationKind = AllocationKindConstants.Rental,
                        AllocationStartDate = agreement.StartDate,
                        AllocationEndDate = agreement.EndDate,
                        Status = AllocationStatusConstants.Active,
                        CreatedAt = now
                    };
                    _dbContext.UnitAllocations.Add(newRentalAlloc);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    alloc = newRentalAlloc;
                }
                else
                {
                    alloc = existingRentalAlloc;
                }
            }

            // Ensure PIN code is generated
            var pinCred = await _dbContext.AccessCredentials
                .FirstOrDefaultAsync(c => c.AgreementId == agreement.Id && c.CredentialType == CredentialTypeConstants.Pin, cancellationToken);
            if (pinCred == null)
            {
                var randomPin = Random.Shared.Next(100000, 999999).ToString("D6");
                _dbContext.AccessCredentials.Add(new AccessCredential
                {
                    AgreementId = agreement.Id,
                    CredentialType = CredentialTypeConstants.Pin,
                    SecretDigest = BCrypt.Net.BCrypt.HashPassword(randomPin),
                    DisplayHint = PinProtectionHelper.Encrypt(randomPin),
                    IssuedAt = now,
                    ExpiresAt = now.AddYears(1),
                    Status = CredentialStatusConstants.Active,
                    CreatedAt = now
                });
            }

            // Ensure Check-in Inspection and HandoverRecord exist per BR-RSV-04
            var existingHandover = await _dbContext.HandoverRecords
                .FirstOrDefaultAsync(h => h.AgreementId == agreement.Id && h.HandoverType == HandoverTypeConstants.CheckIn, cancellationToken);

            if (existingHandover == null)
            {
                alloc ??= await _dbContext.UnitAllocations
                    .FirstOrDefaultAsync(a => a.AgreementId == agreement.Id && a.Status == AllocationStatusConstants.Active, cancellationToken);

                if (alloc != null && unit != null)
                {
                    var isCurrentStaffAssigned = await _dbContext.EmployeeProfiles.AnyAsync(ep => ep.UserId == employeeUserId, cancellationToken)
                        && await _dbContext.StaffFacilityAssignments.AnyAsync(
                            sfa => sfa.EmployeeId == employeeUserId
                                   && sfa.FacilityId == agreement.FacilityId
                                   && sfa.StartsAt <= now
                                   && (sfa.EndsAt == null || sfa.EndsAt > now),
                            cancellationToken);

                    var validStaffId = isCurrentStaffAssigned
                        ? employeeUserId
                        : await _dbContext.StaffFacilityAssignments
                            .Where(sfa => sfa.FacilityId == agreement.FacilityId
                                          && sfa.StartsAt <= now
                                          && (sfa.EndsAt == null || sfa.EndsAt > now))
                            .Select(sfa => sfa.EmployeeId)
                            .FirstOrDefaultAsync(cancellationToken);

                    if (validStaffId <= 0)
                    {
                        validStaffId = await _dbContext.EmployeeProfiles
                            .Where(ep => ep.UserId == employeeUserId)
                            .Select(ep => ep.UserId)
                            .FirstOrDefaultAsync(cancellationToken);
                    }
                    if (validStaffId <= 0)
                    {
                        validStaffId = await _dbContext.EmployeeProfiles
                            .Select(ep => ep.UserId)
                            .FirstOrDefaultAsync(cancellationToken);
                    }

                    if (validStaffId > 0)
                    {
                        var checkInInspection = new Inspection
                        {
                            AgreementId = agreement.Id,
                            FacilityId = agreement.FacilityId,
                            StorageUnitId = unit.Id,
                            InspectedBy = validStaffId,
                            InspectionType = "check_in",
                            OverallCondition = "good",
                            Status = InspectionStatusConstants.Completed,
                            Summary = request.Notes ?? "Biên bản kiểm tra bàn giao nhận kho (Check-in handover)",
                            InspectedAt = now,
                            CreatedAt = now
                        };
                        _dbContext.Inspections.Add(checkInInspection);
                        await _dbContext.SaveChangesAsync(cancellationToken);

                        var handover = new HandoverRecord
                        {
                            AgreementId = agreement.Id,
                            UnitAllocationId = alloc.Id,
                            InspectionId = checkInInspection.Id,
                            HandledBy = validStaffId,
                            HandoverType = HandoverTypeConstants.CheckIn,
                            CustomerSignedAt = now,
                            StaffSignedAt = now,
                            Notes = request.Notes ?? "Bàn giao chìa khóa/mã mở cửa và hiện trạng kho ban đầu tốt.",
                            CreatedAt = now
                        };
                        _dbContext.HandoverRecords.Add(handover);
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            result = new StaffConfirmQrActionResultDto(
                Success: true,
                ActionType: StaffActionConstants.CheckIn,
                Message: "Kích hoạt hợp đồng và nhận kho thành công. Ô kho đã chuyển sang trạng thái Occupied.",
                AgreementId: agreement.Id,
                AgreementStatus: agreement.Status,
                UnitId: unit?.Id,
                UnitStatus: unit?.PhysicalStatus
            );
        }

        // ----------------------------------------------------
        // ACTION 2: DỌN KHO CHUYỂN CƠ SỞ (transfer_move_out)
        // ----------------------------------------------------
        if (action == StaffActionConstants.TransferMoveOut || action == StaffActionConstants.ConfirmTransferMoveOut || action == StaffActionConstants.TransferOut)
        {
            var transferQuery = _dbContext.UnitTransferRequests
                .Include(t => t.FromUnit)
                .Include(t => t.Agreement)
                .AsQueryable();

            UnitTransferRequest? transfer = null;
            if (transferRequestId.HasValue && agreementId.HasValue)
            {
                transfer = await transferQuery.FirstOrDefaultAsync(
                    t => t.Id == transferRequestId.Value || t.AgreementId == agreementId.Value, cancellationToken);
            }
            else if (transferRequestId.HasValue)
            {
                transfer = await transferQuery.FirstOrDefaultAsync(
                    t => t.Id == transferRequestId.Value, cancellationToken);
            }
            else if (agreementId.HasValue)
            {
                transfer = await transferQuery.FirstOrDefaultAsync(
                    t => t.AgreementId == agreementId.Value, cancellationToken);
            }

            if (transfer == null) throw AppException.FromError(StaffErrors.TransferNotFound);

            await ResolveFacilityIdAsync(employeeUserId, transfer.FromUnit.FacilityId, cancellationToken);
            if (transfer.Status != TransferRequestStatusConstants.Paid && transfer.Status != TransferRequestStatusConstants.Scheduled)
                throw AppException.FromError(StaffErrors.TransferNotReady);

            transfer.Status = TransferRequestStatusConstants.MovedOutInTransit;

            // Free origin unit and create cleaning ticket
            var fromUnit = await _dbContext.StorageUnits.FindAsync(new object[] { transfer.FromUnitId }, cancellationToken);
            if (fromUnit != null)
            {
                fromUnit.PhysicalStatus = StorageUnitStatusConstants.Maintenance;
                fromUnit.UpdatedAt = now;

                var cleaningTicket = new SupportTicket
                {
                    TicketNo = TicketConstants.GenerateTicketNo(),
                    CustomerId = transfer.Agreement.CustomerId,
                    FacilityId = fromUnit.FacilityId,
                    AgreementId = transfer.AgreementId,
                    StorageUnitId = fromUnit.Id,
                    Category = MaintenanceCategoryConstants.Cleaning,
                    Priority = TicketPriorityConstants.Normal,
                    Subject = $"Dọn vệ sinh ô kho {fromUnit.UnitCode} sau khi khách dọn đi",
                    Description = $"Khách hàng đã hoàn tất dọn kho để chuyển cơ sở. Nhân viên cần kiểm tra hiện trạng và dọn dẹp sạch sẽ ô kho {fromUnit.UnitCode}.",
                    Status = TicketStatusConstants.Open,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.SupportTickets.Add(cleaningTicket);
            }

            // Deactivate old allocation
            var oldAllocations = await _dbContext.UnitAllocations
                .Where(ua => ua.AgreementId == transfer.AgreementId && ua.StorageUnitId == transfer.FromUnitId && ua.Status == AllocationStatusConstants.Active)
                .ToListAsync(cancellationToken);
            foreach (var alloc in oldAllocations)
            {
                alloc.Status = AllocationStatusConstants.Released;
                alloc.EndedAt = now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            result = new StaffConfirmQrActionResultDto(
                Success: true,
                ActionType: StaffActionConstants.TransferMoveOut,
                Message: "Đã xác nhận dọn kho chuyển cơ sở. Trạng thái chuyển sang MovedOut_InTransit, đã nhả kho và tạo ticket dọn vệ sinh.",
                AgreementId: transfer.AgreementId,
                AgreementStatus: transfer.Agreement.Status,
                UnitId: fromUnit?.Id,
                UnitStatus: fromUnit?.PhysicalStatus
            );
        }

        // ----------------------------------------------------
        // ACTION 3: NHẬN KHO KHI CHUYỂN CƠ SỞ (transfer_move_in)
        // ----------------------------------------------------
        if (action == StaffActionConstants.TransferMoveIn || action == StaffActionConstants.ConfirmTransferMoveIn || action == StaffActionConstants.TransferIn || action == StaffActionConstants.TransferCheckIn)
        {
            var transferQuery = _dbContext.UnitTransferRequests
                .Include(t => t.ToUnit)
                .Include(t => t.Agreement)
                .AsQueryable();

            UnitTransferRequest? transfer = null;
            if (transferRequestId.HasValue && agreementId.HasValue)
            {
                transfer = await transferQuery.FirstOrDefaultAsync(
                    t => t.Id == transferRequestId.Value || t.AgreementId == agreementId.Value, cancellationToken);
            }
            else if (transferRequestId.HasValue)
            {
                transfer = await transferQuery.FirstOrDefaultAsync(
                    t => t.Id == transferRequestId.Value, cancellationToken);
            }
            else if (agreementId.HasValue)
            {
                transfer = await transferQuery.FirstOrDefaultAsync(
                    t => t.AgreementId == agreementId.Value, cancellationToken);
            }

            if (transfer == null) throw AppException.FromError(StaffErrors.TransferNotFound);

            await ResolveFacilityIdAsync(employeeUserId, transfer.RequestedFacilityId, cancellationToken);
            if (transfer.Status != TransferRequestStatusConstants.MovedOutInTransit)
                throw AppException.FromError(StaffErrors.TransferNotReady);

            StorageUnit? toUnit = transfer.ToUnit;
            if (toUnit == null && unitId.HasValue)
            {
                toUnit = await _dbContext.StorageUnits.FindAsync(new object[] { unitId.Value }, cancellationToken);
                if (toUnit == null)
                    throw AppException.FromError(StaffErrors.UnitNotFound);

                if (toUnit.FacilityId != transfer.RequestedFacilityId)
                    throw AppException.FromError(Error.Validation("Staff.FacilityMismatch", "Ô kho được chọn không thuộc cùng cơ sở với yêu cầu chuyển kho."));

                if (toUnit.UnitTypeId != transfer.RequestedUnitTypeId)
                    throw AppException.FromError(StaffErrors.UnitTypeMismatch);

                if (!string.Equals(toUnit.PhysicalStatus, StorageUnitStatusConstants.Available, StringComparison.OrdinalIgnoreCase))
                    throw AppException.FromError(StaffErrors.UnitNotAvailable);

                transfer.ToUnitId = toUnit.Id;
            }

            // Step 1: Release any active old allocation & temporarily detach cross-facility linked tickets
            var oldAlloc = await _dbContext.UnitAllocations
                .FirstOrDefaultAsync(a => a.AgreementId == transfer.AgreementId && a.Status == AllocationStatusConstants.Active, cancellationToken);
            if (oldAlloc != null)
            {
                oldAlloc.Status = AllocationStatusConstants.Released;
                oldAlloc.EndedAt = now;
                var todayAlloc = DateOnly.FromDateTime(DateTime.UtcNow);
                if (todayAlloc > oldAlloc.AllocationStartDate)
                {
                    oldAlloc.AllocationEndDate = todayAlloc;
                }
                else if (oldAlloc.AllocationEndDate <= oldAlloc.AllocationStartDate)
                {
                    oldAlloc.AllocationEndDate = oldAlloc.AllocationStartDate.AddDays(1);
                }
            }

            List<SupportTicket>? linkedTickets = null;
            if (transfer.Agreement != null && transfer.Agreement.FacilityId != transfer.RequestedFacilityId)
            {
                linkedTickets = await _dbContext.SupportTickets
                    .Where(t => t.AgreementId == transfer.AgreementId && t.FacilityId != transfer.RequestedFacilityId)
                    .ToListAsync(cancellationToken);
                foreach (var ticket in linkedTickets)
                {
                    ticket.AgreementId = null;
                }
            }

            if (oldAlloc != null || linkedTickets is { Count: > 0 })
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // Step 2: Update Transfer & Agreement FacilityId / snapshots
            transfer.Status = TransferRequestStatusConstants.Completed;
            transfer.CompletedAt = now;

            if (transfer.Agreement != null)
            {
                transfer.Agreement.FacilityId = transfer.RequestedFacilityId;
                if (transfer.DiffDepositAmount > 0)
                {
                    transfer.Agreement.DepositSnapshot += transfer.DiffDepositAmount;
                    transfer.Agreement.DepositBalance += transfer.DiffDepositAmount;
                }
                var targetRate = await _dbContext.FacilityRates
                    .AsNoTracking()
                    .Where(r => r.FacilityId == transfer.RequestedFacilityId && r.UnitTypeId == transfer.RequestedUnitTypeId && r.ValidFrom <= DateOnly.FromDateTime(DateTime.UtcNow) && (r.ValidTo == null || r.ValidTo >= DateOnly.FromDateTime(DateTime.UtcNow)))
                    .OrderByDescending(r => r.ValidFrom)
                    .FirstOrDefaultAsync(cancellationToken);
                if (targetRate != null)
                {
                    transfer.Agreement.MonthlyRateSnapshot = targetRate.MonthlyRate;
                }
                transfer.Agreement.UpdatedAt = now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Step 3: Occupy target unit, insert new UnitAllocation, and re-attach linked tickets
            if (toUnit != null && transfer.Agreement != null)
            {
                toUnit.PhysicalStatus = StorageUnitStatusConstants.Occupied;
                toUnit.UpdatedAt = now;

                var allocStart = DateOnly.FromDateTime(DateTime.UtcNow);
                if (allocStart < transfer.Agreement.StartDate)
                {
                    allocStart = transfer.Agreement.StartDate;
                }
                var allocEnd = transfer.Agreement.EndDate > allocStart ? transfer.Agreement.EndDate : allocStart.AddDays(1);

                _dbContext.UnitAllocations.Add(new UnitAllocation
                {
                    StorageUnitId = toUnit.Id,
                    AgreementId = transfer.AgreementId,
                    AllocationKind = AllocationKindConstants.Rental,
                    AllocationStartDate = allocStart,
                    AllocationEndDate = allocEnd,
                    Status = AllocationStatusConstants.Active,
                    CreatedAt = now
                });

                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            if (linkedTickets is { Count: > 0 })
            {
                foreach (var ticket in linkedTickets)
                {
                    ticket.AgreementId = transfer.AgreementId;
                    ticket.FacilityId = transfer.RequestedFacilityId;
                    if (ticket.StorageUnitId != null)
                    {
                        ticket.StorageUnitId = toUnit?.Id;
                    }
                    ticket.UpdatedAt = now;
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            result = new StaffConfirmQrActionResultDto(
                Success: true,
                ActionType: StaffActionConstants.TransferMoveIn,
                Message: "Đã hoàn tất nhận ô kho tại cơ sở mới khi chuyển kho.",
                AgreementId: transfer.AgreementId,
                AgreementStatus: transfer.Agreement?.Status ?? RentalAgreementStatusConstants.Active,
                UnitId: toUnit?.Id,
                UnitStatus: toUnit?.PhysicalStatus
            );
        }

        // ----------------------------------------------------
        // ACTION 4: MOVE-OUT TRẢ KHO HOÀN TẤT
        // ----------------------------------------------------
        if (action == StaffActionConstants.MoveOut || action == StaffActionConstants.ConfirmMoveOut)
        {
            var moveOutQuery = _dbContext.MoveOutRequests
                .Include(m => m.Agreement)
                .AsQueryable();

            MoveOutRequest? moveOut = null;
            if (moveOutId.HasValue && agreementId.HasValue)
            {
                moveOut = await moveOutQuery.FirstOrDefaultAsync(
                    m => m.Id == moveOutId.Value || m.AgreementId == agreementId.Value, cancellationToken);
            }
            else if (moveOutId.HasValue)
            {
                moveOut = await moveOutQuery.FirstOrDefaultAsync(
                    m => m.Id == moveOutId.Value, cancellationToken);
            }
            else if (agreementId.HasValue)
            {
                moveOut = await moveOutQuery.FirstOrDefaultAsync(
                    m => m.AgreementId == agreementId.Value, cancellationToken);
            }

            if (moveOut == null) throw AppException.FromError(StaffErrors.MoveOutNotFound);

            await ResolveFacilityIdAsync(employeeUserId, moveOut.Agreement.FacilityId, cancellationToken);

            moveOut.Status = MoveOutStatusConstants.Completed;
            moveOut.FinalizedAt = now;
            moveOut.UpdatedAt = now;
            if (moveOut.Agreement.Status == RentalAgreementStatusConstants.MoveOutScheduled)
            {
                moveOut.Agreement.Status = "checkout_pending";
            }
            else
            {
                moveOut.Agreement.Status = RentalAgreementStatusConstants.Terminated;
            }
            moveOut.Agreement.CheckedOutAt = now;
            moveOut.Agreement.ActualEndDate = DateOnly.FromDateTime(DateTime.UtcNow);

            var allocations = await _dbContext.UnitAllocations
                .Include(a => a.StorageUnit)
                .Where(a => a.AgreementId == moveOut.AgreementId && a.Status == AllocationStatusConstants.Active)
                .ToListAsync(cancellationToken);

            foreach (var a in allocations)
            {
                a.Status = AllocationStatusConstants.Released;
                a.EndedAt = now;
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                if (today > a.AllocationStartDate)
                {
                    a.AllocationEndDate = today;
                }
                else if (a.AllocationEndDate <= a.AllocationStartDate)
                {
                    a.AllocationEndDate = a.AllocationStartDate.AddDays(1);
                }

                if (a.StorageUnit.PhysicalStatus != StorageUnitStatusConstants.Cleaning)
                {
                    a.StorageUnit.PhysicalStatus = StorageUnitStatusConstants.Cleaning;
                }
                a.StorageUnit.UpdatedAt = now;

                var cleaningTicket = new SupportTicket
                {
                    TicketNo = TicketConstants.GenerateTicketNo(),
                    CustomerId = moveOut.Agreement.CustomerId,
                    FacilityId = moveOut.Agreement.FacilityId,
                    AgreementId = moveOut.AgreementId,
                    StorageUnitId = a.StorageUnitId,
                    Category = MaintenanceCategoryConstants.Cleaning,
                    Priority = TicketPriorityConstants.Normal,
                    Subject = $"[INTERNAL] Dọn vệ sinh ô kho {a.StorageUnit.UnitCode} sau khi khách trả kho",
                    Description = $"Khách hàng đã hoàn tất trả kho hợp đồng {moveOut.Agreement.AgreementNo}. Nhân viên cần kiểm tra hiện trạng và dọn dẹp sạch sẽ ô kho {a.StorageUnit.UnitCode}.",
                    Status = TicketStatusConstants.Open,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.SupportTickets.Add(cleaningTicket);
            }

            // Đồng bộ kết quả nghiệm thu (Inspection) và kiểm tra hoàn cọc hợp lệ
            var latestInspection = await _dbContext.Inspections
                .Include(i => i.InspectionItems)
                .Where(i => i.AgreementId == moveOut.AgreementId && (i.InspectionType == "move_out" || i.InspectionType == "check_out"))
                .OrderByDescending(i => i.InspectedAt ?? i.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            var originalDeposit = moveOut.Agreement.DepositSnapshot > 0
                ? moveOut.Agreement.DepositSnapshot
                : moveOut.Agreement.DepositBalance;

            decimal finalRefundAmount;
            if (latestInspection != null)
            {
                var totalDamage = latestInspection.InspectionItems?.Sum(item => item.ChargeAmount) ?? 0m;
                finalRefundAmount = totalDamage < originalDeposit ? (originalDeposit - totalDamage) : 0m;
            }
            else
            {
                // Fallback nếu chưa qua bước nghiệm thu hư hại: lấy DepositBalance hiện hành
                finalRefundAmount = moveOut.Agreement.DepositBalance > 0
                    ? moveOut.Agreement.DepositBalance
                    : (moveOut.Agreement.DepositSnapshot > 0 ? moveOut.Agreement.DepositSnapshot : 0m);
            }

            if (finalRefundAmount > 0)
            {
                var existingRefund = await _dbContext.Refunds
                    .FirstOrDefaultAsync(r => r.AgreementId == moveOut.AgreementId, cancellationToken);

                if (existingRefund == null)
                {
                    // Ưu tiên tìm Payment đặt cọc ban đầu hoặc Payment có số tiền lớn nhất (đáp ứng trg_refunds_guard)
                    var agreementPayments = await _dbContext.Payments
                        .Include(p => p.TargetInvoice)
                            .ThenInclude(inv => inv.InvoiceLines)
                        .Where(p => p.Status == PaymentConstants.StatusSucceeded &&
                            (p.TargetInvoice.AgreementId == moveOut.AgreementId || (moveOut.Agreement.ReservationId > 0 && p.TargetInvoice.ReservationId == moveOut.Agreement.ReservationId)))
                        .ToListAsync(cancellationToken);

                    var depositPayment = agreementPayments
                        .Where(p => (moveOut.Agreement.ReservationId > 0 && p.TargetInvoice.ReservationId == moveOut.Agreement.ReservationId) ||
                                    p.TargetInvoice.InvoiceLines.Any(l => l.LineType == InvoiceLineTypeConstants.Deposit))
                        .OrderByDescending(p => p.Amount)
                        .FirstOrDefault();

                    var targetPayment = (depositPayment != null && depositPayment.Amount >= finalRefundAmount)
                        ? depositPayment
                        : (agreementPayments.OrderByDescending(p => p.Amount).FirstOrDefault() ?? depositPayment);

                    var paymentId = targetPayment?.Id ?? 0L;

                    if (paymentId > 0)
                    {
                        var refund = new Refund
                        {
                            PaymentId = paymentId,
                            AgreementId = moveOut.AgreementId,
                            Amount = finalRefundAmount,
                            Currency = PaymentConstants.CurrencyVnd,
                            Reason = $"Tự động tạo yêu cầu hoàn cọc sau khi trả kho hợp đồng {moveOut.Agreement.AgreementNo}" +
                                     (latestInspection != null && finalRefundAmount < originalDeposit ? " (đã khấu trừ chi phí hư hỏng sau nghiệm thu)" : string.Empty),
                            Provider = targetPayment?.Provider ?? PaymentConstants.ProviderManual,
                            IdempotencyKey = $"{PaymentConstants.RefundMoveOutPrefix}{moveOut.AgreementId}-{now.ToUnixTimeSeconds()}",
                            Status = RefundStatusConstants.Pending,
                            RequestedBy = moveOut.Agreement.CustomerId,
                            CreatedAt = now,
                            UpdatedAt = now
                        };
                        _dbContext.Refunds.Add(refund);
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            result = new StaffConfirmQrActionResultDto(
                Success: true,
                ActionType: StaffActionConstants.MoveOut,
                Message: "Đã xác nhận trả kho thành công. Hợp đồng đã hoàn tất, ô kho chuyển sang Cleaning và đã tạo yêu cầu hoàn cọc chờ duyệt.",
                AgreementId: moveOut.AgreementId,
                AgreementStatus: moveOut.Agreement.Status,
                UnitId: allocations.FirstOrDefault()?.StorageUnitId,
                UnitStatus: StorageUnitStatusConstants.Cleaning
            );
        }

        if (result == null)
        {
            throw AppException.FromError(StaffErrors.InvalidStatus);
        }

        if (transaction != null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return result;
    }
    catch
    {
        if (transaction != null)
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        throw;
    }
}
}
