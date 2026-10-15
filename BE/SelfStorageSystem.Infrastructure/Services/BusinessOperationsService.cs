using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Operations;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public sealed class BusinessOperationsService(SelfStorageDbContext db) : IBusinessOperationsService
{
    public async Task<PagedResult<OperationsRateDto>> GetRatesAsync(
        OperationsRateQueryParameters query, CancellationToken ct = default)
    {
        var rows = db.FacilityRates.AsNoTracking().Include(r => r.Facility).Include(r => r.UnitType).AsQueryable();
        if (query.FacilityId.HasValue) rows = rows.Where(r => r.FacilityId == query.FacilityId);
        if (query.UnitTypeId.HasValue) rows = rows.Where(r => r.UnitTypeId == query.UnitTypeId);
        if (query.EffectiveOn.HasValue)
            rows = rows.Where(r => r.ValidFrom <= query.EffectiveOn && (r.ValidTo == null || r.ValidTo > query.EffectiveOn));
        return await PageAsync(rows.OrderBy(r => r.FacilityId).ThenBy(r => r.UnitTypeId)
            .ThenByDescending(r => r.ValidFrom).ThenBy(r => r.Id), query.PageNumber, query.PageSize, MapRate, ct);
    }

    public async Task<OperationsRateDto> UpsertRateAsync(
        OperationsUpsertRateRequest request, long actorUserId, CancellationToken ct = default)
    {
        Validate(request);
        await EnsureActorAsync(actorUserId, ct);
        var facility = await GetFacilityAsync(request.FacilityId, ct);
        var unitType = await db.UnitTypes.FirstOrDefaultAsync(t => t.Id == request.UnitTypeId && t.IsActive, ct)
            ?? throw AppException.FromError(OperationsErrors.UnitTypeNotFound);
        var existing = request.Id.HasValue
            ? await db.FacilityRates.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            : await db.FacilityRates.FirstOrDefaultAsync(r => r.FacilityId == request.FacilityId &&
                r.UnitTypeId == request.UnitTypeId && r.ValidFrom == request.ValidFrom, ct);
        if (request.Id.HasValue && existing is null)
            throw AppException.FromError(OperationsErrors.RateNotFound);

        var deposit = request.DepositAmount ?? request.MonthlyRate;
        var unchanged = existing is not null && existing.FacilityId == request.FacilityId &&
            existing.UnitTypeId == request.UnitTypeId && existing.MonthlyRate == request.MonthlyRate &&
            existing.DepositAmount == deposit && existing.BookingFee == request.BookingFee &&
            existing.ValidFrom == request.ValidFrom && existing.ValidTo == request.ValidTo;
        if (unchanged)
        {
            existing!.Facility = facility;
            existing.UnitType = unitType;
            return MapRate(existing);
        }

        if (existing is not null && await db.Reservations.AnyAsync(r => r.FacilityRateId == existing.Id, ct))
        {
            // Historical snapshots stay immutable, but an operator can close a version
            // and introduce a new one without invalidating any reservation's start date.
            if (existing.FacilityId != request.FacilityId || existing.UnitTypeId != request.UnitTypeId ||
                existing.MonthlyRate != request.MonthlyRate || existing.DepositAmount != deposit ||
                existing.BookingFee != request.BookingFee || existing.ValidFrom != request.ValidFrom ||
                (request.ValidTo.HasValue && await db.Reservations.AnyAsync(r =>
                    r.FacilityRateId == existing.Id && r.StartDate >= request.ValidTo.Value, ct)))
                throw AppException.FromError(OperationsErrors.RateInUse);
        }
        if (!await db.PriceRanges.AnyAsync(p => p.UnitTypeId == request.UnitTypeId &&
            p.ValidFrom <= request.ValidFrom && (p.ValidTo == null || p.ValidTo > request.ValidFrom) &&
            p.MinMonthlyRate <= request.MonthlyRate && p.MaxMonthlyRate >= request.MonthlyRate, ct))
            throw AppException.FromError(OperationsErrors.PriceOutsideRange);

        var id = existing?.Id ?? 0;
        // Effective periods use [ValidFrom, ValidTo), matching the SQL triggers.
        if (await db.FacilityRates.AnyAsync(r => r.Id != id && r.FacilityId == request.FacilityId &&
            r.UnitTypeId == request.UnitTypeId && (r.ValidTo == null || request.ValidFrom < r.ValidTo) &&
            (request.ValidTo == null || r.ValidFrom < request.ValidTo), ct))
            throw AppException.FromError(OperationsErrors.RateOverlap);

        var old = existing is null ? null : JsonSerializer.Serialize(MapRate(existing));
        var rate = existing ?? new FacilityRate { CreatedAt = DateTimeOffset.UtcNow, CreatedBy = actorUserId };
        rate.FacilityId = facility.Id;
        rate.Facility = facility;
        rate.UnitTypeId = unitType.Id;
        rate.UnitType = unitType;
        rate.MonthlyRate = request.MonthlyRate;
        rate.DepositAmount = deposit;
        rate.BookingFee = request.BookingFee;
        rate.ValidFrom = request.ValidFrom;
        rate.ValidTo = request.ValidTo;
        if (existing is null) db.FacilityRates.Add(rate);
        await SaveWithAuditAsync(actorUserId, nameof(FacilityRate), existing?.Id.ToString() ??
            $"{rate.FacilityId}:{rate.UnitTypeId}:{rate.ValidFrom:yyyy-MM-dd}", old, MapRate(rate), ct);
        return MapRate(rate);
    }

    public async Task<PagedResult<OperationsFeeDto>> GetFeesAsync(
        OperationsFeeQueryParameters query, CancellationToken ct = default)
    {
        var rows = db.FeeRules.AsNoTracking().Include(r => r.Facility).AsQueryable();
        if (query.GlobalOnly) rows = rows.Where(r => r.FacilityId == null);
        else if (query.FacilityId.HasValue) rows = rows.Where(r => r.FacilityId == query.FacilityId || r.FacilityId == null);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToUpperInvariant();
            rows = rows.Where(r => r.Code.ToUpper().Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(query.FeeType))
        {
            var type = query.FeeType.Trim().ToLowerInvariant();
            rows = rows.Where(r => r.FeeType == type);
        }
        if (query.IsActive.HasValue) rows = rows.Where(r => r.IsActive == query.IsActive);
        if (query.EffectiveOn.HasValue)
            rows = rows.Where(r => r.ValidFrom <= query.EffectiveOn && (r.ValidTo == null || r.ValidTo > query.EffectiveOn));
        return await PageAsync(rows.OrderBy(r => r.Code).ThenBy(r => r.FacilityId)
            .ThenByDescending(r => r.ValidFrom).ThenBy(r => r.Id), query.PageNumber, query.PageSize, MapFee, ct);
    }

    public async Task<OperationsFeeDto> UpsertFeeAsync(
        OperationsUpsertFeeRequest request, long actorUserId, CancellationToken ct = default)
    {
        Validate(request);
        await EnsureActorAsync(actorUserId, ct);
        var facility = request.FacilityId.HasValue ? await GetFacilityAsync(request.FacilityId.Value, ct) : null;
        var code = request.Code.Trim().ToUpperInvariant();
        var type = request.FeeType.Trim().ToLowerInvariant();
        var method = request.CalculationMethod.Trim().ToLowerInvariant();
        var conditions = ValidateFeeConditions(request, type, method);
        var existing = request.Id.HasValue
            ? await db.FeeRules.FirstOrDefaultAsync(f => f.Id == request.Id, ct)
            : await db.FeeRules.FirstOrDefaultAsync(f => f.FacilityId == request.FacilityId &&
                f.Code.ToUpper() == code && f.ValidFrom == request.ValidFrom, ct);
        if (request.Id.HasValue && existing is null)
            throw AppException.FromError(OperationsErrors.FeeNotFound);
        var id = existing?.Id ?? 0;
        if (await db.FeeRules.AnyAsync(f => f.Id != id && f.FacilityId == request.FacilityId &&
            f.Code.ToUpper() == code && (f.ValidTo == null || request.ValidFrom < f.ValidTo) &&
            (request.ValidTo == null || f.ValidFrom < request.ValidTo), ct))
            throw AppException.FromError(OperationsErrors.FeeOverlap);
        var old = existing is null ? null : JsonSerializer.Serialize(MapFee(existing));
        var fee = existing ?? new FeeRule { CreatedAt = DateTimeOffset.UtcNow, CreatedBy = actorUserId };
        fee.FacilityId = request.FacilityId;
        fee.Facility = facility;
        fee.Code = code;
        fee.FeeType = type;
        fee.CalculationMethod = method;
        fee.Amount = request.Amount;
        fee.RatePercent = request.RatePercent;
        fee.GraceDays = request.GraceDays;
        fee.Conditions = conditions;
        fee.ValidFrom = request.ValidFrom;
        fee.ValidTo = request.ValidTo;
        fee.IsActive = request.IsActive;
        if (existing is null) db.FeeRules.Add(fee);
        await SaveWithAuditAsync(actorUserId, nameof(FeeRule), existing?.Id.ToString() ??
            $"{request.FacilityId}:{code}:{request.ValidFrom:yyyy-MM-dd}", old, MapFee(fee), ct);
        return MapFee(fee);
    }

    public async Task<PagedResult<OperationsPromotionDto>> GetPromotionsAsync(
        OperationsPromotionQueryParameters query, CancellationToken ct = default)
    {
        var rows = db.Promotions.AsNoTracking().Include(p => p.PromotionRules).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToUpperInvariant();
            rows = rows.Where(p => p.Code.ToUpper().Contains(search) || p.Name.ToUpper().Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(query.DiscountType))
        {
            var type = query.DiscountType.Trim().ToLowerInvariant();
            rows = rows.Where(p => p.DiscountType == type);
        }
        if (query.IsActive.HasValue) rows = rows.Where(p => p.IsActive == query.IsActive);
        if (query.EffectiveAt.HasValue)
            rows = rows.Where(p => p.ValidFrom <= query.EffectiveAt && p.ValidTo >= query.EffectiveAt);
        return await PageAsync(rows.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
            query.PageNumber, query.PageSize, MapPromotion, ct);
    }

    public async Task<OperationsPromotionDto> CreatePromotionAsync(
        OperationsCreatePromotionRequest request, long actorUserId, CancellationToken ct = default)
    {
        Validate(request);
        await EnsureActorAsync(actorUserId, ct);
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Promotions.AnyAsync(p => p.Code.ToUpper() == code, ct))
            throw AppException.FromError(OperationsErrors.DuplicatePromotionCode);
        var rules = new List<PromotionRule>();
        var types = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in request.Rules)
        {
            if (rule is null) throw AppException.FromError(OperationsErrors.InvalidPromotionRule);
            Validate(rule);
            var type = rule.RuleType.Trim().ToLowerInvariant();
            var op = rule.Operator.Trim().ToLowerInvariant();
            if (!types.Add(type) || !long.TryParse(rule.RuleValue, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0)
                throw AppException.FromError(OperationsErrors.InvalidPromotionRule);
            if (type == PromotionRuleTypeConstants.Facility && op == OperationsConstants.EqualOperator)
                await GetFacilityAsync(value, ct);
            else if (type == PromotionRuleTypeConstants.UnitType && op == OperationsConstants.EqualOperator)
            {
                if (!await db.UnitTypes.AnyAsync(t => t.Id == value && t.IsActive, ct))
                    throw AppException.FromError(OperationsErrors.UnitTypeNotFound);
            }
            else if (type != OperationsConstants.MinMonthsRuleType ||
                op != OperationsConstants.GreaterThanOrEqualOperator || value > 12)
                throw AppException.FromError(OperationsErrors.InvalidPromotionRule);
            rules.Add(new PromotionRule { RuleType = type, Operator = op, RuleValue = value.ToString(CultureInfo.InvariantCulture) });
        }
        var promotion = new Promotion
        {
            Code = code, Name = request.Name.Trim(), Description = request.Description?.Trim(),
            DiscountType = request.DiscountType.Trim().ToLowerInvariant(), DiscountValue = request.DiscountValue,
            MaxDiscountAmount = request.MaxDiscountAmount, UsageLimit = request.UsageLimit,
            PerCustomerLimit = request.PerCustomerLimit, ValidFrom = request.ValidFrom, ValidTo = request.ValidTo,
            IsActive = request.IsActive, CreatedBy = actorUserId, CreatedAt = DateTimeOffset.UtcNow,
            PromotionRules = rules
        };
        db.Promotions.Add(promotion);
        await SaveWithAuditAsync(actorUserId, nameof(Promotion), code, null, MapPromotion(promotion), ct);
        return MapPromotion(promotion);
    }

    public async Task<OperationsPromotionDto> UpdatePromotionStatusAsync(long id,
        OperationsUpdatePromotionStatusRequest request, long actorUserId, CancellationToken ct = default)
    {
        Validate(request);
        await EnsureActorAsync(actorUserId, ct);
        var promotion = await db.Promotions.Include(p => p.PromotionRules).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw AppException.FromError(OperationsErrors.PromotionNotFound);
        var old = JsonSerializer.Serialize(MapPromotion(promotion));
        promotion.IsActive = request.IsActive!.Value;
        await SaveWithAuditAsync(actorUserId, nameof(Promotion), id.ToString(), old, MapPromotion(promotion), ct);
        return MapPromotion(promotion);
    }

    public async Task<OperationsFacilityDto> CreateFacilityAsync(
        OperationsCreateFacilityRequest request, long actorUserId, CancellationToken ct = default)
    {
        Validate(request);
        await EnsureActorAsync(actorUserId, ct);
        ValidateTimezone(request.Timezone);
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Facilities.AnyAsync(f => f.Code.ToUpper() == code, ct))
            throw AppException.FromError(OperationsErrors.DuplicateFacilityCode);
        var facility = new Facility { CreatedAt = DateTimeOffset.UtcNow };
        ApplyFacility(facility, request);
        db.Facilities.Add(facility);
        await SaveWithAuditAsync(actorUserId, nameof(Facility), code, null, MapFacility(facility), ct);
        return MapFacility(facility);
    }

    public async Task<OperationsFacilityDto> UpdateFacilityAsync(
        OperationsUpdateFacilityRequest request, long actorUserId, CancellationToken ct = default)
    {
        Validate(request);
        await EnsureActorAsync(actorUserId, ct);
        ValidateTimezone(request.Timezone);
        var facility = await GetFacilityAsync(request.Id, ct);
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Facilities.AnyAsync(f => f.Id != request.Id && f.Code.ToUpper() == code, ct))
            throw AppException.FromError(OperationsErrors.DuplicateFacilityCode);
        var old = JsonSerializer.Serialize(MapFacility(facility));
        ApplyFacility(facility, request);
        await SaveWithAuditAsync(actorUserId, nameof(Facility), facility.Id.ToString(), old, MapFacility(facility), ct);
        return MapFacility(facility);
    }

    private static void ApplyFacility(Facility facility, OperationsCreateFacilityRequest request)
    {
        facility.Code = request.Code.Trim().ToUpperInvariant();
        facility.Name = request.Name.Trim();
        facility.AddressLine = request.AddressLine.Trim();
        facility.City = request.City.Trim();
        facility.Ward = request.Ward?.Trim();
        facility.District = request.District?.Trim();
        facility.Latitude = request.Latitude;
        facility.Longitude = request.Longitude;
        facility.Timezone = request.Timezone.Trim();
        facility.OpeningTime = request.OpeningTime;
        facility.ClosingTime = request.ClosingTime;
        facility.Status = request.Status.Trim().ToLowerInvariant();
        facility.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task EnsureActorAsync(long actorUserId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == actorUserId && u.Status == UserStatusConstants.Active, ct))
            throw AppException.FromError(SelfStorageSystem.Domain.Common.Error.Forbidden(
                "Operations.ActorInactive", "The acting user must be an active account."));
    }

    private async Task<Facility> GetFacilityAsync(long id, CancellationToken ct) =>
        await db.Facilities.FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw AppException.FromError(OperationsErrors.FacilityNotFound);

    private static void Validate(object request)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), results, true))
            throw AppException.FromError(SelfStorageSystem.Domain.Common.Error.Validation(
                OperationsErrors.InvalidRequest.Code, string.Join(" ", results.Select(r => r.ErrorMessage))));
    }

    private static void ValidateTimezone(string timezone)
    {
        try { TimeZoneInfo.FindSystemTimeZoneById(timezone.Trim()); }
        catch (TimeZoneNotFoundException) { throw AppException.FromError(OperationsErrors.InvalidTimezone); }
        catch (InvalidTimeZoneException) { throw AppException.FromError(OperationsErrors.InvalidTimezone); }
    }

    private static string ValidateFeeConditions(OperationsUpsertFeeRequest request, string type, string method)
    {
        try
        {
            using var doc = JsonDocument.Parse(request.Conditions);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) throw AppException.FromError(OperationsErrors.InvalidRequest);
            if (type == OperationsConstants.LateFeeType &&
                (method != OperationsConstants.PercentageCalculationMethod ||
                 request.RatePercent != OperationsConstants.DefaultLateFeeRatePercent || request.GraceDays != 0 ||
                 !doc.RootElement.TryGetProperty("base", out var basis) || basis.ValueKind != JsonValueKind.String ||
                 basis.GetString() != "daily_rent" ||
                 (doc.RootElement.TryGetProperty("start_day", out var day) &&
                  (day.ValueKind != JsonValueKind.Number || !day.TryGetInt32(out var start) || start != 1))))
                throw AppException.FromError(OperationsErrors.InvalidRequest);
            return doc.RootElement.GetRawText();
        }
        catch (JsonException) { throw AppException.FromError(OperationsErrors.InvalidRequest); }
    }

    private async Task SaveWithAuditAsync(long actorUserId, string entityType, string entityId,
        string? oldValues, object newValues, CancellationToken ct)
    {
        if (entityId.Length > 255) entityId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(entityId)));
        // A single SaveChanges makes the configuration change and its audit record atomic.
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorUserId, EntitySchema = AdminConstants.DefaultAuditSchema,
            EntityType = entityType, EntityId = entityId,
            Action = oldValues is null ? AuditActionConstants.Create : AuditActionConstants.Update,
            OldValues = oldValues, NewValues = JsonSerializer.Serialize(newValues), OccurredAt = DateTimeOffset.UtcNow
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql &&
            sql.Number is 2601 or 2627 or 51003 or 51005)
        {
            throw AppException.FromError(OperationsErrors.Conflict, ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number is 547 or 51022)
        {
            throw AppException.FromError(OperationsErrors.InvalidRequest, ex);
        }
    }

    private static async Task<PagedResult<TDto>> PageAsync<TEntity, TDto>(IQueryable<TEntity> rows,
        int pageNumber, int pageSize, Func<TEntity, TDto> map, CancellationToken ct) where TEntity : class
    {
        pageSize = pageSize < 1 ? OperationsConstants.DefaultPageSize : Math.Min(pageSize, OperationsConstants.MaxPageSize);
        pageNumber = Math.Clamp(pageNumber, 1, int.MaxValue / pageSize);
        var total = await rows.CountAsync(ct);
        var items = await rows.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<TDto>(items.Select(map).ToList(), total, pageNumber, pageSize);
    }

    private static OperationsRateDto MapRate(FacilityRate rate) => new()
    {
        Id = rate.Id, FacilityId = rate.FacilityId, FacilityName = rate.Facility?.Name ?? string.Empty,
        UnitTypeId = rate.UnitTypeId, UnitTypeName = rate.UnitType?.Name ?? string.Empty,
        MonthlyRate = rate.MonthlyRate, DepositAmount = rate.DepositAmount, BookingFee = rate.BookingFee,
        ValidFrom = rate.ValidFrom, ValidTo = rate.ValidTo, CreatedBy = rate.CreatedBy, CreatedAt = rate.CreatedAt
    };

    private static OperationsFeeDto MapFee(FeeRule fee) => new()
    {
        Id = fee.Id, FacilityId = fee.FacilityId, FacilityName = fee.Facility?.Name,
        Code = fee.Code, FeeType = fee.FeeType, CalculationMethod = fee.CalculationMethod,
        Amount = fee.Amount, RatePercent = fee.RatePercent, GraceDays = fee.GraceDays,
        Conditions = fee.Conditions, ValidFrom = fee.ValidFrom, ValidTo = fee.ValidTo,
        IsActive = fee.IsActive, CreatedBy = fee.CreatedBy, CreatedAt = fee.CreatedAt
    };

    private static OperationsPromotionDto MapPromotion(Promotion p) => new()
    {
        Id = p.Id, Code = p.Code, Name = p.Name, Description = p.Description,
        DiscountType = p.DiscountType, DiscountValue = p.DiscountValue, MaxDiscountAmount = p.MaxDiscountAmount,
        UsageLimit = p.UsageLimit, PerCustomerLimit = p.PerCustomerLimit, ValidFrom = p.ValidFrom, ValidTo = p.ValidTo,
        IsActive = p.IsActive, CreatedBy = p.CreatedBy, CreatedAt = p.CreatedAt,
        Rules = p.PromotionRules.OrderBy(r => r.RuleType).Select(r => new OperationsPromotionRuleDto
        { Id = r.Id, RuleType = r.RuleType, Operator = r.Operator, RuleValue = r.RuleValue }).ToList()
    };

    private static OperationsFacilityDto MapFacility(Facility f) => new()
    {
        Id = f.Id, Code = f.Code, Name = f.Name, AddressLine = f.AddressLine, City = f.City,
        Ward = f.Ward, District = f.District, Latitude = f.Latitude, Longitude = f.Longitude,
        Timezone = f.Timezone, OpeningTime = f.OpeningTime, ClosingTime = f.ClosingTime,
        Status = f.Status, CreatedAt = f.CreatedAt, UpdatedAt = f.UpdatedAt
    };
}
