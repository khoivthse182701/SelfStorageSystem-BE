using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Staff;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public class FacilityStaffService : IFacilityStaffService
{
    private readonly SelfStorageDbContext _dbContext;
    private readonly PaymentSettings _paymentSettings;

    public FacilityStaffService(
        SelfStorageDbContext dbContext,
        IOptions<PaymentSettings>? paymentOptions = null)
    {
        _dbContext = dbContext;
        _paymentSettings = paymentOptions?.Value ?? new PaymentSettings();
    }

    // ==========================================
    // 🏢 0. STAFF ASSIGNED FACILITY INFO
    // ==========================================

    public async Task<StaffFacilityInfoDto> GetMyFacilityAsync(long employeeUserId, CancellationToken cancellationToken = default)
    {
        var targetFacilityId = await ResolveFacilityIdAsync(employeeUserId, null, cancellationToken);
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
        var targetFacilityId = await ResolveFacilityIdAsync(employeeUserId, facilityId, cancellationToken);

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
                MapPosition: mapDto
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

    public async Task<StaffWalkInBookingResponse> CreateWalkInBookingAsync(
        long employeeUserId,
        StaffWalkInBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        var unit = await _dbContext.StorageUnits
            .Include(u => u.Facility)
            .Include(u => u.UnitType)
            .FirstOrDefaultAsync(u => u.Id == request.UnitId, cancellationToken);

        if (unit == null)
            throw AppException.FromError(StaffErrors.UnitNotFound);

        if (!string.Equals(unit.PhysicalStatus, StorageUnitStatusConstants.Available, StringComparison.OrdinalIgnoreCase))
            throw AppException.FromError(StaffErrors.UnitNotAvailable);

        var phone = request.CustomerPhone.Trim();
        var suppliedEmail = string.IsNullOrWhiteSpace(request.CustomerEmail)
            ? null
            : request.CustomerEmail.Trim().ToLowerInvariant();
        // Phone is the authoritative identifier at the counter.  Do not let a
        // mistyped email select or overwrite another customer's account.
        var user = await _dbContext.Users
            .Include(u => u.CustomerProfile)
            .FirstOrDefaultAsync(u => u.PhoneNumber == phone, cancellationToken);

        if (user == null && suppliedEmail != null)
        {
            user = await _dbContext.Users
                .Include(u => u.CustomerProfile)
                .FirstOrDefaultAsync(u => u.Email == suppliedEmail, cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;
        var today = request.StartDate;

        if (user == null)
        {
            var email = suppliedEmail ?? $"walkin_{phone}_{Guid.NewGuid():N}"[..24] + "@selfstorage.vn";

            user = new User
            {
                Email = email,
                PhoneNumber = phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
                Status = UserStatusConstants.Active,
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var customerRole = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Code == RoleConstants.Customer, cancellationToken);
            if (customerRole != null)
            {
                _dbContext.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = customerRole.Id,
                    GrantedAt = now
                });
            }

            var customerProfile = new CustomerProfile
            {
                UserId = user.Id,
                FullName = request.CustomerName.Trim(),
                IdentityNumber = request.CustomerIdentityNumber?.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.CustomerProfiles.Add(customerProfile);
            await _dbContext.SaveChangesAsync(cancellationToken);
            user.CustomerProfile = customerProfile;
        }
        else if (user.CustomerProfile == null)
        {
            user.CustomerProfile = new CustomerProfile
            {
                UserId = user.Id,
                FullName = request.CustomerName.Trim(),
                IdentityNumber = request.CustomerIdentityNumber?.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.CustomerProfiles.Add(user.CustomerProfile);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var rate = await _dbContext.FacilityRates
            .Where(fr => fr.FacilityId == unit.FacilityId && fr.UnitTypeId == unit.UnitTypeId && fr.ValidFrom <= today && (!fr.ValidTo.HasValue || fr.ValidTo.Value > today))
            .OrderByDescending(fr => fr.ValidFrom)
            .FirstOrDefaultAsync(cancellationToken);

        var monthlyRent = rate?.MonthlyRate ?? 1000000m;
        var depositAmount = rate?.DepositAmount ?? monthlyRent;
        var totalAmount = monthlyRent + depositAmount;

        var durationMonths = request.DurationMonths > 0 ? request.DurationMonths : 1;
        var endDate = request.StartDate.AddMonths(durationMonths);

        var reservationCode = $"RSV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
        var invoiceNo = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";

        var reservation = new Reservation
        {
            CustomerId = user.Id,
            FacilityId = unit.FacilityId,
            UnitTypeId = unit.UnitTypeId,
            FacilityRateId = rate?.Id ?? 1,
            ReservationCode = reservationCode,
            StartDate = request.StartDate,
            EndDate = endDate,
            MonthlyRateSnapshot = monthlyRent,
            DepositSnapshot = depositAmount,
            BookingFeeSnapshot = 0m,
            DiscountSnapshot = 0m,
            QuotedTotal = (monthlyRent * durationMonths) + depositAmount,
            HoldUntil = now.AddMinutes(60),
            Status = ReservationStatusConstants.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Reservations.Add(reservation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        unit.PhysicalStatus = StorageUnitStatusConstants.Reserved;
        unit.UpdatedAt = now;

        var allocation = new UnitAllocation
        {
            StorageUnitId = unit.Id,
            ReservationId = reservation.Id,
            AllocationKind = AllocationKindConstants.Reservation,
            AllocationStartDate = request.StartDate,
            AllocationEndDate = endDate,
            Status = AllocationStatusConstants.Active,
            CreatedAt = now
        };
        _dbContext.UnitAllocations.Add(allocation);

        var invoice = new Invoice
        {
            CustomerId = user.Id,
            ReservationId = reservation.Id,
            InvoiceNo = invoiceNo,
            IssueDate = today,
            DueDate = today,
            Currency = PaymentConstants.CurrencyVnd,
            SubtotalAmount = totalAmount,
            DiscountAmount = 0m,
            TaxAmount = 0m,
            TotalAmount = totalAmount,
            PaidAmount = 0m,
            Status = InvoiceStatusConstants.Open,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _dbContext.InvoiceLines.Add(new InvoiceLine
        {
            InvoiceId = invoice.Id,
            LineType = InvoiceLineTypeConstants.Rent,
            Description = $"Rental fee for {unit.UnitType.Name} ({unit.UnitCode}) - 1st Month",
            Quantity = 1,
            UnitPrice = monthlyRent,
            LineAmount = monthlyRent,
            Metadata = "{}"
        });

        _dbContext.InvoiceLines.Add(new InvoiceLine
        {
            InvoiceId = invoice.Id,
            LineType = InvoiceLineTypeConstants.Deposit,
            Description = $"Security deposit (1 month) for {unit.UnitCode}",
            Quantity = 1,
            UnitPrice = depositAmount,
            LineAmount = depositAmount,
            Metadata = "{}"
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        var prefix = !string.IsNullOrWhiteSpace(_paymentSettings.SePay?.InvoicePrefix)
            ? _paymentSettings.SePay.InvoicePrefix.Trim()
            : "DH";
        var transferContent = $"{prefix}{invoice.Id}";

        var bankCode = _paymentSettings.VietQr?.BankCode ?? "MBBank";
        var accNo = _paymentSettings.VietQr?.AccountNo ?? "0817495759";
        var accName = _paymentSettings.VietQr?.AccountName ?? "VO THAI HOANG KHOI";
        var template = _paymentSettings.VietQr?.Template ?? "compact";

        var qrImageUrl = $"https://img.vietqr.io/image/{bankCode}-{accNo}-{template}.png?amount={(long)totalAmount}&addInfo={Uri.EscapeDataString(transferContent)}&accountName={Uri.EscapeDataString(accName)}";

        return new StaffWalkInBookingResponse(
            ReservationId: reservation.Id,
            ReservationCode: reservationCode,
            InvoiceId: invoice.Id,
            InvoiceNo: invoiceNo,
            CustomerId: user.Id,
            CustomerName: user.CustomerProfile?.FullName ?? request.CustomerName,
            CustomerPhone: phone,
            UnitId: unit.Id,
            UnitCode: unit.UnitCode,
            FacilityId: unit.FacilityId,
            FacilityName: unit.Facility.Name,
            MonthlyRent: monthlyRent,
            DepositAmount: depositAmount,
            TotalAmount: totalAmount,
            QrImageUrl: qrImageUrl,
            TransferContent: transferContent,
            BankCode: bankCode,
            AccountNo: accNo,
            AccountName: accName,
            Status: "awaiting_payment"
        );
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
                    ActionType: "check_in",
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
                    var customerDto = new StaffCustomerSummaryDto(
                        moveOut.Agreement.CustomerId,
                        moveOut.Agreement.Customer.FullName,
                        moveOut.Agreement.Customer.User?.PhoneNumber,
                        moveOut.Agreement.Customer.User?.Email
                    );

                    return new StaffQrVerifyResponse(
                        ActionType: "move_out",
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
                    var reqFacility = await _dbContext.Facilities.FindAsync(activeTransfer.RequestedFacilityId);
                    return BuildTransferQrResponse(employeeUserId, activeTransfer, reqFacility?.Name ?? "Cơ sở mới");
                }

                // Check if this agreement has an active move-out request
                var activeMoveOut = await _dbContext.MoveOutRequests
                    .Include(m => m.Agreement).ThenInclude(a => a.Customer).ThenInclude(c => c.User)
                    .Include(m => m.Agreement).ThenInclude(a => a.Facility)
                    .FirstOrDefaultAsync(m => m.AgreementId == agreement.Id &&
                        (m.Status == MoveOutStatusConstants.Requested || m.Status == MoveOutStatusConstants.Scheduled || m.Status == MoveOutStatusConstants.InProgress), cancellationToken);

                if (activeMoveOut != null)
                {
                    return new StaffQrVerifyResponse(
                        ActionType: "move_out",
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
                ActionType: "transfer_move_in",
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

        // ----------------------------------------------------
        // ACTION 1: CHECK-IN NHẬN KHO
        // ----------------------------------------------------
        if (action == "check_in" || action == "confirm_checkin")
        {
            RentalAgreement? agreement = null;
            StorageUnit? unit = null;

            if (request.ReservationId.HasValue)
            {
                var reservation = await _dbContext.Reservations
                    .Include(r => r.RentalAgreement)
                    .Include(r => r.UnitAllocation).ThenInclude(a => a!.StorageUnit)
                    .FirstOrDefaultAsync(r => r.Id == request.ReservationId.Value, cancellationToken);

                if (reservation == null) throw AppException.FromError(StaffErrors.ReservationNotFound);

                reservation.Status = ReservationStatusConstants.Converted;
                reservation.UpdatedAt = now;

                unit = reservation.UnitAllocation?.StorageUnit;
                if (unit == null && request.UnitId.HasValue)
                {
                    unit = await _dbContext.StorageUnits.FindAsync(request.UnitId.Value);
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
            else if (request.AgreementId.HasValue)
            {
                agreement = await _dbContext.RentalAgreements
                    .Include(a => a.UnitAllocations.Where(ua => ua.Status == AllocationStatusConstants.Active)).ThenInclude(ua => ua.StorageUnit)
                    .FirstOrDefaultAsync(a => a.Id == request.AgreementId.Value, cancellationToken);

                if (agreement == null) throw AppException.FromError(StaffErrors.AgreementNotFound);
                unit = agreement.UnitAllocations.FirstOrDefault()?.StorageUnit;
            }

            if (agreement == null)
                throw AppException.FromError(StaffErrors.AgreementNotFound);

            agreement.Status = RentalAgreementStatusConstants.Active;
            agreement.CheckedInAt = now;
            agreement.UpdatedAt = now;

            if (unit != null)
            {
                unit.PhysicalStatus = StorageUnitStatusConstants.Occupied;
                unit.UpdatedAt = now;
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
                    DisplayHint = randomPin,
                    IssuedAt = now,
                    ExpiresAt = now.AddYears(1),
                    Status = CredentialStatusConstants.Active,
                    CreatedAt = now
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return new StaffConfirmQrActionResultDto(
                Success: true,
                ActionType: "check_in",
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
        if (action == "transfer_move_out" || action == "confirm_transfer_moveout" || action == "transfer_out")
        {
            var transfer = await _dbContext.UnitTransferRequests
                .Include(t => t.FromUnit)
                .Include(t => t.Agreement)
                .FirstOrDefaultAsync(t => t.Id == (request.TransferRequestId ?? 0) || t.AgreementId == (request.AgreementId ?? 0), cancellationToken);

            if (transfer == null) throw AppException.FromError(StaffErrors.TransferNotFound);
            if (transfer.Status != TransferRequestStatusConstants.Paid && transfer.Status != TransferRequestStatusConstants.Scheduled)
                throw AppException.FromError(StaffErrors.TransferNotReady);

            transfer.Status = TransferRequestStatusConstants.MovedOutInTransit;

            // Free origin unit and create cleaning ticket
            var fromUnit = await _dbContext.StorageUnits.FindAsync(transfer.FromUnitId);
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
                    Category = "cleaning",
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
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return new StaffConfirmQrActionResultDto(
                Success: true,
                ActionType: "transfer_move_out",
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
        if (action == "transfer_move_in" || action == "confirm_transfer_movein" || action == "transfer_in" || action == "transfer_check_in")
        {
            var transfer = await _dbContext.UnitTransferRequests
                .Include(t => t.ToUnit)
                .Include(t => t.Agreement)
                .FirstOrDefaultAsync(t => t.Id == (request.TransferRequestId ?? 0) || t.AgreementId == (request.AgreementId ?? 0), cancellationToken);

            if (transfer == null) throw AppException.FromError(StaffErrors.TransferNotFound);
            if (transfer.Status != TransferRequestStatusConstants.MovedOutInTransit)
                throw AppException.FromError(StaffErrors.TransferNotReady);

            transfer.Status = TransferRequestStatusConstants.Completed;
            transfer.CompletedAt = now;

            StorageUnit? toUnit = transfer.ToUnit;
            if (toUnit == null && request.UnitId.HasValue)
            {
                toUnit = await _dbContext.StorageUnits.FindAsync(request.UnitId.Value);
                transfer.ToUnitId = toUnit?.Id;
            }

            if (toUnit != null)
            {
                toUnit.PhysicalStatus = StorageUnitStatusConstants.Occupied;
                toUnit.UpdatedAt = now;

                _dbContext.UnitAllocations.Add(new UnitAllocation
                {
                    StorageUnitId = toUnit.Id,
                    AgreementId = transfer.AgreementId,
                    AllocationKind = AllocationKindConstants.Rental,
                    AllocationStartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    AllocationEndDate = transfer.Agreement.EndDate,
                    Status = AllocationStatusConstants.Active,
                    CreatedAt = now
                });
            }

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

            await _dbContext.SaveChangesAsync(cancellationToken);

            return new StaffConfirmQrActionResultDto(
                Success: true,
                ActionType: "transfer_move_in",
                Message: "Đã hoàn tất nhận ô kho tại cơ sở mới khi chuyển kho.",
                AgreementId: transfer.AgreementId,
                AgreementStatus: transfer.Agreement.Status,
                UnitId: toUnit?.Id,
                UnitStatus: toUnit?.PhysicalStatus
            );
        }

        // ----------------------------------------------------
        // ACTION 4: MOVE-OUT TRẢ KHO HOÀN TẤT
        // ----------------------------------------------------
        if (action == "move_out" || action == "confirm_moveout")
        {
            var moveOut = await _dbContext.MoveOutRequests
                .Include(m => m.Agreement)
                .FirstOrDefaultAsync(m => m.Id == (request.MoveOutId ?? 0) || m.AgreementId == (request.AgreementId ?? 0), cancellationToken);

            if (moveOut != null)
            {
                moveOut.Status = MoveOutStatusConstants.Completed;
                moveOut.Agreement.Status = RentalAgreementStatusConstants.Terminated;
                moveOut.Agreement.CheckedOutAt = now;
                moveOut.Agreement.ActualEndDate = DateOnly.FromDateTime(DateTime.UtcNow);

                var allocations = await _dbContext.UnitAllocations
                    .Include(a => a.StorageUnit)
                    .Where(a => a.AgreementId == moveOut.AgreementId && a.Status == AllocationStatusConstants.Active)
                    .ToListAsync(cancellationToken);

                foreach (var a in allocations)
                {
                    a.Status = AllocationStatusConstants.Released;
                    a.StorageUnit.PhysicalStatus = StorageUnitStatusConstants.Maintenance;
                    a.StorageUnit.UpdatedAt = now;

                    var cleaningTicket = new SupportTicket
                    {
                        TicketNo = TicketConstants.GenerateTicketNo(),
                        CustomerId = moveOut.Agreement.CustomerId,
                        FacilityId = moveOut.Agreement.FacilityId,
                        AgreementId = moveOut.AgreementId,
                        StorageUnitId = a.StorageUnitId,
                        Category = "cleaning",
                        Priority = TicketPriorityConstants.Normal,
                        Subject = $"Dọn vệ sinh ô kho {a.StorageUnit.UnitCode} sau khi khách trả kho",
                        Description = $"Khách hàng đã hoàn tất trả kho hợp đồng {moveOut.Agreement.AgreementNo}. Nhân viên cần kiểm tra hiện trạng và dọn dẹp sạch sẽ ô kho {a.StorageUnit.UnitCode}.",
                        Status = TicketStatusConstants.Open,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _dbContext.SupportTickets.Add(cleaningTicket);
                }

                // Tự động tạo bản ghi Refund trong [operations].[refunds] để Manager duyệt hoàn cọc
                var depositAmount = moveOut.Agreement.DepositSnapshot > 0 
                    ? moveOut.Agreement.DepositSnapshot 
                    : moveOut.Agreement.DepositBalance;

                if (depositAmount > 0)
                {
                    var existingRefund = await _dbContext.Refunds
                        .FirstOrDefaultAsync(r => r.AgreementId == moveOut.AgreementId, cancellationToken);

                    if (existingRefund == null)
                    {
                        var lastPayment = await _dbContext.Payments
                            .Where(p => p.CustomerId == moveOut.Agreement.CustomerId && p.Status == PaymentConstants.StatusSucceeded)
                            .OrderByDescending(p => p.CreatedAt)
                            .FirstOrDefaultAsync(cancellationToken);

                        var paymentId = lastPayment?.Id ?? 1L;
                        var refund = new Refund
                        {
                            PaymentId = paymentId,
                            AgreementId = moveOut.AgreementId,
                            Amount = depositAmount,
                            Currency = "VND",
                            Reason = $"Tự động tạo yêu cầu hoàn cọc sau khi trả kho hợp đồng {moveOut.Agreement.AgreementNo}",
                            Provider = lastPayment?.Provider ?? "manual",
                            IdempotencyKey = $"REF-MOVEOUT-{moveOut.AgreementId}-{now.ToUnixTimeSeconds()}",
                            Status = RefundStatusConstants.Pending,
                            RequestedBy = moveOut.Agreement.CustomerId,
                            CreatedAt = now,
                            UpdatedAt = now
                        };
                        _dbContext.Refunds.Add(refund);
                    }
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                return new StaffConfirmQrActionResultDto(
                    Success: true,
                    ActionType: "move_out",
                    Message: "Đã xác nhận trả kho thành công. Hợp đồng đã hoàn tất, ô kho chuyển sang Cleaning và đã tạo yêu cầu hoàn cọc chờ duyệt.",
                    AgreementId: moveOut.AgreementId,
                    AgreementStatus: moveOut.Agreement.Status,
                    UnitId: allocations.FirstOrDefault()?.StorageUnitId,
                    UnitStatus: StorageUnitStatusConstants.Maintenance
                );
            }
        }

        throw AppException.FromError(StaffErrors.InvalidStatus);
    }

    // ==========================================
    // 🛠️ NHÓM 4: HỖ TRỢ TICKET VỀ LOGISTICS (Dọn dẹp, Sửa chữa, Kiểm tra)
    // ==========================================

    public async Task<IReadOnlyList<StaffLogisticsTicketDto>> GetLogisticsTicketsAsync(
        long employeeUserId,
        long? facilityId = null,
        string? category = null,
        string? status = null,
        bool? onlyAssignedToMe = null,
        CancellationToken cancellationToken = default)
    {
        var targetFacilityId = await ResolveFacilityIdAsync(employeeUserId, facilityId, cancellationToken);

        var query = _dbContext.SupportTickets
            .AsNoTracking()
            .Include(t => t.Facility)
            .Include(t => t.Customer)
            .Include(t => t.StorageUnit)
            .Include(t => t.Agreement)
            .Include(t => t.TicketAssignment).ThenInclude(ta => ta!.Employee)
            .Include(t => t.TicketChargeProposals)
            .Where(t => t.FacilityId == targetFacilityId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim().ToLowerInvariant();
            query = query.Where(t => t.Category.ToLower() == cat);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim().ToLowerInvariant();
            query = query.Where(t => t.Status.ToLower() == st);
        }

        if (onlyAssignedToMe == true)
        {
            var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);
            query = query.Where(t => t.TicketAssignment != null && t.TicketAssignment.EmployeeId == employee.UserId);
        }

        var list = await query.OrderByDescending(t => t.CreatedAt).ToListAsync(cancellationToken);

        return list.Select(t => new StaffLogisticsTicketDto(
            Id: t.Id,
            TicketNo: t.TicketNo,
            FacilityId: t.FacilityId,
            FacilityName: t.Facility.Name,
            StorageUnitId: t.StorageUnitId,
            UnitCode: t.StorageUnit?.UnitCode,
            CustomerId: t.CustomerId,
            CustomerName: t.Customer.FullName,
            AgreementId: t.AgreementId,
            AgreementNo: t.Agreement?.AgreementNo,
            Category: t.Category,
            Priority: t.Priority,
            Subject: t.Subject,
            Description: t.Description,
            Status: t.Status,
            AssignedEmployeeId: t.TicketAssignment?.EmployeeId,
            AssignedEmployeeName: t.TicketAssignment?.Employee?.FullName,
            Resolution: t.Resolution,
            ResolvedAt: t.ResolvedAt,
            CreatedAt: t.CreatedAt,
            UpdatedAt: t.UpdatedAt,
            HasProposedCharges: t.TicketChargeProposals.Any()
        )).ToList();
    }

    public async Task AssignTicketToStaffAsync(
        long employeeUserId,
        long ticketId,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);

        var ticket = await _dbContext.SupportTickets
            .Include(t => t.TicketAssignment)
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket == null)
            throw AppException.FromError(StaffErrors.TicketNotFound);

        var now = DateTimeOffset.UtcNow;
        if (ticket.TicketAssignment == null)
        {
            _dbContext.TicketAssignments.Add(new TicketAssignment
            {
                TicketId = ticket.Id,
                EmployeeId = employee.UserId,
                AssignedAt = now
            });
        }
        else
        {
            ticket.TicketAssignment.EmployeeId = employee.UserId;
            ticket.TicketAssignment.AssignedAt = now;
        }

        ticket.Status = TicketStatusConstants.InProgress;
        ticket.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TicketChargeProposalDto> ProposeTicketChargeAsync(
        long employeeUserId,
        long ticketId,
        StaffProposeChargeRequest request,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);

        var ticket = await _dbContext.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket == null)
            throw AppException.FromError(StaffErrors.TicketNotFound);

        var now = DateTimeOffset.UtcNow;
        var proposal = new TicketChargeProposal
        {
            TicketId = ticket.Id,
            ProposedBy = employee.UserId,
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? request.Reason
                : $"{request.Reason}: {request.Description}",
            Amount = request.Amount,
            Status = "pending",
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.TicketChargeProposals.Add(proposal);

        if (!string.IsNullOrWhiteSpace(request.PhotoUrl))
        {
            _dbContext.TicketAttachments.Add(new TicketAttachment
            {
                TicketId = ticket.Id,
                UploadedBy = employee.UserId,
                FileName = "damage_photo.jpg",
                ObjectUrl = request.PhotoUrl,
                MimeType = "image/jpeg",
                FileSizeBytes = 1024,
                CreatedAt = now
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TicketChargeProposalDto(
            Id: proposal.Id,
            TicketId: proposal.TicketId,
            ProposedBy: employee.UserId,
            ProposedByName: employee.FullName,
            Description: proposal.Description,
            Amount: proposal.Amount,
            Status: proposal.Status,
            CreatedAt: proposal.CreatedAt
        );
    }

    public async Task<StaffCompleteTicketResultDto> CompleteTicketAsync(
        long employeeUserId,
        long ticketId,
        StaffCompleteTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _dbContext.SupportTickets
            .Include(t => t.StorageUnit)
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket == null)
            throw AppException.FromError(StaffErrors.TicketNotFound);

        var now = DateTimeOffset.UtcNow;
        ticket.Status = TicketStatusConstants.Resolved;
        ticket.Resolution = request.Resolution ?? "Hoàn tất xử lý công việc bởi nhân viên.";
        ticket.ResolvedAt = now;
        ticket.UpdatedAt = now;

        long? unlockedUnitId = null;
        string? unlockedUnitCode = null;
        string? unlockedUnitStatus = null;

        // Quy tắc nghiệp vụ đặc biệt:
        // Nếu đây là ticket "Dọn vệ sinh/Sửa chữa ô kho", khi Staff bấm complete, hệ thống tự động mở khóa ô kho đó từ Maintenance/Cleaning về lại Available để bán tiếp!
        if (ticket.StorageUnitId.HasValue && ticket.StorageUnit != null)
        {
            var unit = ticket.StorageUnit;
            var isMaintenanceOrCleaning =
                ticket.Category.Contains("clean", StringComparison.OrdinalIgnoreCase) ||
                ticket.Category.Contains("maint", StringComparison.OrdinalIgnoreCase) ||
                ticket.Category.Contains("repair", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(unit.PhysicalStatus, StorageUnitStatusConstants.Maintenance, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(unit.PhysicalStatus, "cleaning", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(unit.PhysicalStatus, "under_maintenance", StringComparison.OrdinalIgnoreCase);

            if (isMaintenanceOrCleaning)
            {
                var oldStatus = unit.PhysicalStatus;
                unit.PhysicalStatus = StorageUnitStatusConstants.Available;
                unit.IsListed = true;
                unit.UpdatedAt = now;

                _dbContext.UnitStatusHistories.Add(new UnitStatusHistory
                {
                    StorageUnitId = unit.Id,
                    OldStatus = oldStatus,
                    NewStatus = StorageUnitStatusConstants.Available,
                    Reason = $"Hoàn tất ticket {ticket.TicketNo}: {ticket.Resolution}",
                    ChangedBy = employeeUserId,
                    ChangedAt = now
                });

                unlockedUnitId = unit.Id;
                unlockedUnitCode = unit.UnitCode;
                unlockedUnitStatus = StorageUnitStatusConstants.Available;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new StaffCompleteTicketResultDto(
            TicketId: ticket.Id,
            TicketNo: ticket.TicketNo,
            Status: ticket.Status,
            Resolution: ticket.Resolution,
            ResolvedAt: ticket.ResolvedAt.Value,
            UnlockedUnitId: unlockedUnitId,
            UnlockedUnitCode: unlockedUnitCode,
            UnlockedUnitStatus: unlockedUnitStatus
        );
    }

    // ==========================================
    // 🗓️ NHÓM 5: CA TRỰC & LỊCH LÀM VIỆC (Shifts Schedule)
    // ==========================================

    public async Task<IReadOnlyList<StaffShiftScheduleDto>> GetStaffShiftsAsync(
        long employeeUserId,
        long? facilityId = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);

        var query = _dbContext.ShiftAssignments
            .AsNoTracking()
            .Include(sa => sa.Shift)
                .ThenInclude(s => s.Facility)
            .Include(sa => sa.Employee)
            .Where(sa => sa.EmployeeId == employee.UserId)
            .AsQueryable();

        if (facilityId.HasValue)
        {
            query = query.Where(sa => sa.Shift.FacilityId == facilityId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(sa => sa.Shift.EndsAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(sa => sa.Shift.StartsAt <= toDate.Value);
        }

        var assignments = await query
            .OrderBy(sa => sa.Shift.StartsAt)
            .ToListAsync(cancellationToken);

        return assignments.Select(sa => new StaffShiftScheduleDto(
            ShiftId: sa.ShiftId,
            FacilityId: sa.Shift.FacilityId,
            FacilityName: sa.Shift.Facility.Name,
            ShiftName: sa.Shift.ShiftName,
            StartsAt: sa.Shift.StartsAt,
            EndsAt: sa.Shift.EndsAt,
            EmployeeId: sa.EmployeeId,
            EmployeeCode: sa.Employee.EmployeeCode,
            EmployeeName: sa.Employee.FullName,
            DutyRole: sa.DutyRole,
            Status: sa.Status,
            CheckInAt: sa.CheckInAt,
            CheckOutAt: sa.CheckOutAt
        )).ToList();
    }

    public async Task<StaffShiftScheduleDto> CheckInShiftAsync(long employeeUserId, long shiftId, CancellationToken cancellationToken = default)
    {
        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);
        var assignment = await _dbContext.ShiftAssignments
            .Include(sa => sa.Shift).ThenInclude(s => s.Facility)
            .Include(sa => sa.Employee)
            .FirstOrDefaultAsync(sa => sa.ShiftId == shiftId && sa.EmployeeId == employee.UserId, cancellationToken);

        if (assignment == null)
            throw AppException.FromError(StaffErrors.ShiftAssignmentNotFound);

        assignment.CheckInAt = DateTimeOffset.UtcNow;
        assignment.Status = "checked_in";
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new StaffShiftScheduleDto(
            assignment.ShiftId, assignment.Shift.FacilityId, assignment.Shift.Facility.Name, assignment.Shift.ShiftName,
            assignment.Shift.StartsAt, assignment.Shift.EndsAt, assignment.EmployeeId, assignment.Employee.EmployeeCode,
            assignment.Employee.FullName, assignment.DutyRole, assignment.Status, assignment.CheckInAt, assignment.CheckOutAt
        );
    }

    public async Task<StaffShiftScheduleDto> CheckOutShiftAsync(long employeeUserId, long shiftId, CancellationToken cancellationToken = default)
    {
        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);
        var assignment = await _dbContext.ShiftAssignments
            .Include(sa => sa.Shift).ThenInclude(s => s.Facility)
            .Include(sa => sa.Employee)
            .FirstOrDefaultAsync(sa => sa.ShiftId == shiftId && sa.EmployeeId == employee.UserId, cancellationToken);

        if (assignment == null)
            throw AppException.FromError(StaffErrors.ShiftAssignmentNotFound);

        assignment.CheckOutAt = DateTimeOffset.UtcNow;
        assignment.Status = "completed";
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new StaffShiftScheduleDto(
            assignment.ShiftId, assignment.Shift.FacilityId, assignment.Shift.Facility.Name, assignment.Shift.ShiftName,
            assignment.Shift.StartsAt, assignment.Shift.EndsAt, assignment.EmployeeId, assignment.Employee.EmployeeCode,
            assignment.Employee.FullName, assignment.DutyRole, assignment.Status, assignment.CheckInAt, assignment.CheckOutAt
        );
    }

    // ==========================================
    // BACKWARD-COMPATIBILITY / LEGACY METHODS
    // ==========================================

    public async Task<IReadOnlyList<StaffTaskDto>> GetStaffTasksAsync(long employeeUserId, long? facilityId = null, CancellationToken cancellationToken = default)
    {
        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);
        var query = _dbContext.StaffTasks.AsNoTracking().Include(t => t.Facility).Include(t => t.AssignedEmployee).AsQueryable();
        if (facilityId.HasValue) query = query.Where(t => t.FacilityId == facilityId.Value);
        else query = query.Where(t => t.AssignedEmployeeId == employee.UserId);

        var tasks = await query.OrderByDescending(t => t.CreatedAt).ToListAsync(cancellationToken);
        return tasks.Select(t => new StaffTaskDto(t.Id, t.FacilityId, t.Facility.Code, t.AssignedEmployeeId, t.AssignedEmployee?.FullName, t.TaskType, t.Title, t.DueAt, t.Status, t.ProgressPercent, t.CreatedAt, t.UpdatedAt)).ToList();
    }

    public async Task<StaffTaskDto> UpdateStaffTaskStatusAsync(long employeeUserId, long taskId, UpdateStaffTaskStatusRequest request, CancellationToken cancellationToken = default)
    {
        var task = await _dbContext.StaffTasks.Include(t => t.Facility).Include(t => t.AssignedEmployee).FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task == null) throw AppException.FromError(StaffErrors.TaskNotFound);
        task.Status = request.Status;
        if (request.ProgressPercent.HasValue) task.ProgressPercent = request.ProgressPercent.Value;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new StaffTaskDto(task.Id, task.FacilityId, task.Facility.Code, task.AssignedEmployeeId, task.AssignedEmployee?.FullName, task.TaskType, task.Title, task.DueAt, task.Status, task.ProgressPercent, task.CreatedAt, task.UpdatedAt);
    }

    public async Task<IReadOnlyList<StaffReservationLookupDto>> LookupReservationsAsync(string? query, long? facilityId = null, CancellationToken cancellationToken = default)
    {
        var q = _dbContext.Reservations.AsNoTracking().Include(r => r.Customer).Include(r => r.Facility).Include(r => r.UnitType).Include(r => r.UnitAllocation).ThenInclude(a => a!.StorageUnit).AsQueryable();
        if (facilityId.HasValue) q = q.Where(r => r.FacilityId == facilityId.Value);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLowerInvariant();
            q = q.Where(r => r.ReservationCode.ToLower().Contains(term) || r.Customer.FullName.ToLower().Contains(term));
        }
        var list = await q.OrderByDescending(r => r.CreatedAt).Take(50).ToListAsync(cancellationToken);
        return list.Select(r => new StaffReservationLookupDto(r.Id, r.ReservationCode, r.CustomerId, r.Customer.FullName, null, r.FacilityId, r.Facility.Code, r.UnitTypeId, r.UnitType.Name, r.UnitAllocation?.StorageUnitId, r.UnitAllocation?.StorageUnit?.UnitCode, r.StartDate, r.EndDate, r.Status, r.QuotedTotal, r.DepositSnapshot, r.CreatedAt)).ToList();
    }

    public async Task AssignUnitToReservationAsync(long employeeUserId, long reservationId, AssignUnitToReservationRequest request, CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations.Include(r => r.UnitAllocation).FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);
        if (reservation == null) throw AppException.FromError(StaffErrors.ReservationNotFound);
        var unit = await _dbContext.StorageUnits.FirstOrDefaultAsync(u => u.Id == request.StorageUnitId, cancellationToken);
        if (unit == null) throw AppException.FromError(StaffErrors.UnitNotFound);
        if (!string.Equals(unit.PhysicalStatus, StorageUnitStatusConstants.Available, StringComparison.OrdinalIgnoreCase))
            throw AppException.FromError(StaffErrors.UnitNotAvailable);

        if (reservation.UnitAllocation != null)
        {
            var oldUnit = await _dbContext.StorageUnits.FindAsync(reservation.UnitAllocation.StorageUnitId);
            if (oldUnit != null) oldUnit.PhysicalStatus = StorageUnitStatusConstants.Available;
            reservation.UnitAllocation.StorageUnitId = unit.Id;
        }
        else
        {
            _dbContext.UnitAllocations.Add(new UnitAllocation
            {
                StorageUnitId = unit.Id,
                ReservationId = reservation.Id,
                AllocationKind = AllocationKindConstants.Reservation,
                AllocationStartDate = reservation.StartDate,
                AllocationEndDate = reservation.EndDate,
                Status = AllocationStatusConstants.Active,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        unit.PhysicalStatus = StorageUnitStatusConstants.Reserved;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<StaffHandoverResultDto> CreateHandoverAsync(long employeeUserId, CreateStaffHandoverRequest request, CancellationToken cancellationToken = default)
    {
        var res = await ConfirmQrActionAsync(employeeUserId, new StaffConfirmQrActionRequest("check_in", ReservationId: request.ReservationId, Notes: request.Notes), cancellationToken);
        var rsv = await _dbContext.Reservations.Include(r => r.UnitAllocation).ThenInclude(a => a!.StorageUnit).FirstOrDefaultAsync(r => r.Id == request.ReservationId, cancellationToken);
        return new StaffHandoverResultDto(1, res.AgreementId ?? 1, "AGR", request.ReservationId, rsv?.UnitAllocation?.StorageUnitId ?? 1, rsv?.UnitAllocation?.StorageUnit?.UnitCode ?? "U-101", request.HandoverType, RentalAgreementStatusConstants.Active, DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<StaffMoveOutSummaryDto>> GetMoveOutsAsync(long? facilityId = null, DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        var q = _dbContext.MoveOutRequests.AsNoTracking().Include(m => m.Agreement).ThenInclude(a => a.Customer).Include(m => m.Agreement).ThenInclude(a => a.Facility).AsQueryable();
        if (facilityId.HasValue) q = q.Where(m => m.Agreement.FacilityId == facilityId.Value);
        if (date.HasValue) q = q.Where(m => m.RequestedMoveOutDate == date.Value);
        var list = await q.OrderByDescending(m => m.RequestedMoveOutDate).ToListAsync(cancellationToken);
        return list.Select(m => new StaffMoveOutSummaryDto(m.Id, m.AgreementId, m.Agreement.AgreementNo, m.Agreement.CustomerId, m.Agreement.Customer.FullName, m.Agreement.FacilityId, "N/A", m.RequestedMoveOutDate, m.Status, m.Reason, m.CreatedAt)).ToList();
    }

    public async Task<InspectMoveOutResultDto> InspectMoveOutAsync(long employeeUserId, long moveOutId, InspectMoveOutRequest request, CancellationToken cancellationToken = default)
    {
        var res = await ConfirmQrActionAsync(employeeUserId, new StaffConfirmQrActionRequest("move_out", MoveOutId: moveOutId), cancellationToken);
        return new InspectMoveOutResultDto(moveOutId, 1, res.AgreementId ?? 1, 0m, RentalAgreementStatusConstants.Completed, StorageUnitStatusConstants.Available, DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<StaffSupportTicketDto>> GetFacilityTicketsAsync(long? facilityId = null, string? status = null, CancellationToken cancellationToken = default)
    {
        var q = _dbContext.SupportTickets.AsNoTracking().Include(t => t.Facility).Include(t => t.Customer).Include(t => t.StorageUnit).Include(t => t.Agreement).AsQueryable();
        if (facilityId.HasValue) q = q.Where(t => t.FacilityId == facilityId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(t => t.Status == status);
        var list = await q.OrderByDescending(t => t.CreatedAt).ToListAsync(cancellationToken);
        return list.Select(t => new StaffSupportTicketDto(t.Id, t.TicketNo, t.FacilityId, t.Facility.Code, t.CustomerId, t.Customer.FullName, t.AgreementId, t.Agreement?.AgreementNo, t.StorageUnitId, t.StorageUnit?.UnitCode, t.Category, t.Priority, t.Subject, t.Description, t.Status, t.CreatedAt, t.UpdatedAt)).ToList();
    }

    public Task AssignTicketToMeAsync(long employeeUserId, long ticketId, CancellationToken cancellationToken = default)
        => AssignTicketToStaffAsync(employeeUserId, ticketId, cancellationToken);

    public async Task<StaffTicketMessageDto> AddStaffTicketMessageAsync(long employeeUserId, long ticketId, AddStaffTicketMessageRequest request, CancellationToken cancellationToken = default)
    {
        var emp = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);
        var msg = new TicketMessage { TicketId = ticketId, AuthorUserId = employeeUserId, Body = request.Body, IsInternal = request.IsInternal, CreatedAt = DateTimeOffset.UtcNow };
        _dbContext.TicketMessages.Add(msg);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new StaffTicketMessageDto(msg.Id, ticketId, employeeUserId, emp.FullName, "Staff", request.Body, request.IsInternal, msg.CreatedAt);
    }

    public Task<TicketChargeProposalDto> ProposeTicketChargeAsync(long employeeUserId, long ticketId, ProposeTicketChargeRequest request, CancellationToken cancellationToken = default)
        => ProposeTicketChargeAsync(employeeUserId, ticketId, new StaffProposeChargeRequest(request.Amount, request.Description), cancellationToken);

    public async Task ResolveTicketAsync(long employeeUserId, long ticketId, ResolveTicketRequest request, CancellationToken cancellationToken = default)
        => await CompleteTicketAsync(employeeUserId, ticketId, new StaffCompleteTicketRequest(request.Resolution), cancellationToken);

    public async Task<IReadOnlyList<StaffFacilityUnitDto>> GetFacilityUnitsAsync(long facilityId, CancellationToken cancellationToken = default)
    {
        var units = await GetUnitsAsync(1, facilityId, cancellationToken: cancellationToken);
        return units.Select(u => new StaffFacilityUnitDto(u.UnitId, u.UnitCode, u.UnitTypeId, u.UnitTypeName, u.PhysicalStatus, u.CurrentRate, u.CurrentAgreementNo, u.CustomerName)).ToList();
    }

    public async Task UpdateUnitStatusAsync(long employeeUserId, long unitId, UpdateUnitStatusRequest request, CancellationToken cancellationToken = default)
    {
        var unit = await _dbContext.StorageUnits.FirstOrDefaultAsync(u => u.Id == unitId, cancellationToken);
        if (unit == null) throw AppException.FromError(StaffErrors.UnitNotFound);
        unit.PhysicalStatus = request.Status;
        unit.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<MaintenanceWorkOrderDto> CreateMaintenanceOrderAsync(long employeeUserId, long unitId, CreateMaintenanceWorkOrderRequest request, CancellationToken cancellationToken = default)
    {
        var unit = await _dbContext.StorageUnits.Include(u => u.Facility).FirstOrDefaultAsync(u => u.Id == unitId, cancellationToken);
        if (unit == null) throw AppException.FromError(StaffErrors.UnitNotFound);
        var order = new MaintenanceWorkOrder
        {
            WorkOrderNo = $"WO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
            FacilityId = unit.FacilityId,
            StorageUnitId = unit.Id,
            OpenedBy = employeeUserId,
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            BlocksBooking = request.BlocksBooking,
            Status = "pending",
            EstimatedCost = request.EstimatedCost,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _dbContext.MaintenanceWorkOrders.Add(order);
        if (request.BlocksBooking) unit.PhysicalStatus = StorageUnitStatusConstants.Maintenance;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new MaintenanceWorkOrderDto(order.Id, order.WorkOrderNo, order.FacilityId, order.StorageUnitId, unit.UnitCode, order.Title, order.Description, order.Priority, order.BlocksBooking, order.Status, order.EstimatedCost, order.CreatedAt);
    }

    public async Task<IReadOnlyList<OverdueAgreementDto>> GetOverdueAgreementsAsync(long? facilityId = null, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(new List<OverdueAgreementDto>());
    }

    public async Task LockAgreementAccessAsync(long employeeUserId, long agreementId, LockAccessRequest request, CancellationToken cancellationToken = default)
    {
        var creds = await _dbContext.AccessCredentials.Where(c => c.AgreementId == agreementId).ToListAsync(cancellationToken);
        foreach (var c in creds) c.Status = CredentialStatusConstants.Suspended;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // ==========================================
    // HELPERS
    // ==========================================

    private async Task<EmployeeProfile> GetEmployeeProfileAsync(long userId, CancellationToken cancellationToken)
    {
        var employee = await _dbContext.EmployeeProfiles
            .FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);

        if (employee == null)
            throw AppException.FromError(StaffErrors.EmployeeProfileNotFound);

        return employee;
    }

    private async Task<long> ResolveFacilityIdAsync(long employeeUserId, long? facilityId, CancellationToken cancellationToken)
    {
        if (facilityId.HasValue) return facilityId.Value;

        var now = DateTimeOffset.UtcNow;
        var assignedFacilityId = await _dbContext.StaffFacilityAssignments
            .Where(s => s.Employee.UserId == employeeUserId && (!s.EndsAt.HasValue || s.EndsAt > now))
            .Select(s => (long?)s.FacilityId)
            .FirstOrDefaultAsync(cancellationToken);

        if (assignedFacilityId.HasValue) return assignedFacilityId.Value;

        throw AppException.FromError(ManagerErrors.EmployeeNotAssignedToFacility);
    }
}
