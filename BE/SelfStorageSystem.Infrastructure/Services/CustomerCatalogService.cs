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
                IsBlocked = u.UnitAllocations.Any(a =>
                    a.Status == AllocationStatusConstants.Active &&
                    (a.AgreementId != null ||
                     (a.Reservation != null && (
                         a.Reservation.Status == ReservationStatusConstants.Confirmed ||
                         a.Reservation.Status == ReservationStatusConstants.Converted ||
                         ((a.Reservation.Status == ReservationStatusConstants.Pending ||
                           a.Reservation.Status == ReservationStatusConstants.AwaitingDeposit) &&
                          a.Reservation.HoldUntil > now))))),
                HasExpiredPendingHold = u.UnitAllocations.Any(a =>
                    a.Status == AllocationStatusConstants.Active &&
                    a.ReservationId != null &&
                    (a.Reservation!.Status == ReservationStatusConstants.Pending ||
                     a.Reservation.Status == ReservationStatusConstants.AwaitingDeposit) &&
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
            AvailableUnitCount = unitRows.Count(u => u.FacilityId == f.Id && !u.IsBlocked &&
                (u.PhysicalStatus == StorageUnitStatusConstants.Available ||
                 (u.PhysicalStatus == StorageUnitStatusConstants.Reserved && u.HasExpiredPendingHold)))
        }).ToList();
    }

    public async Task<FacilityDetailDto?> GetFacilityAsync(long id, CancellationToken ct = default)
    {
        var f = await db.Facilities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.Status == FacilityStatusConstants.Active, ct);

        if (f is null) return null;

        var now = DateTimeOffset.UtcNow;
        var today = GetBusinessDate(f.Timezone, now);

        var units = await ListedUnits()
            .Where(u => u.FacilityId == id)
            .Select(u => new
            {
                u.UnitTypeId,
                u.PhysicalStatus,
                u.UnitType,
                IsBlocked = u.UnitAllocations.Any(a =>
                    a.Status == AllocationStatusConstants.Active &&
                    (a.AgreementId != null ||
                     (a.Reservation != null && (
                         a.Reservation.Status == ReservationStatusConstants.Confirmed ||
                         a.Reservation.Status == ReservationStatusConstants.Converted ||
                         ((a.Reservation.Status == ReservationStatusConstants.Pending ||
                           a.Reservation.Status == ReservationStatusConstants.AwaitingDeposit) &&
                          a.Reservation.HoldUntil > now))))),
                HasExpiredPendingHold = u.UnitAllocations.Any(a =>
                    a.Status == AllocationStatusConstants.Active &&
                    a.ReservationId != null &&
                    (a.Reservation!.Status == ReservationStatusConstants.Pending ||
                     a.Reservation.Status == ReservationStatusConstants.AwaitingDeposit) &&
                    a.Reservation.HoldUntil <= now)
            })
            .ToListAsync(ct);

        var rates = await ActiveRates(id, today)
            .OrderByDescending(r => r.ValidFrom)
            .ToListAsync(ct);

        var types = units.GroupBy(x => x.UnitTypeId).Select(g =>
        {
            var t = g.First().UnitType;
            var availableCount = g.Count(x => !x.IsBlocked &&
                (x.PhysicalStatus == StorageUnitStatusConstants.Available ||
                 (x.PhysicalStatus == StorageUnitStatusConstants.Reserved && x.HasExpiredPendingHold)));

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
        var facility = facilityId.HasValue
            ? await db.Facilities.AsNoTracking().FirstOrDefaultAsync(f => f.Id == facilityId.Value, ct)
            : null;
        var today = GetBusinessDate(facility?.Timezone, now);

        var rates = await db.FacilityRates
            .AsNoTracking()
            .Where(r =>
                (!facilityId.HasValue || r.FacilityId == facilityId) &&
                (!unitTypeId.HasValue || r.UnitTypeId == unitTypeId) &&
                r.ValidFrom <= today &&
                (r.ValidTo == null || r.ValidTo >= today))
            .OrderByDescending(r => r.ValidFrom)
            .ToListAsync(ct);

        var units = await ListedUnits()
            .Where(u =>
                (!facilityId.HasValue || u.FacilityId == facilityId) &&
                (!unitTypeId.HasValue || u.UnitTypeId == unitTypeId) &&
                (!facilityAreaId.HasValue || u.AreaId == facilityAreaId) &&
                !u.UnitAllocations.Any(a =>
                    a.Status == AllocationStatusConstants.Active &&
                    (a.AgreementId != null ||
                     (a.Reservation != null && (
                         a.Reservation.Status == ReservationStatusConstants.Confirmed ||
                         a.Reservation.Status == ReservationStatusConstants.Converted ||
                         ((a.Reservation.Status == ReservationStatusConstants.Pending ||
                           a.Reservation.Status == ReservationStatusConstants.AwaitingDeposit) &&
                          a.Reservation.HoldUntil > now))))) &&
                (u.PhysicalStatus == StorageUnitStatusConstants.Available ||
                 (u.PhysicalStatus == StorageUnitStatusConstants.Reserved && u.UnitAllocations.Any(a =>
                     a.Status == AllocationStatusConstants.Active &&
                     a.ReservationId != null &&
                     (a.Reservation!.Status == ReservationStatusConstants.Pending ||
                      a.Reservation.Status == ReservationStatusConstants.AwaitingDeposit) &&
                     a.Reservation.HoldUntil <= now))))
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


    public async Task<PricingCalculationDto> CalculatePricingAsync(
        CalculatePricingRequest request,
        CancellationToken ct = default)
    {
        if (request.DurationMonths is < 1 or > 12)
        {
            throw AppException.FromError(CatalogErrors.InvalidPricingRequest);
        }

        var facility = await db.Facilities
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.FacilityId && f.Status == FacilityStatusConstants.Active, ct);

        if (facility is null)
        {
            throw AppException.FromError(CatalogErrors.InvalidPricingRequest);
        }

        var now = DateTimeOffset.UtcNow;
        var today = GetBusinessDate(facility.Timezone, now);

        var rate = await ActiveRates(request.FacilityId, today)
            .Where(r => r.UnitTypeId == request.UnitTypeId)
            .OrderByDescending(r => r.ValidFrom)
            .FirstOrDefaultAsync(ct);

        if (rate is null)
        {
            throw AppException.FromError(CatalogErrors.InvalidPricingRequest);
        }

        var rent = Math.Round(rate.MonthlyRate * request.DurationMonths, 0, MidpointRounding.AwayFromZero);
        var discount = 0m;
        Promotion? promotion = null;

        if (!string.IsNullOrWhiteSpace(request.VoucherCode))
        {
            promotion = await db.Promotions
                .Include(p => p.PromotionRules)
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.Code == request.VoucherCode.Trim() &&
                    p.IsActive &&
                    p.ValidFrom <= now &&
                    p.ValidTo >= now, ct);

            if (promotion is null)
            {
                throw AppException.FromError(CatalogErrors.InvalidVoucher);
            }

            // Quota limit check
            if (promotion.UsageLimit.HasValue)
            {
                var usedCount = await db.PromotionRedemptions
                    .CountAsync(pr => pr.PromotionId == promotion.Id && pr.Status != PromotionRedemptionStatusConstants.Released, ct);

                if (usedCount >= promotion.UsageLimit.Value)
                {
                    throw AppException.FromError(CatalogErrors.InvalidVoucher);
                }
            }

            // Scope checks: Facility, UnitType, MinDuration
            var hasMismatchedFacility = promotion.PromotionRules.Any(r =>
                r.RuleType.Contains(PromotionRuleTypeConstants.Facility, StringComparison.OrdinalIgnoreCase) &&
                long.TryParse(r.RuleValue, out var targetFid) &&
                targetFid != request.FacilityId);

            var hasMismatchedUnitType = promotion.PromotionRules.Any(r =>
                r.RuleType.Contains(PromotionRuleTypeConstants.UnitType, StringComparison.OrdinalIgnoreCase) &&
                long.TryParse(r.RuleValue, out var targetUtid) &&
                targetUtid != request.UnitTypeId);

            var hasMismatchedDuration = promotion.PromotionRules.Any(r =>
                r.RuleType.Contains(PromotionRuleTypeConstants.Month, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(r.RuleValue, out var minimum) &&
                request.DurationMonths < minimum);

            if (hasMismatchedFacility || hasMismatchedUnitType || hasMismatchedDuration)
            {
                throw AppException.FromError(CatalogErrors.InvalidVoucher);
            }

            discount = promotion.DiscountType == PromotionDiscountTypeConstants.Percentage
                ? Math.Round(rent * promotion.DiscountValue / 100m, 0, MidpointRounding.AwayFromZero)
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
            .Where(x => x.FeeType.Contains(FeeRuleTypeConstants.Booking, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.FacilityId.HasValue)
            .Select(x => x.Amount ?? 0m)
            .FirstOrDefault();
        bookingFee = Math.Round(bookingFee, 0, MidpointRounding.AwayFromZero);

        // Accounting standard: VAT Tax applies to rental fee after discount (+ booking fee if applicable).
        // Security deposit is a refundable deposit, NOT subject to VAT.
        var taxable = Math.Max(0m, rent - discount) + bookingFee;

        var taxRate = feeRules
            .Where(x => x.FeeType.Contains(FeeRuleTypeConstants.Tax, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.FacilityId.HasValue)
            .Select(x => x.RatePercent ?? 0m)
            .FirstOrDefault();

        var tax = Math.Round(taxable * taxRate / 100m, 0, MidpointRounding.AwayFromZero);
        var securityDeposit = Math.Round(rate.MonthlyRate, 0, MidpointRounding.AwayFromZero);
        var total = Math.Round(rent - discount + bookingFee + securityDeposit + tax, 0, MidpointRounding.AwayFromZero);

        return new PricingCalculationDto
        {
            BaseMonthlyRate = rate.MonthlyRate,
            DurationMonths = request.DurationMonths,
            RentAmount = rent,
            SecurityDeposit = securityDeposit,
            BookingFee = bookingFee,
            DiscountAmount = discount,
            TaxAmount = tax,
            TotalAmount = total,
            AppliedVoucherCode = promotion?.Code
        };
    }


    private static DateOnly GetBusinessDate(string? timezone, DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(timezone))
        {
            timezone = TimezoneConstants.DefaultVietnam;
        }

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, tz).DateTime);
        }
        catch
        {
            return DateOnly.FromDateTime(utcNow.ToOffset(TimeSpan.FromHours(TimezoneConstants.VietnamUtcOffsetHours)).DateTime);
        }
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
            .Where(r => r.FacilityId == facilityId && r.ValidFrom <= today && (r.ValidTo == null || r.ValidTo >= today))
            .OrderByDescending(r => r.ValidFrom);
    }
}
