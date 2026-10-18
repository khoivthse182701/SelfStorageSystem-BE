using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SelfStorageSystem.Contracts.Operations;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;
using Xunit;

namespace SelfStorageSystem.Tests;

public class BusinessOperationsServiceTests
{
    private const long ActorId = 99;

    [Fact]
    public async Task UpsertRate_RepeatingNaturalKey_PreservesOneRateAndDefaultsDepositToMonthlyRent()
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        var service = new BusinessOperationsService(db);
        var request = NewRate();

        var first = await service.UpsertRateAsync(request, ActorId);
        var second = await service.UpsertRateAsync(request, ActorId);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(await db.FacilityRates.ToListAsync());
        Assert.Equal(request.MonthlyRate, second.DepositAmount);
        Assert.Equal(ActorId, second.CreatedBy);
        Assert.NotEmpty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task UpsertRate_OverlappingEffectivePeriods_RejectsWithoutWriting()
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        db.FacilityRates.Add(ExistingRate());
        await db.SaveChangesAsync();
        var service = new BusinessOperationsService(db);
        var request = NewRate();
        request.ValidFrom = new DateOnly(2026, 6, 29);
        request.ValidTo = new DateOnly(2026, 12, 31);

        await Assert.ThrowsAsync<AppConflictException>(() => service.UpsertRateAsync(request, ActorId));

