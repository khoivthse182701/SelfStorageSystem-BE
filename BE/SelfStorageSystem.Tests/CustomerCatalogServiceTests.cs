using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SelfStorageSystem.Contracts.Customer.Facilities;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;
using Xunit;

namespace SelfStorageSystem.Tests;

public class CustomerCatalogServiceTests
{
    private SelfStorageDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<SelfStorageDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new SelfStorageDbContext(options);
    }

    [Fact]
    public async Task GetFacilities_ReturnsActiveFacilities_WithRealtimeAvailableUnitCount()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var facilityActive = new Facility
        {
            Id = 1,
            Code = "FAC-01",
            Name = "Facility 1",
            AddressLine = "123 Street",
            City = "HCM",
            Latitude = 10.762622m,
            Longitude = 106.660172m,
            Timezone = "Asia/Ho_Chi_Minh",
            OpeningTime = new TimeOnly(8, 0),
            ClosingTime = new TimeOnly(21, 0),
            Status = FacilityStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var facilityInactive = new Facility
        {
            Id = 2,
            Code = "FAC-02",
            Name = "Facility 2",
            AddressLine = "456 Street",
            City = "HCM",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = "inactive",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Facilities.AddRange(facilityActive, facilityInactive);

        var unitType = new UnitType
        {
            Id = 1,
            Code = "STD-01",
            Name = "Standard",
            WidthM = 2, LengthM = 2, HeightM = 2.5m,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.UnitTypes.Add(unitType);

        var area = new FacilityArea
        {
            Id = 1,
            FacilityId = 1,
            Name = "Floor 1",
            Code = "F1",
            AreaType = "Floor",
            MapMetadata = "{}",
            IsActive = true
        };
        db.FacilityAreas.Add(area);

        // 1 Available unit
        var unitAvailable = new StorageUnit
        {
            Id = 1,
            FacilityId = 1,
            AreaId = 1,
            UnitTypeId = 1,
            UnitCode = "U-001",
            FloorLabel = "1",
            PhysicalStatus = StorageUnitStatusConstants.Available,
            IsListed = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        // 1 Occupied unit (excluded)
        var unitOccupied = new StorageUnit
        {
            Id = 2,
            FacilityId = 1,
            AreaId = 1,
            UnitTypeId = 1,
            UnitCode = "U-002",
            FloorLabel = "1",
            PhysicalStatus = StorageUnitStatusConstants.Occupied,
            IsListed = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        // 1 Maintenance unit (excluded)
        var unitMaintenance = new StorageUnit
        {
            Id = 3,
            FacilityId = 1,
            AreaId = 1,
            UnitTypeId = 1,
            UnitCode = "U-003",
            FloorLabel = "1",
            PhysicalStatus = StorageUnitStatusConstants.UnderMaintenance,
            IsListed = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.StorageUnits.AddRange(unitAvailable, unitOccupied, unitMaintenance);
        await db.SaveChangesAsync();

        var service = new CustomerCatalogService(db);

        // Act
        var result = await service.GetFacilitiesAsync();

        // Assert
        Assert.Single(result);
        var f = result[0];
        Assert.Equal(1, f.Id);
        Assert.Equal("FAC-01", f.Code);
        Assert.Equal(10.762622m, f.Latitude);
        Assert.Equal(106.660172m, f.Longitude);
        Assert.Equal(1, f.AvailableUnitCount); // BR-OPS-02: only available unit counted
    }

    [Fact]
    public async Task CalculatePricing_ValidatesDurationAndAppliesPromotionAndDeposit()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var facility = new Facility
        {
            Id = 1,
            Code = "FAC-01",
            Name = "Facility 1",
            AddressLine = "123 Street",
            City = "HCM",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = FacilityStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Facilities.Add(facility);

        var unitType = new UnitType
        {
            Id = 1,
            Code = "STD-01",
            Name = "Standard",
            WidthM = 2, LengthM = 2, HeightM = 2.5m,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.UnitTypes.Add(unitType);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.FacilityRates.Add(new FacilityRate
        {
            Id = 1,
            FacilityId = 1,
            UnitTypeId = 1,
            MonthlyRate = 1000000m,
            ValidFrom = today.AddDays(-10),
            ValidTo = today.AddDays(100),
            CreatedAt = DateTimeOffset.UtcNow
        });

        // Add 10% tax rule and 50000 booking fee rule
        db.FeeRules.AddRange(
            new FeeRule
            {
                Id = 1,
                Code = "BK01",
                FeeType = "booking_fee",
                CalculationMethod = "Fixed",
                Conditions = "{}",
                Amount = 50000m,
                IsActive = true,
                ValidFrom = today.AddDays(-10),
                ValidTo = today.AddDays(100),
                CreatedAt = DateTimeOffset.UtcNow
            },
            new FeeRule
            {
                Id = 2,
                Code = "TAX01",
                FeeType = "vat_tax",
                CalculationMethod = "Percentage",
                Conditions = "{}",
                RatePercent = 10m,
                IsActive = true,
                ValidFrom = today.AddDays(-10),
                ValidTo = today.AddDays(100),
                CreatedAt = DateTimeOffset.UtcNow
            }
        );

        // Add Voucher 10% off with min 3 months
        var now = DateTimeOffset.UtcNow;
        var promo = new Promotion
        {
            Id = 1,
            Code = "SALE10",
            Name = "Summer Sale",
            DiscountType = PromotionDiscountTypeConstants.Percentage,
            DiscountValue = 10m,
            IsActive = true,
            ValidFrom = now.AddDays(-1),
            ValidTo = now.AddDays(10),
            CreatedAt = now
        };
        promo.PromotionRules.Add(new PromotionRule
        {
            Id = 1,
            PromotionId = 1,
            RuleType = "minimum_duration_months",
            Operator = ">=",
            RuleValue = "3"
        });
        db.Promotions.Add(promo);

        await db.SaveChangesAsync();

        var service = new CustomerCatalogService(db);

        // BR-RSV-02: duration < 1 month throws AppValidationException
        var invalidReqMin = new CalculatePricingRequest { FacilityId = 1, UnitTypeId = 1, DurationMonths = 0 };
        await Assert.ThrowsAsync<AppValidationException>(() => service.CalculatePricingAsync(invalidReqMin));

        // BR-RSV-02: duration > 12 months throws AppValidationException
        var invalidReqMax = new CalculatePricingRequest { FacilityId = 1, UnitTypeId = 1, DurationMonths = 13 };
        await Assert.ThrowsAsync<AppValidationException>(() => service.CalculatePricingAsync(invalidReqMax));

        // BR-FIN-04: voucher with duration 2 months (< min 3) throws AppValidationException
        var invalidVoucherReq = new CalculatePricingRequest { FacilityId = 1, UnitTypeId = 1, DurationMonths = 2, VoucherCode = "SALE10" };
        await Assert.ThrowsAsync<AppValidationException>(() => service.CalculatePricingAsync(invalidVoucherReq));

        // Valid request for 3 months with voucher
        var validReq = new CalculatePricingRequest { FacilityId = 1, UnitTypeId = 1, DurationMonths = 3, VoucherCode = "SALE10" };
        var calc = await service.CalculatePricingAsync(validReq);

        // Assertions
        Assert.Equal(1000000m, calc.BaseMonthlyRate);
        Assert.Equal(3, calc.DurationMonths);
        Assert.Equal(3000000m, calc.RentAmount);
        // BR-FIN-01: Security deposit is 100% of 1 month rate
        Assert.Equal(1000000m, calc.SecurityDeposit);
        // Voucher 10% of 3,000,000 = 300,000
        Assert.Equal(300000m, calc.DiscountAmount);
        Assert.Equal(50000m, calc.BookingFee);
        // Taxable = 3,000,000 (rent) - 300,000 (discount) + 50,000 (booking) = 2,750,000 (Deposit is non-taxable)
        // Tax 10% = 275,000
        Assert.Equal(275000m, calc.TaxAmount);
        // Total = 2,700,000 (net rent) + 1,000,000 (deposit) + 50,000 (booking) + 275,000 (tax) = 4,025,000
        Assert.Equal(4025000m, calc.TotalAmount);
    }

    [Fact]
    public async Task GetFacilityMap_ReturnsLayoutAndRealtimeStatuses()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var facility = new Facility
        {
            Id = 1,
            Code = "FAC-01",
            Name = "Facility 1",
            AddressLine = "123 Street",
            City = "HCM",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = FacilityStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Facilities.Add(facility);

        var area = new FacilityArea
        {
            Id = 1,
            FacilityId = 1,
            Name = "Floor 1",
            Code = "F1",
            AreaType = "Floor",
            MapMetadata = "{}",
            IsActive = true
        };
        db.FacilityAreas.Add(area);

        var unit = new StorageUnit
        {
            Id = 1,
            FacilityId = 1,
            AreaId = 1,
            UnitTypeId = 1,
            UnitCode = "U-101",
            PhysicalStatus = StorageUnitStatusConstants.Available,
            IsListed = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.StorageUnits.Add(unit);

        db.UnitMapPositions.Add(new UnitMapPosition
        {
            UnitId = 1,
            AreaId = 1,
            X = 10.5m,
            Y = 20.5m,
            Width = 2.0m,
            Height = 3.0m,
            RotationDegrees = 90m,
            Metadata = "{\"color\":\"#00ff00\"}",
            Unit = unit,
            Area = area
        });

        await db.SaveChangesAsync();

        var service = new CustomerCatalogService(db);

        // Act
        var map = await service.GetFacilityMapAsync(1);

        // Assert
        Assert.NotNull(map);
        Assert.Single(map.Units);
        var u = map.Units[0];
        Assert.Equal(1, u.UnitId);
        Assert.Equal("U-101", u.UnitCode);
        Assert.Equal("Floor 1", u.Layer);
        Assert.Equal(10.5m, u.X);
        Assert.Equal(20.5m, u.Y);
        Assert.Equal(FacilityMapStatusConstants.Available, u.Status);
    }

    [Fact]
    public async Task GetFacilities_StateDrift_WhenReservationConfirmed_UnitIsNotConsideredAvailableEvenIfPhysicalStatusSaysAvailable()
    {
        // Arrange: Unit PhysicalStatus is still 'available' due to delay/drift, but Reservation is Confirmed
        await using var db = CreateInMemoryDbContext();
        var facility = new Facility
        {
            Id = 1,
            Code = "FAC-01",
            Name = "Facility 1",
            AddressLine = "123 Street",
            City = "HCM",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = FacilityStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Facilities.Add(facility);

        var unitType = new UnitType
        {
            Id = 1,
            Code = "STD-01",
            Name = "Standard",
            WidthM = 2, LengthM = 2, HeightM = 2.5m,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.UnitTypes.Add(unitType);

        var area = new FacilityArea
        {
            Id = 1,
            FacilityId = 1,
            Name = "Floor 1",
            Code = "F1",
            AreaType = "Floor",
            MapMetadata = "{}",
            IsActive = true
        };
        db.FacilityAreas.Add(area);

        var unit = new StorageUnit
        {
            Id = 1,
            FacilityId = 1,
            AreaId = 1,
            UnitTypeId = 1,
            UnitCode = "U-DRIFT",
            PhysicalStatus = StorageUnitStatusConstants.Available, // Drift: physical status not updated yet
            IsListed = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.StorageUnits.Add(unit);

        var reservation = new Reservation
        {
            Id = 10,
            CustomerId = 100,
            FacilityId = 1,
            UnitTypeId = 1,
            ReservationCode = "RES-CONFIRMED",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(3),
            Status = ReservationStatusConstants.Confirmed, // Confirmed reservation!
            HoldUntil = DateTimeOffset.UtcNow.AddMinutes(-5),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Reservations.Add(reservation);

        var allocation = new UnitAllocation
        {
            Id = 100,
            StorageUnitId = 1,
            ReservationId = 10,
            AllocationKind = AllocationKindConstants.Reservation,
            AllocationStartDate = reservation.StartDate,
            AllocationEndDate = reservation.EndDate,
            Status = AllocationStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.UnitAllocations.Add(allocation);

        await db.SaveChangesAsync();

        var service = new CustomerCatalogService(db);

        // Act
        var facilities = await service.GetFacilitiesAsync();

        // Assert: Unit is blocked by Confirmed reservation, so AvailableUnitCount must be 0!
        Assert.Single(facilities);
        Assert.Equal(0, facilities[0].AvailableUnitCount);
    }

    [Fact]
    public async Task CalculatePricing_VoucherUsageLimitReached_ThrowsAppValidationException()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var facility = new Facility
        {
            Id = 1,
            Code = "FAC-01",
            Name = "Facility 1",
            AddressLine = "123 Street",
            City = "HCM",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = FacilityStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Facilities.Add(facility);

        var unitType = new UnitType
        {
            Id = 1,
            Code = "STD-01",
            Name = "Standard",
            WidthM = 2, LengthM = 2, HeightM = 2.5m,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.UnitTypes.Add(unitType);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.FacilityRates.Add(new FacilityRate
        {
            Id = 1,
            FacilityId = 1,
            UnitTypeId = 1,
            MonthlyRate = 1000000m,
            ValidFrom = today.AddDays(-10),
            ValidTo = today.AddDays(100),
            CreatedAt = DateTimeOffset.UtcNow
        });

        var now = DateTimeOffset.UtcNow;
        var promo = new Promotion
        {
            Id = 1,
            Code = "LIMITED",
            Name = "Limited Promo",
            DiscountType = PromotionDiscountTypeConstants.Fixed,
            DiscountValue = 100000m,
            UsageLimit = 1, // Only 1 redemption allowed
            IsActive = true,
            ValidFrom = now.AddDays(-1),
            ValidTo = now.AddDays(10),
            CreatedAt = now
        };
        db.Promotions.Add(promo);

        // Already redeemed once
        db.PromotionRedemptions.Add(new PromotionRedemption
        {
            Id = 1,
            PromotionId = 1,
            CustomerId = 999,
            Status = PromotionRedemptionStatusConstants.Applied,
            DiscountAmount = 100000m,
            RedeemedAt = now
        });

        await db.SaveChangesAsync();

        var service = new CustomerCatalogService(db);

        // Act & Assert
        var req = new CalculatePricingRequest { FacilityId = 1, UnitTypeId = 1, DurationMonths = 3, VoucherCode = "LIMITED" };
        await Assert.ThrowsAsync<AppValidationException>(() => service.CalculatePricingAsync(req));
    }
}
