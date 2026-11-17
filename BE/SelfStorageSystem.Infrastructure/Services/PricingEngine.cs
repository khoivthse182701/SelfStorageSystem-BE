using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Customer.Facilities;
using SelfStorageSystem.Domain.Common;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Common;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public sealed class PricingEngine(
    SelfStorageDbContext db,
    IOptions<ReservationSettings>? reservationOptions = null) : IPricingEngine
{
    private readonly ReservationSettings _settings = reservationOptions?.Value ?? new ReservationSettings();

    public async Task<PricingQuoteDto> CalculateQuoteAsync(
        PricingQuoteRequest request,
        long? customerId = null,
        bool lockPromotionRow = false,
        bool throwConflictOnPromotionLimit = false,
        CancellationToken cancellationToken = default)
    {
        if (request.DurationMonths < _settings.MinDurationMonths || request.DurationMonths > _settings.MaxDurationMonths)
            throw AppException.FromError(ReservationErrors.InvalidDuration(_settings.MinDurationMonths, _settings.MaxDurationMonths));

        var facility = await db.Facilities
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.FacilityId && f.Status == FacilityStatusConstants.Active, cancellationToken)
            ?? throw AppException.FromError(throwConflictOnPromotionLimit ? ReservationErrors.FacilityNotFound : CatalogErrors.FacilityNotFound);

        var unitTypeExists = await db.UnitTypes
            .AsNoTracking()
            .AnyAsync(ut => ut.Id == request.UnitTypeId && ut.IsActive, cancellationToken);
        if (!unitTypeExists)
            throw AppException.FromError(ReservationErrors.UnitTypeNotFound);

        var now = DateTimeOffset.UtcNow;
        var tz = TimeZoneHelper.ResolveTimeZone(facility.Timezone);
        var facilityToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, tz).DateTime);

        if (request.StartDate < facilityToday)
            throw AppException.FromError(ReservationErrors.StartDateInPast);

        var rate = await db.FacilityRates
            .AsNoTracking()
            .Where(fr => fr.FacilityId == request.FacilityId
                      && fr.UnitTypeId == request.UnitTypeId
                      && fr.ValidFrom <= request.StartDate
                      && (fr.ValidTo == null || fr.ValidTo > request.StartDate))
            .OrderByDescending(fr => fr.ValidFrom)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw AppException.FromError(ReservationErrors.NoPublishedRate);

        var baseMonthlyRate = rate.MonthlyRate;
        var rentSubtotal = Math.Round(baseMonthlyRate * request.DurationMonths, 0, MidpointRounding.AwayFromZero);
        var depositAmount = rate.DepositAmount;
        var bookingFee = rate.BookingFee;

        var discountAmount = 0m;
        Promotion? promotion = null;

        if (!string.IsNullOrWhiteSpace(request.PromotionCode))
        {
            promotion = await EvaluatePromotionAsync(
                request.PromotionCode.Trim(),
                request.FacilityId,
                request.UnitTypeId,
                request.DurationMonths,
                rentSubtotal,
                customerId,
                now,
                lockPromotionRow,
                throwConflictOnPromotionLimit,
                cancellationToken);

            discountAmount = promotion.DiscountType.Trim().ToLowerInvariant() switch
            {
                PromotionDiscountTypeConstants.Percentage =>
                    Math.Round(baseMonthlyRate * promotion.DiscountValue / 100m, 0, MidpointRounding.AwayFromZero),
                PromotionDiscountTypeConstants.Fixed => promotion.DiscountValue,
                PromotionDiscountTypeConstants.FreeDays =>
                    Math.Round(baseMonthlyRate * promotion.DiscountValue / 30m, 0, MidpointRounding.AwayFromZero),
                _ => 0m
            };

            if (promotion.MaxDiscountAmount.HasValue)
                discountAmount = Math.Min(discountAmount, promotion.MaxDiscountAmount.Value);

            discountAmount = Math.Min(Math.Max(0m, discountAmount), baseMonthlyRate);
        }

        var vatRatePercent = await db.FeeRules
            .AsNoTracking()
            .Where(x => x.IsActive
                     && (x.FacilityId == null || x.FacilityId == request.FacilityId)
                     && x.ValidFrom <= facilityToday
                     && (x.ValidTo == null || x.ValidTo > facilityToday)
                     && (x.FeeType.ToLower() == FeeRuleTypeConstants.Tax
                         || x.FeeType.ToLower() == FeeRuleTypeConstants.VatTax
                         || x.FeeType.ToLower().Contains(FeeRuleTypeConstants.Tax)))
            .OrderByDescending(x => x.FacilityId.HasValue)
            .ThenByDescending(x => x.ValidFrom)
            .Select(x => x.RatePercent ?? 0m)
            .FirstOrDefaultAsync(cancellationToken);

        var taxableBase = Math.Max(0m, baseMonthlyRate - discountAmount) + bookingFee;
        var taxAmount = Math.Round(taxableBase * vatRatePercent / 100m, 0, MidpointRounding.AwayFromZero);

        var totalTaxable = Math.Max(0m, rentSubtotal - discountAmount) + bookingFee;
        var totalTaxAmount = Math.Round(totalTaxable * vatRatePercent / 100m, 0, MidpointRounding.AwayFromZero);

        var firstPaymentAmount = Math.Max(0m, baseMonthlyRate - discountAmount) + depositAmount + bookingFee + taxAmount;
        var totalContractAmount = Math.Max(0m, rentSubtotal - discountAmount) + depositAmount + bookingFee + totalTaxAmount;

        return new PricingQuoteDto
        {
            BaseMonthlyRate = baseMonthlyRate,
            DurationMonths = request.DurationMonths,
            RentSubtotal = rentSubtotal,
            DepositAmount = depositAmount,
            BookingFee = bookingFee,
            DiscountAmount = discountAmount,
            VatRatePercent = vatRatePercent,
            TaxAmount = taxAmount,
            TotalTaxAmount = totalTaxAmount,
            TotalContractAmount = totalContractAmount,
            FirstPaymentAmount = firstPaymentAmount,
            AppliedPromotionCode = promotion?.Code,
            AppliedPromotionId = promotion?.Id
        };
    }

    private async Task<Promotion> EvaluatePromotionAsync(
        string promoCode,
        long facilityId,
        long unitTypeId,
        int durationMonths,
        decimal rentSubtotal,
        long? customerId,
        DateTimeOffset now,
        bool lockRow,
        bool throwConflictOnLimit,
        CancellationToken ct)
    {
        var invalidErr = throwConflictOnLimit ? ReservationErrors.PromotionInvalidOrExpired : CatalogErrors.InvalidVoucher;

        Promotion? promotion = lockRow && db.Database.IsSqlServer()
            ? await db.Promotions
                .FromSqlInterpolated($"SELECT * FROM [core].[promotions] WITH (UPDLOCK, ROWLOCK) WHERE code = {promoCode} AND is_active = 1")
                .Include(p => p.PromotionRules)
                .FirstOrDefaultAsync(ct)
            : await db.Promotions
                .Include(p => p.PromotionRules)
                .FirstOrDefaultAsync(p => p.Code == promoCode && p.IsActive, ct);

        if (promotion is null || promotion.ValidFrom > now || promotion.ValidTo < now)
            throw AppException.FromError(invalidErr);

        foreach (var rule in promotion.PromotionRules)
        {
            var rt = rule.RuleType.Trim().ToLowerInvariant();
            var op = NormalizeOperator(rule.Operator);
            var rv = rule.RuleValue?.Trim() ?? string.Empty;

            if (rt.Contains(PromotionRuleTypeConstants.Facility))
            {
                if (!EvaluateIdRule(facilityId, op, rv))
                    throw AppException.FromError(invalidErr);
            }
            else if (rt.Contains(PromotionRuleTypeConstants.UnitType))
            {
                if (!EvaluateIdRule(unitTypeId, op, rv))
                    throw AppException.FromError(invalidErr);
            }
            else if (rt.Contains(PromotionRuleTypeConstants.Month))
            {
                var effectiveOp = rt.StartsWith("minimum", StringComparison.Ordinal) && op == PromotionOperatorConstants.Eq
                    ? PromotionOperatorConstants.Gte
                    : op;
                if (!EvaluateDecimalRule(durationMonths, effectiveOp, rv))
                    throw AppException.FromError(invalidErr);
            }
            else if (rt.Contains(PromotionRuleTypeConstants.MinimumAmount))
            {
                var effectiveOp = op == PromotionOperatorConstants.Eq ? PromotionOperatorConstants.Gte : op;
                if (!EvaluateDecimalRule(rentSubtotal, effectiveOp, rv))
                    throw AppException.FromError(invalidErr);
            }
            else if (rt.Contains(PromotionRuleTypeConstants.NewCustomer))
            {
                var requireNewCustomer = !bool.TryParse(rv, out var parsedFlag) || parsedFlag;
                if (requireNewCustomer && !customerId.HasValue)
                    throw AppException.FromError(invalidErr);

                if (customerId.HasValue)
                {
                    var hasHistory = await db.Reservations.AsNoTracking()
                        .AnyAsync(r => r.CustomerId == customerId.Value &&
                                       (r.Status == ReservationStatusConstants.Confirmed ||
                                        r.Status == ReservationStatusConstants.Converted ||
                                        r.Status == ReservationStatusConstants.Completed), ct)
                        || await db.RentalAgreements.AsNoTracking()
                            .AnyAsync(a => a.CustomerId == customerId.Value, ct);
                    if (requireNewCustomer && hasHistory)
                        throw AppException.FromError(invalidErr);
                    if (!requireNewCustomer && !hasHistory)
                        throw AppException.FromError(invalidErr);
                }
            }
        }

        if (promotion.UsageLimit.HasValue)
        {
            var usedCount = await db.PromotionRedemptions
                .CountAsync(pr => pr.PromotionId == promotion.Id && pr.Status != PromotionRedemptionStatusConstants.Released, ct);
            if (usedCount >= promotion.UsageLimit.Value)
                throw AppException.FromError(throwConflictOnLimit ? ReservationErrors.PromotionUsageLimitReached : CatalogErrors.InvalidVoucher);
        }

        if (customerId.HasValue && promotion.PerCustomerLimit.HasValue)
        {
            var customerUsedCount = await db.PromotionRedemptions
                .CountAsync(pr => pr.PromotionId == promotion.Id
                               && pr.CustomerId == customerId.Value
                               && pr.Status != PromotionRedemptionStatusConstants.Released, ct);
            if (customerUsedCount >= promotion.PerCustomerLimit.Value)
            {
                throw AppException.FromError(throwConflictOnLimit
                    ? ReservationErrors.PromotionPerCustomerLimitReached(promotion.PerCustomerLimit.Value)
                    : CatalogErrors.InvalidVoucher);
            }
        }

        return promotion;
    }

    private static string NormalizeOperator(string? op) =>
        (op ?? PromotionOperatorConstants.Eq).Trim().ToLowerInvariant() switch
        {
            "==" or "=" or PromotionOperatorConstants.Eq => PromotionOperatorConstants.Eq,
            ">=" or PromotionOperatorConstants.Gte => PromotionOperatorConstants.Gte,
            "<=" or PromotionOperatorConstants.Lte => PromotionOperatorConstants.Lte,
            PromotionOperatorConstants.In => PromotionOperatorConstants.In,
            _ => PromotionOperatorConstants.Eq
        };

    private static bool EvaluateIdRule(long actualValue, string op, string ruleValue)
    {
        if (op == PromotionOperatorConstants.In)
        {
            var tokens = ruleValue.Trim('[', ']').Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                if (long.TryParse(token.Trim(), out var candidate) && candidate == actualValue)
                    return true;
            }
            return false;
        }

        if (!long.TryParse(ruleValue, out var target))
            return false;

        return op switch
        {
            PromotionOperatorConstants.Eq => actualValue == target,
            PromotionOperatorConstants.Gte => actualValue >= target,
            PromotionOperatorConstants.Lte => actualValue <= target,
            _ => false
        };
    }

    private static bool EvaluateDecimalRule(decimal actualValue, string op, string ruleValue)
    {
        if (op == PromotionOperatorConstants.In)
        {
            var tokens = ruleValue.Trim('[', ']').Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                if (decimal.TryParse(token.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var candidate) &&
                    candidate == actualValue)
                    return true;
            }
            return false;
        }

        if (!decimal.TryParse(ruleValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var target))
            return false;

        return op switch
        {
            PromotionOperatorConstants.Eq => actualValue == target,
            PromotionOperatorConstants.Gte => actualValue >= target,
            PromotionOperatorConstants.Lte => actualValue <= target,
            _ => false
        };
    }
}