        Assert.Single(await db.FacilityRates.ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task UpsertRate_StartsExactlyAtPreviousExclusiveEnd_AllowsNewRate()
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        db.FacilityRates.Add(ExistingRate());
        await db.SaveChangesAsync();
        var request = NewRate();
        request.ValidFrom = new DateOnly(2026, 6, 30);

        var created = await new BusinessOperationsService(db).UpsertRateAsync(request, ActorId);

        Assert.Equal(2, await db.FacilityRates.CountAsync());
        Assert.Equal(new DateOnly(2026, 6, 30), created.ValidFrom);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0.001)]
    [InlineData(1000000000000)]
    public async Task UpsertRate_InvalidMoney_RejectsWithoutSaving(double amount)
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        var request = NewRate();
        request.MonthlyRate = (decimal)amount;

        await Assert.ThrowsAsync<AppValidationException>(() => new BusinessOperationsService(db).UpsertRateAsync(request, ActorId));

        Assert.Empty(await db.FacilityRates.ToListAsync());
    }

    [Fact]
    public async Task UpsertRate_OutsideApprovedPriceRange_Rejects()
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        var request = NewRate();
        request.MonthlyRate = 2_000_001m;

        await Assert.ThrowsAsync<AppValidationException>(() => new BusinessOperationsService(db).UpsertRateAsync(request, ActorId));

        Assert.Empty(await db.FacilityRates.ToListAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpsertRate_UnknownFacilityOrUnitType_ReturnsNotFound(bool unknownFacility)
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        var request = NewRate();
        if (unknownFacility) request.FacilityId = 404;
        else request.UnitTypeId = 404;

        await Assert.ThrowsAsync<AppNotFoundException>(() => new BusinessOperationsService(db).UpsertRateAsync(request, ActorId));

        Assert.Empty(await db.FacilityRates.ToListAsync());
    }

    [Fact]
    public async Task UpsertRate_UnknownExplicitId_ReturnsNotFoundInsteadOfCreating()
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        var request = NewRate();
        request.Id = 404;

        await Assert.ThrowsAsync<AppNotFoundException>(() => new BusinessOperationsService(db).UpsertRateAsync(request, ActorId));

        Assert.Empty(await db.FacilityRates.ToListAsync());
    }

    [Fact]
    public async Task UpsertRate_ReferencedByReservation_RejectsRepricingButAllowsIdenticalRetry()
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        db.FacilityRates.Add(ExistingRate());
        db.Reservations.Add(new Reservation
        {
            Id = 1,
            ReservationCode = "RSV-EXISTING",
            CustomerId = 10,
            FacilityId = 1,
            UnitTypeId = 1,
            FacilityRateId = 1,
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2026, 6, 1),
            MonthlyRateSnapshot = 1_000_000m,
            Status = "confirmed"
        });
        await db.SaveChangesAsync();
        var service = new BusinessOperationsService(db);
        var request = NewRate();
        request.Id = 1;
        request.ValidTo = new DateOnly(2026, 6, 30);
        request.MonthlyRate = 1_100_000m;

        await Assert.ThrowsAsync<AppConflictException>(() => service.UpsertRateAsync(request, ActorId));
        Assert.Equal(1_000_000m, (await db.FacilityRates.FindAsync(1L))!.MonthlyRate);

        request.MonthlyRate = 1_000_000m;
        var retried = await service.UpsertRateAsync(request, ActorId);
        Assert.Equal(1, retried.Id);
        Assert.Equal(1_000_000m, retried.MonthlyRate);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public async Task UpsertRate_CloseReferencedPeriod_ProtectsReservationStartDate(int endDay, bool allowed)
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        var existing = ExistingRate();
        existing.ValidTo = null;
        db.FacilityRates.Add(existing);
        db.Reservations.Add(new Reservation
        {
            Id = 1,
            ReservationCode = "RSV-CLOSURE",
            CustomerId = 10,
            FacilityId = 1,
            UnitTypeId = 1,
            FacilityRateId = 1,
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2026, 8, 1),
            MonthlyRateSnapshot = 1_000_000m,
            Status = "confirmed"
        });
        await db.SaveChangesAsync();
        var request = NewRate();
        request.Id = 1;
        request.ValidTo = new DateOnly(2026, 5, endDay);
        var service = new BusinessOperationsService(db);

        if (allowed)
        {
            var updated = await service.UpsertRateAsync(request, ActorId);
            Assert.Equal(request.ValidTo, updated.ValidTo);
            Assert.Equal(1_000_000m, updated.MonthlyRate);
        }
        else
        {
            await Assert.ThrowsAsync<AppConflictException>(() => service.UpsertRateAsync(request, ActorId));
            Assert.Null((await db.FacilityRates.FindAsync(1L))!.ValidTo);
        }
    }

    [Fact]
    public async Task UpsertFee_RepeatingNaturalKey_DoesNotDuplicateGlobalRule()
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        var service = new BusinessOperationsService(db);
        var request = NewFee();

        var first = await service.UpsertFeeAsync(request, ActorId);
        var second = await service.UpsertFeeAsync(request, ActorId);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(await db.FeeRules.ToListAsync());
        Assert.Null(second.FacilityId);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("123")]
    public async Task UpsertFee_InvalidConditions_RejectsBeforeSaving(string conditions)
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        var request = NewFee();
        request.Conditions = conditions;

        await Assert.ThrowsAsync<AppValidationException>(() => new BusinessOperationsService(db).UpsertFeeAsync(request, ActorId));

        Assert.Empty(await db.FeeRules.ToListAsync());
    }

    [Fact]
    public async Task UpsertFee_PercentageWithoutRate_Rejects()
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        var request = NewFee();
        request.CalculationMethod = "percentage";
        request.Amount = null;
        request.RatePercent = null;

        await Assert.ThrowsAsync<AppValidationException>(() => new BusinessOperationsService(db).UpsertFeeAsync(request, ActorId));
    }

    [Theory]
    [InlineData("\"1\"")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("{}")]
    public async Task UpsertFee_LateFeeStartDayIsNotNumeric_ReturnsValidationError(string startDayJson)
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        var request = NewFee();
        request.FeeType = "late_fee";
        request.CalculationMethod = "percentage";
        request.Amount = null;
        request.RatePercent = 150m;
        request.Conditions = "{\"base\":\"daily_rent\",\"start_day\":" + startDayJson + "}";

        await Assert.ThrowsAsync<AppValidationException>(() => new BusinessOperationsService(db).UpsertFeeAsync(request, ActorId));

        Assert.Empty(await db.FeeRules.ToListAsync());
    }

    [Fact]
    public async Task CreatePromotion_PersistsScopeQuotaAndNormalizedCodeTogether()
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        var request = NewPromotion();
        request.Code = "  summer10  ";
        request.UsageLimit = 100;
        request.PerCustomerLimit = 1;
        request.Rules = new()
        {
            new() { RuleType = "facility", Operator = "eq", RuleValue = "1" },
            new() { RuleType = "unit_type", Operator = "eq", RuleValue = "1" },
            new() { RuleType = OperationsConstants.MinMonthsRuleType, Operator = "gte", RuleValue = "3" }
        };

        var result = await new BusinessOperationsService(db).CreatePromotionAsync(request, ActorId);

        Assert.Equal("SUMMER10", result.Code);
        Assert.Equal(100, result.UsageLimit);
        Assert.Equal(1, result.PerCustomerLimit);
        Assert.Equal(3, result.Rules.Count);
        Assert.Equal(3, await db.PromotionRules.CountAsync(r => r.PromotionId == result.Id));
        Assert.Single(await db.Promotions.ToListAsync());
    }

    [Fact]
    public async Task CreatePromotion_DuplicateCodeWithDifferentCaseAndWhitespace_Rejects()
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        var service = new BusinessOperationsService(db);
        await service.CreatePromotionAsync(NewPromotion(), ActorId);
        var duplicate = NewPromotion();
        duplicate.Code = " summer10 ";

        await Assert.ThrowsAsync<AppConflictException>(() => service.CreatePromotionAsync(duplicate, ActorId));

        Assert.Single(await db.Promotions.ToListAsync());
    }

    [Theory]
    [InlineData("facility")]
    [InlineData("unit_type")]
    public async Task CreatePromotion_UnknownScope_RejectsWithoutPartialPromotion(string ruleType)
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        var request = NewPromotion();
        request.Rules = new() { new() { RuleType = ruleType, Operator = "eq", RuleValue = "404" } };

        await Assert.ThrowsAsync<AppNotFoundException>(() => new BusinessOperationsService(db).CreatePromotionAsync(request, ActorId));

        Assert.Empty(await db.Promotions.ToListAsync());
        Assert.Empty(await db.PromotionRules.ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task CreatePromotion_PercentageAboveOneHundred_Rejects()
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        var request = NewPromotion();
        request.DiscountValue = 100.01m;

        await Assert.ThrowsAsync<AppValidationException>(() => new BusinessOperationsService(db).CreatePromotionAsync(request, ActorId));
    }

    [Fact]
    public async Task CreatePromotion_PerCustomerLimitExceedsTotalQuota_Rejects()
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        var request = NewPromotion();
        request.UsageLimit = 2;
        request.PerCustomerLimit = 3;

        await Assert.ThrowsAsync<AppValidationException>(() => new BusinessOperationsService(db).CreatePromotionAsync(request, ActorId));
    }

    [Fact]
    public async Task UpdatePromotionStatus_DeactivatesPromotionWithoutDeletingItsRules()
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        var service = new BusinessOperationsService(db);
        var request = NewPromotion();
        request.Rules = new() { new() { RuleType = "facility", Operator = "eq", RuleValue = "1" } };
        var created = await service.CreatePromotionAsync(request, ActorId);

        var result = await service.UpdatePromotionStatusAsync(created.Id, new() { IsActive = false }, ActorId);

        Assert.False(result.IsActive);
        Assert.Single(await db.Promotions.ToListAsync());
        Assert.Single(await db.PromotionRules.ToListAsync());
    }

    [Fact]
    public async Task CreateFacility_InvalidTimezone_RejectsWithoutSaving()
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        var request = NewFacility();
        request.Timezone = "Invalid/Unknown_Zone";

        await Assert.ThrowsAsync<AppValidationException>(() => new BusinessOperationsService(db).CreateFacilityAsync(request, ActorId));

        Assert.Single(await db.Facilities.ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task CreateFacility_DuplicateNormalizedCode_Rejects()
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        var request = NewFacility();
        request.Code = " hcm-01 ";

        await Assert.ThrowsAsync<AppConflictException>(() => new BusinessOperationsService(db).CreateFacilityAsync(request, ActorId));

        Assert.Single(await db.Facilities.ToListAsync());
    }

    [Fact]
    public async Task CreateFacility_PreservesCoordinatesHoursAndTimezone()
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        var request = NewFacility();
        request.Latitude = 10.762622m;
        request.Longitude = 106.660172m;
        request.OpeningTime = new TimeOnly(8, 0);
        request.ClosingTime = new TimeOnly(21, 30);

        var result = await new BusinessOperationsService(db).CreateFacilityAsync(request, ActorId);

        Assert.Equal("DN-01", result.Code);
        Assert.Equal(request.Latitude, result.Latitude);
        Assert.Equal(request.Longitude, result.Longitude);
        Assert.Equal(request.OpeningTime, result.OpeningTime);
        Assert.Equal(request.ClosingTime, result.ClosingTime);
        Assert.Equal("Asia/Ho_Chi_Minh", result.Timezone);
    }

    private static OperationsUpsertRateRequest NewRate() => new()
    {
        FacilityId = 1,
        UnitTypeId = 1,
        MonthlyRate = 1_000_000m,
        BookingFee = 50_000m,
        ValidFrom = new DateOnly(2026, 1, 1),
        ValidTo = new DateOnly(2026, 12, 31)
    };

    private static OperationsUpsertFeeRequest NewFee() => new()
    {
        Code = "LOST_KEY",
        FeeType = "lost_key_fee",
        CalculationMethod = "flat",
        Amount = 100_000m,
        Conditions = "{}",
        ValidFrom = new DateOnly(2026, 1, 1),
        ValidTo = new DateOnly(2026, 12, 31)
    };

    private static OperationsCreatePromotionRequest NewPromotion() => new()
    {
        Code = "SUMMER10",
        Name = "Summer promotion",
        DiscountType = PromotionDiscountTypeConstants.Percentage,
        DiscountValue = 10m,
        ValidFrom = DateTimeOffset.UtcNow.AddDays(-1),
        ValidTo = DateTimeOffset.UtcNow.AddMonths(1)
    };

    private static OperationsCreateFacilityRequest NewFacility() => new()
    {
        Code = "DN-01",
        Name = "Da Nang Warehouse",
        AddressLine = "123 Beach Street",
        City = "Da Nang",
        Timezone = "Asia/Ho_Chi_Minh",
        Status = FacilityStatusConstants.Active
    };

    private static SelfStorageDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SelfStorageDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new SelfStorageDbContext(options);
    }

    private static async Task SeedRateScopeAsync(SelfStorageDbContext db)
    {
        db.Users.Add(new User
        {
            Id = ActorId,
            Email = "operations@example.com",
            PasswordHash = "hash",
            Status = UserStatusConstants.Active
        });
        db.Facilities.Add(new Facility
        {
            Id = 1,
            Code = "HCM-01",
            Name = "Ho Chi Minh City",
            AddressLine = "123 Main Street",
            City = "Ho Chi Minh City",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = FacilityStatusConstants.Active
        });
        db.UnitTypes.Add(new UnitType
        {
            Id = 1,
            Code = "SMALL",
            Name = "Small unit",
            WidthM = 2,
            LengthM = 2,
            HeightM = 2,
            IsActive = true
        });
        db.PriceRanges.Add(new PriceRange
        {
            Id = 1,
            UnitTypeId = 1,
            MinMonthlyRate = 500_000m,
            MaxMonthlyRate = 2_000_000m,
            ValidFrom = new DateOnly(2025, 1, 1),
            CreatedBy = ActorId
        });
        await db.SaveChangesAsync();
    }

    private static FacilityRate ExistingRate(long id = 1) => new()
    {
        Id = id,
        FacilityId = 1,
        UnitTypeId = 1,
        MonthlyRate = 1_000_000m,
        DepositAmount = 1_000_000m,
        BookingFee = 50_000m,
        ValidFrom = new DateOnly(2026, 1, 1),
        ValidTo = new DateOnly(2026, 6, 30),
        CreatedBy = ActorId
    };

    [Fact]
    public async Task GetFacilities_ReturnsFilteredAndPagedResults()
    {
        await using var db = CreateDbContext();
        await SeedRateScopeAsync(db);
        db.Facilities.Add(new Facility { Id = 2, Code = "FAC-HN", Name = "Hanoi Central", AddressLine = "123 Ba Dinh", City = "Ha Noi", Timezone = "Asia/Ho_Chi_Minh", Status = FacilityStatusConstants.Active });
        db.Facilities.Add(new Facility { Id = 3, Code = "FAC-DN", Name = "Danang Beach", AddressLine = "456 Son Tra", City = "Da Nang", Timezone = "Asia/Ho_Chi_Minh", Status = "maintenance" });
        await db.SaveChangesAsync();

        var service = new BusinessOperationsService(db);
        var resultAll = await service.GetFacilitiesAsync(new OperationsFacilityQueryParameters());
        Assert.Equal(3, resultAll.TotalCount);

        var resultFiltered = await service.GetFacilitiesAsync(new OperationsFacilityQueryParameters { Status = "maintenance" });
        Assert.Equal(1, resultFiltered.TotalCount);
        Assert.Equal("FAC-DN", resultFiltered.Items[0].Code);
    }
}
