using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/customer")]
public sealed class CustomerCatalogController(SelfStorageDbContext db) : ControllerBase
{
    [HttpGet("facilities")]
    public async Task<IActionResult> GetFacilities(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await db.Facilities.AsNoTracking().Where(f => f.Status == "active")
            .Select(f => new
            {
                f.Id, f.Code, f.Name, f.AddressLine, f.Ward, f.District, f.City,
                f.Latitude, f.Longitude, f.Timezone, f.OpeningTime, f.ClosingTime,
                AvailableUnitCount = f.StorageUnits.Count(u => u.IsListed && u.PhysicalStatus == "available"
                    && !u.UnitAllocations.Any(a => a.Status == "active" && a.AllocationKind == "reservation_hold"
                        && a.Reservation != null && a.Reservation.Status == "pending" && a.Reservation.HoldUntil > now))
            }).ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(rows));
    }

    [HttpGet("facilities/{id:long}")]
    public async Task<IActionResult> GetFacility(long id, CancellationToken ct)
    {
        var f = await db.Facilities.AsNoTracking().Where(x => x.Id == id && x.Status == "active")
            .Select(x => new { x.Id, x.Code, x.Name, x.AddressLine, x.Ward, x.District, x.City, x.Latitude, x.Longitude, x.Timezone, x.OpeningTime, x.ClosingTime }).SingleOrDefaultAsync(ct);
        if (f is null) return NotFound(ApiResponse.Fail("Facility not found."));
        var now = DateTimeOffset.UtcNow;
        var facilityUnits = await db.StorageUnits.AsNoTracking().Where(u => u.FacilityId == id && u.IsListed)
            .Select(u => new { u.UnitTypeId, u.UnitType.Code, u.UnitType.Name, u.UnitType.WidthM, u.UnitType.LengthM, u.UnitType.HeightM, u.UnitType.AreaM2, u.UnitType.VolumeM3, u.UnitType.Description,
                IsAvailable = u.PhysicalStatus == "available" && !u.UnitAllocations.Any(a => a.Status == "active" && a.AllocationKind == "reservation_hold" && a.Reservation != null && a.Reservation.Status == "pending" && a.Reservation.HoldUntil > now) }).ToListAsync(ct);
        var units = facilityUnits.GroupBy(u => new { u.UnitTypeId, u.Code, u.Name, u.WidthM, u.LengthM, u.HeightM, u.AreaM2, u.VolumeM3, u.Description })
            .Select(g => new { g.Key.UnitTypeId, g.Key.Code, g.Key.Name, g.Key.WidthM, g.Key.LengthM, g.Key.HeightM, g.Key.AreaM2, g.Key.VolumeM3, g.Key.Description, AvailableCount = g.Count(u => u.IsAvailable) }).ToList();
        var rates = await db.FacilityRates.AsNoTracking().Where(r => r.FacilityId == id && r.ValidFrom <= DateOnly.FromDateTime(DateTime.UtcNow) && (r.ValidTo == null || r.ValidTo > DateOnly.FromDateTime(DateTime.UtcNow)))
            .Select(r => new { r.UnitTypeId, r.MonthlyRate, r.DepositAmount, r.BookingFee }).ToListAsync(ct);
        var areaMetadata = await db.FacilityAreas.AsNoTracking().Where(a => a.FacilityId == id && a.IsActive).Select(a => new { a.Id, a.Code, a.Name, a.MapMetadata }).ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { Facility = f, UnitTypes = units.Select(u => new { u.UnitTypeId, u.Code, u.Name, u.WidthM, u.LengthM, u.HeightM, u.AreaM2, u.VolumeM3, u.Description, u.AvailableCount, Price = rates.FirstOrDefault(r => r.UnitTypeId == u.UnitTypeId) }), AreaMetadata = areaMetadata }));
    }

    [HttpGet("unit-types")]
    public async Task<IActionResult> GetUnitTypes(CancellationToken ct)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var list = await db.UnitTypes.AsNoTracking().Where(t => t.IsActive).Select(t => new { t.Id, t.Code, t.Name, t.WidthM, t.LengthM, t.HeightM, t.AreaM2, t.VolumeM3, t.ClimateControlled, t.MaxWeightKg, t.Description,
            PriceRanges = t.PriceRanges.Where(p => p.ValidFrom <= date && (p.ValidTo == null || p.ValidTo > date)).Select(p => new { p.MinMonthlyRate, p.MaxMonthlyRate }) }).ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(list));
    }

    [HttpGet("storage-units/available")]
    public async Task<IActionResult> GetAvailableUnits([FromQuery] long? facilityId, [FromQuery] long? unitTypeId, [FromQuery] long? facilityAreaId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var q = db.StorageUnits.AsNoTracking().Where(u => u.IsListed && u.PhysicalStatus == "available"
            && (facilityId == null || u.FacilityId == facilityId) && (unitTypeId == null || u.UnitTypeId == unitTypeId) && (facilityAreaId == null || u.AreaId == facilityAreaId)
            && !u.UnitAllocations.Any(a => a.Status == "active" && a.AllocationKind == "reservation_hold" && a.Reservation != null && a.Reservation.Status == "pending" && a.Reservation.HoldUntil > now));
        var items = await q.Select(u => new { u.Id, u.FacilityId, FacilityName = u.Facility.Name, u.UnitTypeId, UnitTypeName = u.UnitType.Name, u.AreaId, u.UnitCode, u.FloorLabel, u.ZoneLabel, u.PhysicalStatus }).ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(items));
    }

    [HttpGet("facilities/{facilityId:long}/map")]
    public async Task<IActionResult> GetMap(long facilityId, CancellationToken ct)
    {
        var facility = await db.Facilities.AsNoTracking().AnyAsync(f => f.Id == facilityId && f.Status == "active", ct);
        if (!facility) return NotFound(ApiResponse.Fail("Facility not found."));
        var now = DateTimeOffset.UtcNow;
        var items = await db.StorageUnits.AsNoTracking().Where(u => u.FacilityId == facilityId && u.IsListed && u.UnitMapPosition != null)
            .Select(u => new { u.Id, u.UnitCode, u.UnitTypeId, UnitTypeName = u.UnitType.Name, u.PhysicalStatus, u.FloorLabel, u.ZoneLabel,
                Area = u.Area == null ? null : new { u.Area.Id, u.Area.Code, u.Area.Name, u.Area.AreaType, u.Area.ParentAreaId, u.Area.MapMetadata },
                Position = new { u.UnitMapPosition!.X, u.UnitMapPosition.Y, u.UnitMapPosition.Width, u.UnitMapPosition.Height, u.UnitMapPosition.RotationDegrees, u.UnitMapPosition.Metadata },
                Held = u.UnitAllocations.Any(a => a.Status == "active" && a.AllocationKind == "reservation_hold" && a.Reservation != null && a.Reservation.Status == "pending" && a.Reservation.HoldUntil > now),
                Occupied = u.UnitAllocations.Any(a => a.Status == "active" && a.AllocationKind == "rental") }).ToListAsync(ct);
        var mapped = items.Select(u => new { u.Id, u.UnitCode, u.UnitTypeId, u.UnitTypeName, u.FloorLabel, u.ZoneLabel, u.Area, u.Position, Status = u.Held ? "reserved" : u.Occupied || u.PhysicalStatus == "occupied" ? "occupied" : u.PhysicalStatus == "maintenance" ? "maintenance" : u.PhysicalStatus == "available" ? "available" : u.PhysicalStatus });
        return Ok(ApiResponse<object>.Ok(mapped));
    }

    [HttpPost("pricing/calculate")]
    public async Task<IActionResult> Calculate([FromBody] PriceRequest request, CancellationToken ct)
    {
        if (request.DurationMonths is < 1 or > 12) return BadRequest(ApiResponse.Fail("durationMonths must be between 1 and 12."));
        if (request.FacilityId <= 0 || request.UnitTypeId <= 0) return BadRequest(ApiResponse.Fail("facilityId and unitTypeId are required."));
        if (!string.IsNullOrWhiteSpace(request.VoucherCode) && request.VoucherCode.Contains(',', StringComparison.Ordinal)) return BadRequest(ApiResponse.Fail("Only one voucher may be applied."));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rate = await db.FacilityRates.AsNoTracking().Where(r => r.FacilityId == request.FacilityId && r.UnitTypeId == request.UnitTypeId && r.ValidFrom <= today && (r.ValidTo == null || r.ValidTo > today))
            .OrderByDescending(r => r.ValidFrom).Select(r => new { r.MonthlyRate, r.BookingFee }).FirstOrDefaultAsync(ct);
        if (rate is null) return NotFound(ApiResponse.Fail("No active price configured for this facility and unit type."));
        var subtotal = rate.MonthlyRate * request.DurationMonths;
        decimal discount = 0;
        if (!string.IsNullOrWhiteSpace(request.VoucherCode))
        {
            var now = DateTimeOffset.UtcNow;
            var promo = await db.Promotions.AsNoTracking().Include(p => p.PromotionRules).FirstOrDefaultAsync(p => p.Code == request.VoucherCode.Trim() && p.IsActive && p.ValidFrom <= now && p.ValidTo >= now, ct);
            if (promo is null) return BadRequest(ApiResponse.Fail("Voucher is invalid or expired."));
            foreach (var rule in promo.PromotionRules)
            {
                if (rule.RuleType == "minimum_months" && !RulePasses(request.DurationMonths, rule.Operator, rule.RuleValue)) return BadRequest(ApiResponse.Fail("Rental duration does not meet voucher requirements."));
                if (rule.RuleType == "facility" && !RulePasses(request.FacilityId, rule.Operator, rule.RuleValue)) return BadRequest(ApiResponse.Fail("Voucher is not valid for this facility."));
                if (rule.RuleType == "unit_type" && !RulePasses(request.UnitTypeId, rule.Operator, rule.RuleValue)) return BadRequest(ApiResponse.Fail("Voucher is not valid for this unit type."));
            }
            discount = string.Equals(promo.DiscountType, "percentage", StringComparison.OrdinalIgnoreCase) ? subtotal * promo.DiscountValue / 100m : promo.DiscountValue;
            if (promo.MaxDiscountAmount.HasValue) discount = Math.Min(discount, promo.MaxDiscountAmount.Value);
            discount = Math.Min(discount, subtotal);
        }
        var taxRule = await db.FeeRules.AsNoTracking().Where(r => r.IsActive && r.FeeType == "tax"
            && (r.FacilityId == null || r.FacilityId == request.FacilityId) && r.ValidFrom <= today && (r.ValidTo == null || r.ValidTo > today))
            .OrderByDescending(r => r.FacilityId == request.FacilityId).ThenByDescending(r => r.ValidFrom)
            .Select(r => new { r.CalculationMethod, r.Amount, r.RatePercent }).FirstOrDefaultAsync(ct);
        var tax = taxRule == null ? 0m : string.Equals(taxRule.CalculationMethod, "percentage", StringComparison.OrdinalIgnoreCase)
            ? subtotal * (taxRule.RatePercent ?? 0m) / 100m : taxRule.Amount ?? 0m;
        // Deposit defaults to one month of rent as defined by BR-FIN-01.
        return Ok(ApiResponse<object>.Ok(new { BaseMonthlyRate = rate.MonthlyRate, DurationMonths = request.DurationMonths, RentalSubtotal = subtotal, SecurityDeposit = rate.MonthlyRate, BookingFee = rate.BookingFee, Discount = discount, TaxAmount = tax, Total = subtotal + rate.MonthlyRate + rate.BookingFee + tax - discount }));
    }

    private static bool RulePasses(long value, string op, string expected) =>
        (op == "gte" && long.TryParse(expected, NumberStyles.Integer, CultureInfo.InvariantCulture, out var min) && value >= min)
        || (op == "lte" && long.TryParse(expected, NumberStyles.Integer, CultureInfo.InvariantCulture, out var max) && value <= max)
        || (op == "eq" && long.TryParse(expected, NumberStyles.Integer, CultureInfo.InvariantCulture, out var exact) && value == exact)
        || (op == "in" && expected.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Any(x => long.TryParse(x, out var item) && item == value));
}

public sealed class PriceRequest
{
    public long FacilityId { get; set; }
    public long UnitTypeId { get; set; }
    public int DurationMonths { get; set; }
    public string? VoucherCode { get; set; }
}
