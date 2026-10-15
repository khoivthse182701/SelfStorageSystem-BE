using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Customer.Facilities;
using SelfStorageSystem.Contracts.Customer.Reservations;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;

namespace SelfStorageSystem.Tests;

public class OperationsConsumerCompatibilityTests
{
    [Theory]
    [InlineData(350000, 25000)]
    [InlineData(0, 0)]
    public async Task PricingAndReservation_UseConfiguredDepositAndBookingFee(
        int configuredDeposit,
        int configuredBookingFee)
    {
        await using var db = CreateDbContext();
        await SeedCatalogAsync(db, configuredDeposit, configuredBookingFee);

        // A generic fee rule must not replace an explicit facility rate, including zero.
        db.FeeRules.Add(new FeeRule
        {
            Code = "LEGACY-BOOKING",
            FeeType = "booking_fee",
            CalculationMethod = "fixed",
            Amount = 99999m,
            Conditions = "{}",
            ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var pricing = await new CustomerCatalogService(db).CalculatePricingAsync(new CalculatePricingRequest
        {
            FacilityId = 1,
            UnitTypeId = 1,
            DurationMonths = 1
        });
        var reservation = await CreateReservationService(db).CreateReservationAsync(10, ReservationRequest(1));

        Assert.Equal((decimal)configuredDeposit, pricing.SecurityDeposit);
        Assert.Equal((decimal)configuredBookingFee, pricing.BookingFee);
        Assert.Equal(pricing.SecurityDeposit, reservation.SecurityDeposit);
        Assert.Equal(pricing.TotalAmount, reservation.FirstPaymentAmount);

        var storedReservation = await db.Reservations.SingleAsync();
        Assert.Equal(pricing.SecurityDeposit, storedReservation.DepositSnapshot);
        Assert.Equal(pricing.BookingFee, storedReservation.BookingFeeSnapshot);
        var invoice = await db.Invoices.Include(i => i.InvoiceLines).SingleAsync();
        Assert.Equal(pricing.TotalAmount, invoice.TotalAmount);
        Assert.Equal((decimal)configuredDeposit,
            invoice.InvoiceLines.Single(l => l.LineType == InvoiceLineTypeConstants.Deposit).UnitPrice);
        Assert.Equal((decimal)configuredBookingFee,
            invoice.InvoiceLines.Where(l => l.LineType == InvoiceLineTypeConstants.BookingFee).Sum(l => l.UnitPrice));
    }

    [Theory]
    [InlineData("facility", "eq", "2")]
    [InlineData("unit_type", "eq", "2")]
    [InlineData("minimum_months", "gte", "4")]
    [InlineData("minimum_duration_months", ">=", "4")]
    public async Task PromotionOutsideScope_IsRejectedBeforeReservationWrites(
        string ruleType,
        string ruleOperator,
        string ruleValue)
    {
        await using var db = CreateDbContext();
        await SeedCatalogAsync(db, 350000m, 25000m);
        var promotion = PromotionWithRules(new PromotionRule
        {
            RuleType = ruleType,
            Operator = ruleOperator,
            RuleValue = ruleValue
        });
        db.Promotions.Add(promotion);
        await db.SaveChangesAsync();

        var catalogError = await Assert.ThrowsAsync<AppValidationException>(() =>
            new CustomerCatalogService(db).CalculatePricingAsync(new CalculatePricingRequest
            {
                FacilityId = 1,
                UnitTypeId = 1,
                DurationMonths = 3,
                VoucherCode = promotion.Code
            }));
        Assert.Equal(CatalogErrors.InvalidVoucher.Code, catalogError.Error.Code);

        var request = ReservationRequest(3);
        request.PromotionCode = promotion.Code;
        var reservationError = await Assert.ThrowsAsync<AppValidationException>(() =>
            CreateReservationService(db).CreateReservationAsync(10, request));
        Assert.Equal(ReservationErrors.PromotionInvalidOrExpired.Code, reservationError.Error.Code);
        Assert.Empty(await db.Reservations.ToListAsync());
        Assert.Empty(await db.Invoices.ToListAsync());
        Assert.Empty(await db.PromotionRedemptions.ToListAsync());
        Assert.Empty(await db.UnitAllocations.ToListAsync());
        Assert.Equal(StorageUnitStatusConstants.Available, (await db.StorageUnits.SingleAsync()).PhysicalStatus);
    }

    [Fact]
    public async Task PromotionWithMatchingOperationsRules_CanBeReservedAtMinimumDuration()
    {
        await using var db = CreateDbContext();
        await SeedCatalogAsync(db, 350000m, 25000m);
        var promotion = PromotionWithRules(
            new PromotionRule { RuleType = "facility", Operator = "eq", RuleValue = "1" },
            new PromotionRule { RuleType = "unit_type", Operator = "eq", RuleValue = "1" },
            new PromotionRule { RuleType = "minimum_months", Operator = "gte", RuleValue = "3" });
        db.Promotions.Add(promotion);
        await db.SaveChangesAsync();

        var request = ReservationRequest(3);
        request.PromotionCode = promotion.Code;
        var reservation = await CreateReservationService(db).CreateReservationAsync(10, request);

        Assert.Equal(100000m, reservation.DiscountAmount);
        var redemption = await db.PromotionRedemptions.SingleAsync();
        Assert.Equal(promotion.Id, redemption.PromotionId);
        Assert.Equal(reservation.ReservationId, redemption.ReservationId);
        Assert.Equal(PromotionRedemptionStatusConstants.Reserved, redemption.Status);
        Assert.Equal(100000m, (await db.Reservations.SingleAsync()).DiscountSnapshot);
    }

    [Fact]
    public async Task ReservationRateEndingAtStartDate_IsRejectedWithoutFallingBackToExpiredRate()
    {
        await using var db = CreateDbContext();
        await SeedCatalogAsync(db, 350000m, 25000m);
        var request = ReservationRequest(1);
        (await db.FacilityRates.SingleAsync()).ValidTo = request.StartDate;
        await db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AppException>(() =>
            CreateReservationService(db).CreateReservationAsync(10, request));

        Assert.Equal(ReservationErrors.NoPublishedRate.Code, error.Error.Code);
        Assert.Empty(await db.Reservations.ToListAsync());
        Assert.Empty(await db.Invoices.ToListAsync());
    }

    [Fact]
    public async Task CatalogRateEndingToday_IsExcludedFromPricingAndAvailability()
    {
        await using var db = CreateDbContext();
        await SeedCatalogAsync(db, 350000m, 25000m);
        (await db.FacilityRates.SingleAsync()).ValidTo = BusinessDateToday();
        await db.SaveChangesAsync();
        var catalog = new CustomerCatalogService(db);

        var error = await Assert.ThrowsAsync<AppValidationException>(() =>
            catalog.CalculatePricingAsync(new CalculatePricingRequest
            {
                FacilityId = 1,
                UnitTypeId = 1,
                DurationMonths = 1
            }));
        Assert.Equal(CatalogErrors.InvalidPricingRequest.Code, error.Error.Code);
        Assert.Null((await catalog.GetFacilityAsync(1))!.UnitTypes.Single().MonthlyRate);
        Assert.Equal(0m, (await catalog.GetAvailableUnitsAsync(1, 1, null)).Single().MonthlyRate);
    }

    [Fact]
    public async Task CatalogFeeEndingToday_IsExcludedFromPricing()
    {
        await using var db = CreateDbContext();
        await SeedCatalogAsync(db, 350000m, 25000m);
        db.FeeRules.Add(new FeeRule
        {
            Code = "EXPIRED-TAX",
            FeeType = "vat_tax",
            CalculationMethod = "percentage",
            RatePercent = 10m,
            Conditions = "{}",
            ValidFrom = BusinessDateToday().AddDays(-1),
            ValidTo = BusinessDateToday(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var pricing = await new CustomerCatalogService(db).CalculatePricingAsync(new CalculatePricingRequest
        {
            FacilityId = 1,
            UnitTypeId = 1,
            DurationMonths = 1
        });

        Assert.Equal(0m, pricing.TaxAmount);
        Assert.Equal(1375000m, pricing.TotalAmount);
    }

    private static DateOnly BusinessDateToday() => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById(TimezoneConstants.DefaultVietnam)).DateTime);

    private static SelfStorageDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<SelfStorageDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CustomerReservationService CreateReservationService(SelfStorageDbContext db) => new(
        db,
        Options.Create(new ReservationSettings()),
        NullLogger<CustomerReservationService>.Instance);

    private static CreateReservationRequest ReservationRequest(int durationMonths) => new()
    {
        FacilityId = 1,
        UnitTypeId = 1,
        StorageUnitId = 1,
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
        DurationMonths = durationMonths
    };

    private static Promotion PromotionWithRules(params PromotionRule[] rules)
    {
        var now = DateTimeOffset.UtcNow;
        return new Promotion
        {
            Code = "OPERATIONS-SCOPE",
            Name = "Scoped operations promotion",
            DiscountType = PromotionDiscountTypeConstants.Fixed,
            DiscountValue = 100000m,
            ValidFrom = now.AddDays(-1),
            ValidTo = now.AddDays(10),
            IsActive = true,
            CreatedAt = now,
            PromotionRules = rules.ToList()
        };
    }

    private static async Task SeedCatalogAsync(SelfStorageDbContext db, decimal deposit, decimal bookingFee)
    {
        var now = DateTimeOffset.UtcNow;
        db.Users.Add(new User
        {
            Id = 10,
            Email = "consumer@example.com",
            PasswordHash = "hash",
            Status = UserStatusConstants.Active,
            CreatedAt = now,
            CustomerProfile = new CustomerProfile
            {
                UserId = 10,
                FullName = "Customer",
                CreatedAt = now,
                UpdatedAt = now
            }
        });
        db.Facilities.Add(new Facility
        {
            Id = 1,
            Code = "FAC-CONSUMER",
            Name = "Consumer facility",
            AddressLine = "123 Main Street",
            City = "HCM",
            Timezone = TimezoneConstants.DefaultVietnam,
            Status = FacilityStatusConstants.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.UnitTypes.Add(new UnitType
        {
            Id = 1,
            Code = "STANDARD",
            Name = "Standard unit",
            WidthM = 2,
            LengthM = 2,
            HeightM = 2,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.FacilityRates.Add(new FacilityRate
        {
            Id = 1,
            FacilityId = 1,
            UnitTypeId = 1,
            MonthlyRate = 1000000m,
            DepositAmount = deposit,
            BookingFee = bookingFee,
            ValidFrom = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-10),
            ValidTo = DateOnly.FromDateTime(now.UtcDateTime).AddDays(100),
            CreatedAt = now
        });
        db.StorageUnits.Add(new StorageUnit
        {
            Id = 1,
            FacilityId = 1,
            UnitTypeId = 1,
            UnitCode = "CONSUMER-01",
            PhysicalStatus = StorageUnitStatusConstants.Available,
            IsListed = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
    }
}
