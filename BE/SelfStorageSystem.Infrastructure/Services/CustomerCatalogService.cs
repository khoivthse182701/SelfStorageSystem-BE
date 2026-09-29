using Microsoft.EntityFrameworkCore;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Customer.Facilities;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public sealed class CustomerCatalogService(SelfStorageDbContext db) : ICustomerCatalogService
{
    public async Task<IReadOnlyList<FacilitySummaryDto>> GetFacilitiesAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var facilities = await db.Facilities
            .AsNoTracking()
            .Where(f => f.Status == FacilityStatusConstants.Active)
            .ToListAsync(ct);

        var facilityIds = facilities.Select(f => f.Id).ToList();

        var unitRows = await ListedUnits()
            .Where(u => facilityIds.Contains(u.FacilityId))
            .Select(u => new
            {
                u.FacilityId,
                u.PhysicalStatus,
                Holds = u.UnitAllocations.Any(a =>
                    a.Status == AllocationStatusConstants.Active &&
                    a.ReservationId != null &&
                    a.Reservation!.Status == ReservationStatusConstants.Pending &&
                    a.Reservation.HoldUntil > now),
                ExpiredHold = u.UnitAllocations.Any(a =>
                    a.Status == AllocationStatusConstants.Active &&
                    a.ReservationId != null &&
                    a.Reservation!.Status == ReservationStatusConstants.Pending &&
                    a.Reservation.HoldUntil <= now)
            })
            .ToListAsync(ct);

        return facilities.Select(f => new FacilitySummaryDto
        {
            Id = f.Id,
            Code = f.Code,
            Name = f.Name,
            Address = f.AddressLine,
            City = f.City,
            Latitude = f.Latitude,
            Longitude = f.Longitude,
            OpeningTime = f.OpeningTime,
            ClosingTime = f.ClosingTime,
            AvailableUnitCount = unitRows.Count(u => u.FacilityId == f.Id &&
                (u.PhysicalStatus == StorageUnitStatusConstants.Available ||
                 (u.PhysicalStatus == StorageUnitStatusConstants.Reserved && u.ExpiredHold && !u.Holds)))
        }).ToList();
    }

    public async Task<FacilityDetailDto?> GetFacilityAsync(long id, CancellationToken ct = default)
    {
        var f = await db.Facilities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.Status == FacilityStatusConstants.Active, ct);

        if (f is null) return null;

        var now = DateTimeOffset.UtcNow;

        var units = await ListedUnits()
            .Where(u => u.FacilityId == id)
            .Select(u => new
            {
                u.UnitTypeId,
                u.PhysicalStatus,
                u.UnitType,
                Holds = u.UnitAllocations.Any(a =>
                    a.Status == AllocationStatusConstants.Active &&
                    a.ReservationId != null &&
                    a.Reservation!.Status == ReservationStatusConstants.Pending &&
                    a.Reservation.HoldUntil > now),
                ExpiredHold = u.UnitAllocations.Any(a =>
                    a.Status == AllocationStatusConstants.Active &&
                    a.ReservationId != null &&
                    a.Reservation!.Status == ReservationStatusConstants.Pending &&
                    a.Reservation.HoldUntil <= now)
            })
            .ToListAsync(ct);

        var rates = await ActiveRates(id, DateOnly.FromDateTime(DateTime.UtcNow)).ToListAsync(ct);

        var types = units.GroupBy(x => x.UnitTypeId).Select(g =>
        {
            var t = g.First().UnitType;
            var availableCount = g.Count(x =>
                x.PhysicalStatus == StorageUnitStatusConstants.Available ||
                (x.PhysicalStatus == StorageUnitStatusConstants.Reserved && x.ExpiredHold && !x.Holds));

            return new UnitTypeAvailabilityDto
            {
                UnitTypeId = t.Id,
                Code = t.Code,
                Name = t.Name,
                WidthM = t.WidthM,
                LengthM = t.LengthM,
                HeightM = t.HeightM,
                AreaM2 = t.AreaM2,
                VolumeM3 = t.VolumeM3,
                MonthlyRate = rates.FirstOrDefault(r => r.UnitTypeId == t.Id)?.MonthlyRate,
                AvailableUnitCount = availableCount
            };
        }).ToList();

        return new FacilityDetailDto
        {
            Id = f.Id,
            Code = f.Code,
            Name = f.Name,
            Address = f.AddressLine,
            City = f.City,
            Latitude = f.Latitude,
            Longitude = f.Longitude,
            OpeningTime = f.OpeningTime,
            ClosingTime = f.ClosingTime,
            Description = null,
            Amenities = new List<string>(),
            UnitTypes = types
        };
    }

    public async Task<IReadOnlyList<UnitTypeDto>> GetUnitTypesAsync(CancellationToken ct = default)
    {
        return await db.UnitTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new UnitTypeDto
            {
                Id = t.Id,
                Code = t.Code,
                Name = t.Name,
                WidthM = t.WidthM,
                LengthM = t.LengthM,
                HeightM = t.HeightM,
                AreaM2 = t.AreaM2,
                VolumeM3 = t.VolumeM3,
                MaxWeightKg = t.MaxWeightKg,
                ClimateControlled = t.ClimateControlled,
                Description = t.Description
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AvailableUnitDto>> GetAvailableUnitsAsync(
        long? facilityId,
        long? unitTypeId,
        long? facilityAreaId,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rates = await db.FacilityRates
            .AsNoTracking()
            .Where(r =>
                (!facilityId.HasValue || r.FacilityId == facilityId) &&
                (!unitTypeId.HasValue || r.UnitTypeId == unitTypeId) &&
                r.ValidFrom <= today &&
                (r.ValidTo == null || r.ValidTo >= today))
            .ToListAsync(ct);

        var units = await ListedUnits()
            .Where(u =>
                (!facilityId.HasValue || u.FacilityId == facilityId) &&
                (!unitTypeId.HasValue || u.UnitTypeId == unitTypeId) &&
                (!facilityAreaId.HasValue || u.AreaId == facilityAreaId) &&
                (u.PhysicalStatus == StorageUnitStatusConstants.Available ||
                 (u.PhysicalStatus == StorageUnitStatusConstants.Reserved && u.UnitAllocations.Any(a =>
                     a.Status == AllocationStatusConstants.Active &&
                     a.ReservationId != null &&
                     a.Reservation!.Status == ReservationStatusConstants.Pending &&
                     a.Reservation.HoldUntil <= now))) &&
                !u.UnitAllocations.Any(a =>
                    a.Status == AllocationStatusConstants.Active &&
                    a.ReservationId != null &&
                    a.Reservation!.Status == ReservationStatusConstants.Pending &&
                    a.Reservation.HoldUntil > now))
            .Select(u => new
            {
                u.Id,
                u.FacilityId,
                u.UnitTypeId,
                u.AreaId,
                u.UnitCode,
                u.FloorLabel,
                u.ZoneLabel
            })
            .ToListAsync(ct);

        return units.Select(u => new AvailableUnitDto
        {
            Id = u.Id,
            FacilityId = u.FacilityId,
            UnitTypeId = u.UnitTypeId,
            FacilityAreaId = u.AreaId,
            UnitCode = u.UnitCode,
            FloorLabel = u.FloorLabel,
            ZoneLabel = u.ZoneLabel,
            MonthlyRate = rates.FirstOrDefault(r => r.FacilityId == u.FacilityId && r.UnitTypeId == u.UnitTypeId)?.MonthlyRate ?? 0m
        }).ToList();
    }

    public async Task<FacilityMapDto?> GetFacilityMapAsync(long facilityId, CancellationToken ct = default)
    {
        var facilityExists = await db.Facilities
            .AnyAsync(f => f.Id == facilityId && f.Status == FacilityStatusConstants.Active, ct);

        if (!facilityExists) return null;

        var now = DateTimeOffset.UtcNow;

        var data = await db.UnitMapPositions
            .AsNoTracking()
            .Where(p => p.Unit.FacilityId == facilityId && p.Area.IsActive)
            .Select(p => new
            {
                p.UnitId,
                p.Unit.UnitCode,
                p.Unit.UnitTypeId,
                p.AreaId,
                p.Area.Name,
                p.X,
                p.Y,
                p.Width,
                p.Height,
                p.RotationDegrees,
                p.Metadata,
                p.Unit.PhysicalStatus,
                Pending = p.Unit.UnitAllocations.Any(a =>
                    a.Status == AllocationStatusConstants.Active &&
                    a.ReservationId != null &&
                    a.Reservation!.Status == ReservationStatusConstants.Pending &&
                    a.Reservation.HoldUntil > now),
                ExpiredHold = p.Unit.UnitAllocations.Any(a =>
                    a.Status == AllocationStatusConstants.Active &&
                    a.ReservationId != null &&
                    a.Reservation!.Status == ReservationStatusConstants.Pending &&
                    a.Reservation.HoldUntil <= now)
            })
            .ToListAsync(ct);

        return new FacilityMapDto
        {
            FacilityId = facilityId,
            Units = data.Select(p => new FacilityMapUnitDto
            {
                UnitId = p.UnitId,
                UnitCode = p.UnitCode,
                UnitTypeId = p.UnitTypeId,
                AreaId = p.AreaId,
                Layer = p.Name,
                X = p.X,
                Y = p.Y,
                Width = p.Width,
                Height = p.Height,
                RotationDegrees = p.RotationDegrees,
                Status = DetermineUnitMapStatus(p.Pending, p.PhysicalStatus, p.ExpiredHold),
                Metadata = p.Metadata
            }).ToList()
        };
    }

    public async Task<PricingCalculationDto> CalculatePricingAsync(
        CalculatePricingRequest request,
        CancellationToken ct = default)
    {
        if (request.DurationMonths is < 1 or > 12)
        {
            throw AppException.FromError(CatalogErrors.InvalidPricingRequest);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rate = await ActiveRates(request.FacilityId, today)
            .Where(r => r.UnitTypeId == request.UnitTypeId)
            .OrderByDescending(r => r.ValidFrom)
            .FirstOrDefaultAsync(ct);

        if (rate is null)
        {
            throw AppException.FromError(CatalogErrors.InvalidPricingRequest);
        }

        var rent = rate.MonthlyRate * request.DurationMonths;
        var discount = 0m;
        Promotion? promotion = null;

        if (!string.IsNullOrWhiteSpace(request.VoucherCode))
        {
            var now = DateTimeOffset.UtcNow;
            promotion = await db.Promotions
                .Include(p => p.PromotionRules)
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.Code == request.VoucherCode.Trim() &&
                    p.IsActive &&
                    p.ValidFrom <= now &&
                    p.ValidTo >= now, ct);

            var isVoucherInvalid = promotion is null || promotion.PromotionRules.Any(r =>
                r.RuleType.Contains("month", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(r.RuleValue, out var minimum) &&
                request.DurationMonths < minimum);

            if (isVoucherInvalid)
            {
                throw AppException.FromError(CatalogErrors.InvalidVoucher);
            }

            discount = promotion!.DiscountType == PromotionDiscountTypeConstants.Percentage
                ? rent * promotion.DiscountValue / 100m
                : promotion.DiscountValue;

            if (promotion.MaxDiscountAmount.HasValue)
            {
                discount = Math.Min(discount, promotion.MaxDiscountAmount.Value);
            }

            discount = Math.Min(discount, rent);
        }

        var feeRules = await db.FeeRules
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                (x.FacilityId == null || x.FacilityId == request.FacilityId) &&
                x.ValidFrom <= today &&
                (x.ValidTo == null || x.ValidTo >= today))
            .ToListAsync(ct);

        var bookingFee = feeRules
            .Where(x => x.FeeType.Contains("booking", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.FacilityId.HasValue)
            .Select(x => x.Amount ?? 0m)
            .FirstOrDefault();

        var taxable = rent + rate.MonthlyRate + bookingFee - discount;

        var taxRate = feeRules
            .Where(x => x.FeeType.Contains("tax", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.FacilityId.HasValue)
            .Select(x => x.RatePercent ?? 0m)
            .FirstOrDefault();

        var tax = taxable * taxRate / 100m;
        var total = rent + rate.MonthlyRate + bookingFee - discount + tax;

        return new PricingCalculationDto
        {
            BaseMonthlyRate = rate.MonthlyRate,
            DurationMonths = request.DurationMonths,
            RentAmount = rent,
            SecurityDeposit = rate.MonthlyRate,
            BookingFee = bookingFee,
            DiscountAmount = discount,
            TaxAmount = tax,
            TotalAmount = total,
            AppliedVoucherCode = promotion?.Code
        };
    }

    private static string DetermineUnitMapStatus(bool pending, string physicalStatus, bool expiredHold)
    {
        if (pending) return "pending_payment";
        if (physicalStatus == StorageUnitStatusConstants.Reserved && expiredHold) return "available";

        return physicalStatus switch
        {
            StorageUnitStatusConstants.Available => "available",
            StorageUnitStatusConstants.Occupied or StorageUnitStatusConstants.InUse => "occupied",
            StorageUnitStatusConstants.UnderMaintenance => "maintenance",
            _ => "reserved"
        };
    }

    private IQueryable<StorageUnit> ListedUnits()
    {
        return db.StorageUnits
            .AsNoTracking()
            .Where(u => u.IsListed && u.Facility.Status == FacilityStatusConstants.Active && u.UnitType.IsActive);
    }

    private IQueryable<FacilityRate> ActiveRates(long facilityId, DateOnly today)
    {
        return db.FacilityRates
            .AsNoTracking()
            .Where(r => r.FacilityId == facilityId && r.ValidFrom <= today && (r.ValidTo == null || r.ValidTo >= today));
    }
}
