using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Customer.Rentals;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;
using Xunit;

namespace SelfStorageSystem.Tests;

public class CustomerRentalServiceTests
{
    private SelfStorageDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<SelfStorageDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new SelfStorageDbContext(options);
    }

    private IOptions<JwtSettings> CreateJwtSettings()
    {
        return Options.Create(new JwtSettings
        {
            Secret = "SuperSecretKeyForTestingPurposesMustBeLongEnough123456!",
            Issuer = "SelfStorageSystem",
            Audience = "SelfStorageSystemClient",
            AccessTokenMinutes = 60
        });
    }

    private CustomerRentalService CreateService(
        SelfStorageDbContext dbContext,
        IMemoryCache? cache = null,
        RentalSettings? rentalSettings = null)
    {
        var memoryCache = cache ?? new MemoryCache(new MemoryCacheOptions());
        var settingsOptions = Options.Create(rentalSettings ?? new RentalSettings());
        return new CustomerRentalService(
            dbContext,
            memoryCache,
            CreateJwtSettings(),
            NullLogger<CustomerRentalService>.Instance,
            settingsOptions);
    }

    [Fact]
    public async Task GetMyRentalsAsync_ShouldReturnActiveAndExpiringRentalsOnly()
    {
        using var dbContext = CreateInMemoryDbContext();
        long customerId = 1001;
        long otherCustomerId = 1002;

        var facility = new Facility
        {
            Id = 1,
            Code = "FAC-01",
            Name = "Central Facility",
            AddressLine = "123 Storage Road",
            City = "Ho Chi Minh City",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = "active"
        };
        var unitType = new UnitType
        {
            Id = 1,
            Code = "MEDIUM",
            Name = "Medium Unit",
            WidthM = 2.0m,
            LengthM = 2.5m,
            HeightM = 2.4m,
            AreaM2 = 5.0m,
            VolumeM3 = 12.0m,
            IsActive = true
        };
        var unit1 = new StorageUnit
        {
            Id = 1,
            FacilityId = 1,
            UnitTypeId = 1,
            UnitCode = "U-101",
            FloorLabel = "1",
            ZoneLabel = "A",
            PhysicalStatus = StorageUnitStatusConstants.Occupied,
            UnitType = unitType,
            Facility = facility
        };
        var unit2 = new StorageUnit
        {
            Id = 2,
            FacilityId = 1,
            UnitTypeId = 1,
            UnitCode = "U-102",
            FloorLabel = "1",
            ZoneLabel = "A",
            PhysicalStatus = StorageUnitStatusConstants.Occupied,
            UnitType = unitType,
            Facility = facility
        };
        var unit3 = new StorageUnit
        {
            Id = 3,
            FacilityId = 1,
            UnitTypeId = 1,
            UnitCode = "U-103",
            FloorLabel = "2",
            ZoneLabel = "B",
            PhysicalStatus = StorageUnitStatusConstants.Occupied,
            UnitType = unitType,
            Facility = facility
        };

        // Active agreement
        var activeAgreement = new RentalAgreement
        {
            Id = 101,
            AgreementNo = "AGR-001",
            CustomerId = customerId,
            FacilityId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(2)),
            Status = RentalAgreementStatusConstants.Active,
            MonthlyRateSnapshot = 1500000m,
            DepositBalance = 1500000m,
            Facility = facility
        };
        var alloc1 = new UnitAllocation
        {
            Id = 1,
            AgreementId = activeAgreement.Id,
            StorageUnitId = unit1.Id,
            AllocationKind = "rental",
            Status = AllocationStatusConstants.Active,
            StorageUnit = unit1
        };

        // Expiring soon agreement
        var expiringAgreement = new RentalAgreement
        {
            Id = 102,
            AgreementNo = "AGR-002",
            CustomerId = customerId,
            FacilityId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-2)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            Status = RentalAgreementStatusConstants.ExpiringSoon,
            MonthlyRateSnapshot = 1500000m,
            DepositBalance = 1500000m,
            Facility = facility
        };
        var alloc2 = new UnitAllocation
        {
            Id = 2,
            AgreementId = expiringAgreement.Id,
            StorageUnitId = unit2.Id,
            AllocationKind = "rental",
            Status = AllocationStatusConstants.Active,
            StorageUnit = unit2
        };

        // Terminated agreement (should be excluded)
        var terminatedAgreement = new RentalAgreement
        {
            Id = 103,
            AgreementNo = "AGR-003",
            CustomerId = customerId,
            FacilityId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-5)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            Status = RentalAgreementStatusConstants.Terminated,
            MonthlyRateSnapshot = 1500000m,
            DepositBalance = 0m,
            Facility = facility
        };

        // Other customer agreement
        var otherAgreement = new RentalAgreement
        {
            Id = 104,
            AgreementNo = "AGR-004",
            CustomerId = otherCustomerId,
            FacilityId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            Status = RentalAgreementStatusConstants.Active,
            MonthlyRateSnapshot = 1500000m,
            DepositBalance = 1500000m,
            Facility = facility
        };

        await dbContext.Facilities.AddAsync(facility);
        await dbContext.UnitTypes.AddAsync(unitType);
        await dbContext.StorageUnits.AddRangeAsync(unit1, unit2, unit3);
        await dbContext.RentalAgreements.AddRangeAsync(activeAgreement, expiringAgreement, terminatedAgreement, otherAgreement);
        await dbContext.UnitAllocations.AddRangeAsync(alloc1, alloc2);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.GetMyRentalsAsync(customerId, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.AgreementNo == "AGR-001" && r.Status == RentalAgreementStatusConstants.Active);
        Assert.Contains(result, r => r.AgreementNo == "AGR-002" && r.Status == RentalAgreementStatusConstants.ExpiringSoon);
        Assert.DoesNotContain(result, r => r.AgreementNo == "AGR-003");
        Assert.DoesNotContain(result, r => r.AgreementNo == "AGR-004");
    }

    [Fact]
    public async Task GetAccessCredentialsAsync_ShouldThrowException_WhenNotCheckedIn()
    {
        using var dbContext = CreateInMemoryDbContext();
        long customerId = 1001;

        var facility = new Facility
        {
            Id = 1,
            Code = "FAC-01",
            Name = "Central Facility",
            AddressLine = "123 Road",
            City = "HCM",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = "active"
        };

        var agreement = new RentalAgreement
        {
            Id = 201,
            AgreementNo = "AGR-001",
            CustomerId = customerId,
            FacilityId = 1,
            Facility = facility,
            Status = RentalAgreementStatusConstants.Active,
            CheckedInAt = null // BR-RSV-04: Not checked in
        };

        await dbContext.Facilities.AddAsync(facility);
        await dbContext.RentalAgreements.AddAsync(agreement);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var ex = await Assert.ThrowsAsync<AppValidationException>(
            () => service.GetAccessCredentialsAsync(customerId, agreement.Id, CancellationToken.None));

        Assert.Equal(RentalErrors.AgreementNotCheckedIn.Code, ex.Error.Code);
    }

    [Fact]
    public async Task GetAccessCredentialsAsync_ShouldReturnSuspended_WhenOverdueExceedsOneDay()
    {
        using var dbContext = CreateInMemoryDbContext();
        long customerId = 1001;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var facility = new Facility
        {
            Id = 1,
            Code = "FAC-01",
            Name = "Central Facility",
            AddressLine = "123 Storage Road",
            City = "Ho Chi Minh City",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = "active"
        };
        var unit = new StorageUnit
        {
            Id = 1,
            FacilityId = 1,
            UnitCode = "U-101",
            PhysicalStatus = StorageUnitStatusConstants.Occupied
        };
        var agreement = new RentalAgreement
        {
            Id = 202,
            AgreementNo = "AGR-002",
            CustomerId = customerId,
            FacilityId = 1,
            Status = RentalAgreementStatusConstants.Active,
            CheckedInAt = DateTimeOffset.UtcNow.AddDays(-10),
            Facility = facility
        };
        var alloc = new UnitAllocation
        {
            Id = 1,
            AgreementId = agreement.Id,
            StorageUnitId = unit.Id,
            AllocationKind = "rental",
            Status = AllocationStatusConstants.Active,
            StorageUnit = unit
        };

        // Overdue unpaid invoice > 1 day
        var overdueInvoice = new Invoice
        {
            Id = 1,
            AgreementId = agreement.Id,
            InvoiceNo = "INV-001",
            CustomerId = customerId,
            IssueDate = today.AddDays(-10),
            DueDate = today.AddDays(-2), // 2 days overdue
            Currency = "VND",
            Status = InvoiceStatusConstants.Open,
            TotalAmount = 1500000m,
            PaidAmount = 0m
        };

        await dbContext.Facilities.AddAsync(facility);
        await dbContext.StorageUnits.AddAsync(unit);
        await dbContext.RentalAgreements.AddAsync(agreement);
        await dbContext.UnitAllocations.AddAsync(alloc);
        await dbContext.Invoices.AddAsync(overdueInvoice);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var credentials = await service.GetAccessCredentialsAsync(customerId, agreement.Id, CancellationToken.None);

        // BR-REN-03: Suspended
        Assert.Equal(CredentialStatusConstants.Suspended, credentials.Status);
        Assert.Null(credentials.GateQrToken);
        Assert.NotNull(credentials.SuspendedReason);
        Assert.Equal(RentalSuspensionReasons.OverdueDebtExceeded, credentials.SuspendedReason);
    }

    [Fact]
    public async Task GetAccessCredentialsAsync_ShouldReturnActiveCredentialsAnd120sToken_WhenValid()
    {
        using var dbContext = CreateInMemoryDbContext();
        long customerId = 1001;
        var facility = new Facility
        {
            Id = 1,
            Code = "FAC-01",
            Name = "Main Branch",
            AddressLine = "123 Storage Road",
            City = "Ho Chi Minh City",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = "active"
        };
        var unit = new StorageUnit
        {
            Id = 1,
            FacilityId = 1,
            UnitCode = "A-12",
            PhysicalStatus = StorageUnitStatusConstants.Occupied,
            Facility = facility
        };
        var agreement = new RentalAgreement
        {
            Id = 203,
            AgreementNo = "AGR-003",
            CustomerId = customerId,
            FacilityId = 1,
            Status = RentalAgreementStatusConstants.Active,
            CheckedInAt = DateTimeOffset.UtcNow.AddDays(-5),
            Facility = facility
        };
        var alloc = new UnitAllocation
        {
            Id = 1,
            AgreementId = agreement.Id,
            StorageUnitId = unit.Id,
            AllocationKind = "rental",
            Status = AllocationStatusConstants.Active,
            StorageUnit = unit
        };

        var hashedPin = BCrypt.Net.BCrypt.HashPassword("847291");
        var credential = new AccessCredential
        {
            Id = 1,
            AgreementId = agreement.Id,
            CredentialType = CredentialTypeConstants.Pin,
            SecretDigest = hashedPin,
            DisplayHint = "847291",
            IssuedAt = DateTimeOffset.UtcNow.AddDays(-5),
            Status = CredentialStatusConstants.Active
        };

        await dbContext.Facilities.AddAsync(facility);
        await dbContext.StorageUnits.AddAsync(unit);
        await dbContext.RentalAgreements.AddAsync(agreement);
        await dbContext.UnitAllocations.AddAsync(alloc);
        await dbContext.AccessCredentials.AddAsync(credential);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.GetAccessCredentialsAsync(customerId, agreement.Id, CancellationToken.None);

        Assert.Equal(CredentialStatusConstants.Active, result.Status);
        Assert.Equal("847291", result.KeypadPin);
        Assert.NotNull(result.GateQrToken);
        Assert.Equal(120, result.QrExpiresInSeconds); // 120s TTL for clock skew resilience
    }

    [Fact]
    public async Task GetAccessCredentialsAsync_ShouldThrowOutsideBusinessHours_WhenOutsideOperatingHours()
    {
        using var dbContext = CreateInMemoryDbContext();
        long customerId = 1001;

        // Facility open only during impossible time range (e.g. 03:00 to 03:01 AM)
        var facility = new Facility
        {
            Id = 1,
            Code = "FAC-01",
            Name = "Strict Hours Branch",
            AddressLine = "123 Storage Road",
            City = "Ho Chi Minh City",
            Timezone = "Asia/Ho_Chi_Minh",
            OpeningTime = new TimeOnly(3, 0),
            ClosingTime = new TimeOnly(3, 1),
            Status = "active"
        };
        var agreement = new RentalAgreement
        {
            Id = 209,
            AgreementNo = "AGR-009",
            CustomerId = customerId,
            FacilityId = 1,
            Status = RentalAgreementStatusConstants.Active,
            CheckedInAt = DateTimeOffset.UtcNow.AddDays(-5),
            Facility = facility
        };

        await dbContext.Facilities.AddAsync(facility);
        await dbContext.RentalAgreements.AddAsync(agreement);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, rentalSettings: new RentalSettings { EnforceOperatingHours = true });

        var ex = await Assert.ThrowsAsync<AppConflictException>(
            () => service.GetAccessCredentialsAsync(customerId, agreement.Id, CancellationToken.None));

        Assert.Equal(RentalErrors.OutsideBusinessHours.Code, ex.Error.Code);
    }

    [Fact]
    public async Task GetAccessCredentialsAsync_ShouldAllowAccess_WhenOutsideOperatingHours_AndEnforceOperatingHoursIsFalse()
    {
        using var dbContext = CreateInMemoryDbContext();
        long customerId = 1001;

        var facility = new Facility
        {
            Id = 1,
            Code = "FAC-01",
            Name = "Strict Hours Branch",
            AddressLine = "123 Storage Road",
            City = "Ho Chi Minh City",
            Timezone = "Asia/Ho_Chi_Minh",
            OpeningTime = new TimeOnly(3, 0),
            ClosingTime = new TimeOnly(3, 1),
            Status = "active"
        };
        var agreement = new RentalAgreement
        {
            Id = 209,
            AgreementNo = "AGR-009",
            CustomerId = customerId,
            FacilityId = 1,
            Status = RentalAgreementStatusConstants.Active,
            CheckedInAt = DateTimeOffset.UtcNow.AddDays(-5),
            Facility = facility
        };

        var initialPin = "847291";
        var credential = new AccessCredential
        {
            Id = 1,
            AgreementId = agreement.Id,
            CredentialType = CredentialTypeConstants.Pin,
            SecretDigest = BCrypt.Net.BCrypt.HashPassword(initialPin),
            DisplayHint = initialPin,
            IssuedAt = DateTimeOffset.UtcNow,
            Status = CredentialStatusConstants.Active
        };

        await dbContext.Facilities.AddAsync(facility);
        await dbContext.RentalAgreements.AddAsync(agreement);
        await dbContext.AccessCredentials.AddAsync(credential);
        await dbContext.SaveChangesAsync();

        // EnforceOperatingHours is false by default in RentalSettings
        var service = CreateService(dbContext, rentalSettings: new RentalSettings { EnforceOperatingHours = false });

        var result = await service.GetAccessCredentialsAsync(customerId, agreement.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(CredentialStatusConstants.Active, result.Status);
        Assert.Equal(initialPin, result.KeypadPin);
    }

    [Theory]
    [InlineData("12345")] // Less than 6 digits
    [InlineData("1234567")] // More than 6 digits
    [InlineData("abcdef")] // Non digits
    [InlineData("123456")] // Sequential
    [InlineData("654321")] // Reverse sequential
    [InlineData("111111")] // Repeated digits
    [InlineData("000000")] // Repeated digits
    public async Task ChangePinAsync_ShouldThrowException_WhenPinIsInvalidOrWeak(string newPin)
    {
        using var dbContext = CreateInMemoryDbContext();
        long customerId = 1001;

        var agreement = new RentalAgreement
        {
            Id = 204,
            AgreementNo = "AGR-004",
            CustomerId = customerId,
            FacilityId = 1,
            Status = RentalAgreementStatusConstants.Active,
            CheckedInAt = DateTimeOffset.UtcNow
        };

        await dbContext.RentalAgreements.AddAsync(agreement);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var request = new ChangePinRequest
        {
            CurrentPin = "847291",
            NewPin = newPin
        };

        await Assert.ThrowsAsync<AppValidationException>(
            () => service.ChangePinAsync(customerId, agreement.Id, request, CancellationToken.None));
    }

    [Fact]
    public async Task ChangePinAsync_ShouldLockOut_WhenFailedAttemptsExceedLimit()
    {
        using var dbContext = CreateInMemoryDbContext();
        long customerId = 1001;
        long agreementId = 208;

        var agreement = new RentalAgreement
        {
            Id = agreementId,
            AgreementNo = "AGR-008",
            CustomerId = customerId,
            FacilityId = 1,
            Status = RentalAgreementStatusConstants.Active,
            CheckedInAt = DateTimeOffset.UtcNow
        };

        var initialPin = "847291";
        var currentHash = BCrypt.Net.BCrypt.HashPassword(initialPin);
        var credential = new AccessCredential
        {
            Id = 1,
            AgreementId = agreement.Id,
            CredentialType = CredentialTypeConstants.Pin,
            SecretDigest = currentHash,
            DisplayHint = initialPin,
            IssuedAt = DateTimeOffset.UtcNow,
            Status = CredentialStatusConstants.Active
        };

        await dbContext.RentalAgreements.AddAsync(agreement);
        await dbContext.AccessCredentials.AddAsync(credential);
        await dbContext.SaveChangesAsync();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(dbContext, cache);

        // Fail 5 times
        for (int i = 0; i < 5; i++)
        {
            var wrongRequest = new ChangePinRequest
            {
                CurrentPin = "999999",
                NewPin = "846201"
            };
            await Assert.ThrowsAsync<AppValidationException>(
                () => service.ChangePinAsync(customerId, agreementId, wrongRequest, CancellationToken.None));
        }

        // 6th attempt: should be blocked by anti-brute force lockout
        var lockedRequest = new ChangePinRequest
        {
            CurrentPin = initialPin, // Even with correct PIN, must be locked out
            NewPin = "846201"
        };

        var ex = await Assert.ThrowsAsync<AppConflictException>(
            () => service.ChangePinAsync(customerId, agreementId, lockedRequest, CancellationToken.None));

        Assert.Equal(RentalErrors.TooManyFailedPinAttempts.Code, ex.Error.Code);
    }

    [Fact]
    public async Task ChangePinAsync_ShouldSucceedAndReturnPendingSyncStatus_WhenValid()
    {
        using var dbContext = CreateInMemoryDbContext();
        long customerId = 1001;

        var agreement = new RentalAgreement
        {
            Id = 205,
            AgreementNo = "AGR-005",
            CustomerId = customerId,
            FacilityId = 1,
            Status = RentalAgreementStatusConstants.Active,
            CheckedInAt = DateTimeOffset.UtcNow
        };

        var initialPin = "847291";
        var currentHash = BCrypt.Net.BCrypt.HashPassword(initialPin);
        var credential = new AccessCredential
        {
            Id = 1,
            AgreementId = agreement.Id,
            CredentialType = CredentialTypeConstants.Pin,
            SecretDigest = currentHash,
            DisplayHint = initialPin,
            IssuedAt = DateTimeOffset.UtcNow,
            Status = CredentialStatusConstants.Active
        };

        await dbContext.RentalAgreements.AddAsync(agreement);
        await dbContext.AccessCredentials.AddAsync(credential);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var newPin = "938172";
        var request = new ChangePinRequest
        {
            CurrentPin = initialPin,
            NewPin = newPin
        };

        var result = await service.ChangePinAsync(customerId, agreement.Id, request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(PinSyncStatusConstants.Pending, result.SyncStatus);
        Assert.Equal(60, result.EstimatedSyncSeconds);

        var updatedCred = await dbContext.AccessCredentials.FirstOrDefaultAsync(c => c.AgreementId == agreement.Id);
        Assert.NotNull(updatedCred);
        Assert.True(BCrypt.Net.BCrypt.Verify(newPin, updatedCred.SecretDigest));
        Assert.Equal(newPin, updatedCred.DisplayHint);
        Assert.Equal(CredentialStatusConstants.Pending, updatedCred.Status);
    }

    [Fact]
    public async Task GetHandoverRecordAsync_ShouldReturnRecordWithInspectionAndChecklist()
    {
        using var dbContext = CreateInMemoryDbContext();
        long customerId = 1001;
        long agreementId = 301;

        var facility = new Facility
        {
            Id = 1,
            Code = "FAC-01",
            Name = "Central Facility",
            AddressLine = "123 Road",
            City = "HCM",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = "active"
        };

        var staff = new EmployeeProfile
        {
            UserId = 501,
            EmployeeCode = "EMP-01",
            FullName = "Nguyen Van Staff",
            EmploymentStatus = "active"
        };

        var unit = new StorageUnit
        {
            Id = 1,
            FacilityId = 1,
            UnitCode = "U-101",
            PhysicalStatus = StorageUnitStatusConstants.Occupied,
            Facility = facility
        };

        var agreement = new RentalAgreement
        {
            Id = agreementId,
            AgreementNo = "AGR-001",
            CustomerId = customerId,
            FacilityId = 1,
            Facility = facility,
            Status = RentalAgreementStatusConstants.Active,
            CheckedInAt = DateTimeOffset.UtcNow
        };

        var unitAllocation = new UnitAllocation
        {
            Id = 1,
            AgreementId = agreementId,
            StorageUnitId = 1,
            AllocationKind = "rental",
            Status = AllocationStatusConstants.Active,
            StorageUnit = unit,
            Agreement = agreement
        };

        var inspection = new Inspection
        {
            Id = 1,
            FacilityId = 1,
            AgreementId = agreementId,
            InspectedBy = staff.UserId,
            InspectionType = "check_in",
            Status = "passed",
            OverallCondition = "clean",
            Summary = "Unit is in excellent condition.",
            InspectedAt = DateTimeOffset.UtcNow,
            InspectionItems =
            [
                new InspectionItem
                {
                    Id = 1,
                    InspectionId = 1,
                    ItemName = "Door Lock Mechanism",
                    Condition = "good",
                    Notes = "Working properly",
                    PhotoUrl = "https://storage.example.com/photos/lock.jpg",
                    ChargeAmount = 0m
                }
            ]
        };

        var handover = new HandoverRecord
        {
            Id = 1,
            AgreementId = agreementId,
            UnitAllocationId = unitAllocation.Id,
            InspectionId = inspection.Id,
            HandledBy = staff.UserId,
            HandoverType = HandoverTypeConstants.CheckIn,
            CustomerSignatureRef = "https://storage.example.com/signatures/cust1.png",
            StaffSignatureRef = "https://storage.example.com/signatures/staff1.png",
            CustomerSignedAt = DateTimeOffset.UtcNow,
            StaffSignedAt = DateTimeOffset.UtcNow,
            Notes = "Everything handed over smoothly.",
            Agreement = agreement,
            HandledByNavigation = staff,
            Inspection = inspection,
            UnitAllocation = unitAllocation
        };

        await dbContext.Facilities.AddAsync(facility);
        await dbContext.StorageUnits.AddAsync(unit);
        await dbContext.EmployeeProfiles.AddAsync(staff);
        await dbContext.RentalAgreements.AddAsync(agreement);
        await dbContext.UnitAllocations.AddAsync(unitAllocation);
        await dbContext.Inspections.AddAsync(inspection);
        await dbContext.HandoverRecords.AddAsync(handover);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.GetHandoverRecordAsync(customerId, agreementId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(HandoverTypeConstants.CheckIn, result.HandoverType);
        Assert.Equal("Nguyen Van Staff", result.HandledByName);
        Assert.NotNull(result.Inspection);
        Assert.Single(result.Inspection.Items);
        Assert.Equal("Door Lock Mechanism", result.Inspection.Items[0].ItemName);
        Assert.Equal("https://storage.example.com/photos/lock.jpg", result.Inspection.Items[0].PhotoUrl);
    }

    [Fact]
    public async Task UnlockUnitAsync_ShouldSucceed_WhenPinIsValidAndUnitCheckedIn()
    {
        using var dbContext = CreateInMemoryDbContext();
        long customerId = 1001;
        long agreementId = 301;
        string correctPin = "938210";

        var facility = new Facility
        {
            Id = 1,
            Name = "Cau Giay Storage Facility",
            Code = "FAC-CG",
            AddressLine = "123 Cau Giay",
            Timezone = "Asia/Ho_Chi_Minh",
            City = "Hanoi",
            Status = "active",
            OpeningTime = new TimeOnly(0, 0),
            ClosingTime = new TimeOnly(23, 59)
        };

        var unit = new StorageUnit
        {
            Id = 1,
            FacilityId = 1,
            UnitCode = "CG-A101",
            PhysicalStatus = StorageUnitStatusConstants.Occupied
        };

        var agreement = new RentalAgreement
        {
            Id = agreementId,
            AgreementNo = "AGR-301",
            CustomerId = customerId,
            FacilityId = 1,
            Status = RentalAgreementStatusConstants.Active,
            CheckedInAt = DateTimeOffset.UtcNow.AddDays(-2),
            Facility = facility
        };

        var unitAllocation = new UnitAllocation
        {
            Id = 1,
            AgreementId = agreementId,
            StorageUnitId = 1,
            AllocationKind = "rental",
            Status = AllocationStatusConstants.Active,
            StorageUnit = unit
        };

        var credential = new AccessCredential
        {
            Id = 1,
            AgreementId = agreementId,
            CredentialType = CredentialTypeConstants.Pin,
            SecretDigest = BCrypt.Net.BCrypt.HashPassword(correctPin),
            DisplayHint = correctPin,
            Status = CredentialStatusConstants.Active,
            IssuedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await dbContext.Facilities.AddAsync(facility);
        await dbContext.StorageUnits.AddAsync(unit);
        await dbContext.RentalAgreements.AddAsync(agreement);
        await dbContext.UnitAllocations.AddAsync(unitAllocation);
        await dbContext.AccessCredentials.AddAsync(credential);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var request = new UnlockStorageUnitRequest { Pin = correctPin };
        var result = await service.UnlockUnitAsync(customerId, agreementId, request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("CG-A101", result.UnitCode);
        Assert.Equal("Cau Giay Storage Facility", result.FacilityName);
        Assert.Equal(30, result.RelockAfterSeconds);
    }

    [Fact]
    public async Task UnlockUnitAsync_ShouldThrowValidationException_WhenPinIsIncorrect()
    {
        using var dbContext = CreateInMemoryDbContext();
        long customerId = 1001;
        long agreementId = 302;
        string correctPin = "938210";

        var facility = new Facility
        {
            Id = 1,
            Name = "Cau Giay Storage Facility",
            Code = "FAC-CG",
            AddressLine = "123 Cau Giay",
            Timezone = "Asia/Ho_Chi_Minh",
            City = "Hanoi",
            Status = "active",
            OpeningTime = new TimeOnly(0, 0),
            ClosingTime = new TimeOnly(23, 59)
        };

        var unit = new StorageUnit
        {
            Id = 1,
            FacilityId = 1,
            UnitCode = "CG-A102",
            PhysicalStatus = StorageUnitStatusConstants.Occupied
        };

        var agreement = new RentalAgreement
        {
            Id = agreementId,
            AgreementNo = "AGR-302",
            CustomerId = customerId,
            FacilityId = 1,
            Status = RentalAgreementStatusConstants.Active,
            CheckedInAt = DateTimeOffset.UtcNow.AddDays(-2),
            Facility = facility
        };

        var unitAllocation = new UnitAllocation
        {
            Id = 1,
            AgreementId = agreementId,
            StorageUnitId = 1,
            AllocationKind = "rental",
            Status = AllocationStatusConstants.Active,
            StorageUnit = unit
        };

        var credential = new AccessCredential
        {
            Id = 1,
            AgreementId = agreementId,
            CredentialType = CredentialTypeConstants.Pin,
            SecretDigest = BCrypt.Net.BCrypt.HashPassword(correctPin),
            DisplayHint = correctPin,
            Status = CredentialStatusConstants.Active,
            IssuedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await dbContext.Facilities.AddAsync(facility);
        await dbContext.StorageUnits.AddAsync(unit);
        await dbContext.RentalAgreements.AddAsync(agreement);
        await dbContext.UnitAllocations.AddAsync(unitAllocation);
        await dbContext.AccessCredentials.AddAsync(credential);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var request = new UnlockStorageUnitRequest { Pin = "111222" };
        var ex = await Assert.ThrowsAsync<AppValidationException>(
            () => service.UnlockUnitAsync(customerId, agreementId, request, CancellationToken.None));

        Assert.Equal(RentalErrors.IncorrectPin.Code, ex.Error.Code);
    }
}
