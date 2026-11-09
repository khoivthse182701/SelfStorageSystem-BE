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
    public async Task GetAccessCredentialsAsync_ShouldReturnSuspended_OnTheDayAfterDueDate()
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

        // Access is suspended from 00:00 on the day after the due date.
        var overdueInvoice = new Invoice
        {
            Id = 1,
            AgreementId = agreement.Id,
            InvoiceNo = "INV-001",
            CustomerId = customerId,
            IssueDate = today.AddDays(-10),
            DueDate = today.AddDays(-1),
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
    public async Task GetAccessCredentialsAsync_ShouldNotSuspend_WhenInvoicesAreVoidedOrTransferRelated()
    {
        using var dbContext = CreateInMemoryDbContext();
        long customerId = 1001;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

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
        var unit = new StorageUnit
        {
            Id = 1,
            FacilityId = 1,
            UnitCode = "A-111",
            PhysicalStatus = StorageUnitStatusConstants.Occupied,
            Facility = facility
        };
        var agreement = new RentalAgreement
        {
            Id = 1008,
            AgreementNo = "AGR-1008",
            CustomerId = customerId,
            FacilityId = 1,
            Status = RentalAgreementStatusConstants.Active,
            StartDate = today.AddDays(-10),
            EndDate = today.AddDays(20),
            CheckedInAt = DateTimeOffset.UtcNow.AddDays(-10),
            MonthlyRateSnapshot = 2000000,
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

        // Paid rental invoice
        var paidInvoice = new Invoice
        {
            Id = 1016,
            AgreementId = agreement.Id,
            InvoiceNo = "INV-PAID-01",
            CustomerId = customerId,
            IssueDate = today.AddDays(-10),
            DueDate = today.AddDays(-5),
            Currency = "VND",
            Status = InvoiceStatusConstants.Paid,
            TotalAmount = 2000000m,
            PaidAmount = 2000000m
        };

        // Voided transfer invoice with past due date
        var voidedTransferInvoice = new Invoice
        {
            Id = 9309,
            AgreementId = agreement.Id,
            InvoiceNo = "INV-TRF-20261106-000006",
            CustomerId = customerId,
            IssueDate = today.AddDays(-3),
            DueDate = today.AddDays(-1),
            Currency = "VND",
            Status = InvoiceStatusConstants.Voided,
            BillingPeriod = "TRANSFER:6",
            TotalAmount = 11200000m,
            PaidAmount = 0m
        };

        await dbContext.Facilities.AddAsync(facility);
        await dbContext.StorageUnits.AddAsync(unit);
        await dbContext.RentalAgreements.AddAsync(agreement);
        await dbContext.UnitAllocations.AddAsync(alloc);
        await dbContext.Invoices.AddRangeAsync(paidInvoice, voidedTransferInvoice);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var credentials = await service.GetAccessCredentialsAsync(customerId, agreement.Id, CancellationToken.None);

        // Access must remain Active, must not be suspended by voided or transfer invoice
        Assert.Equal(CredentialStatusConstants.Active, credentials.Status);
        Assert.NotNull(credentials.GateQrToken);
        Assert.Null(credentials.SuspendedReason);
        Assert.NotNull(credentials.KeypadPin);
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
        Assert.Equal(newPin, PinProtectionHelper.Decrypt(updatedCred.DisplayHint));
        Assert.NotEqual(newPin, updatedCred.DisplayHint);
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

        var customer = new CustomerProfile
        {
            UserId = customerId, FullName = "Nguyen Van Customer",
            User = new User { Id = customerId, Email = "handover@example.com", PhoneNumber = "0900000001", PasswordHash = "test", Status = "active" }
        };
        dbContext.CustomerProfiles.Add(customer);
        var unitType = new UnitType
        {
            Id = 1, Code = "S-DRY", Name = "Small Dry Storage", IsActive = true,
            WidthM = 1.5m, LengthM = 2m, HeightM = 2.5m, AreaM2 = 3m, VolumeM3 = 7.5m
        };
        var unit = new StorageUnit
        {
            Id = 1,
            FacilityId = 1,
            UnitCode = "U-101",
            UnitType = unitType,
            FloorLabel = "1",
            ZoneLabel = "A",
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
        Assert.Equal("U-101", result.UnitCode);
        Assert.Equal("Small Dry Storage", result.UnitTypeName);
        Assert.Equal("Central Facility", result.FacilityName);
        Assert.Equal("123 Road", result.FacilityAddress);
        Assert.Equal("Nguyen Van Customer", result.CustomerName);
        Assert.Equal("handover@example.com", result.CustomerEmail);
        Assert.Equal("0900000001", result.CustomerPhone);
        Assert.Equal("EMP-01", result.StaffEmployeeCode);
        Assert.Equal(3m, result.AreaM2);
        Assert.Equal(7.5m, result.VolumeM3);
        Assert.Equal("Nguyen Van Staff", result.HandledByName);
        Assert.NotNull(result.Inspection);
        Assert.Single(result.Inspection.Items);
        Assert.Equal("Door Lock Mechanism", result.Inspection.Items[0].ItemName);
        Assert.Equal("https://storage.example.com/photos/lock.jpg", result.Inspection.Items[0].PhotoUrl);

        await Assert.ThrowsAsync<AppNotFoundException>(() => service.GetHandoverRecordAsync(customerId + 1, agreementId));

        // The report must follow its recorded allocation, even if another unit
        // becomes active later in the rental lifecycle.
        var otherUnit = new StorageUnit { Id = 2, FacilityId = 1, UnitType = unitType, UnitCode = "U-102", PhysicalStatus = "occupied" };
        dbContext.StorageUnits.Add(otherUnit);
        dbContext.UnitAllocations.Add(new UnitAllocation
        {
            Id = 2, AgreementId = agreementId, StorageUnit = otherUnit,
            AllocationKind = "rental", Status = AllocationStatusConstants.Active
        });
        await dbContext.SaveChangesAsync();
        Assert.Equal("U-101", (await service.GetHandoverRecordAsync(customerId, agreementId)).UnitCode);
    }

    [Theory]
    [InlineData("pending_payment", "open", 0, true)]
    [InlineData("pending_payment", "partially_paid", 1000, true)]
    [InlineData("paid", "paid", 8067, false)]
    [InlineData("cancelled", "voided", 0, false)]
    public async Task GetLatestTransferRequestAsync_ReportsServerPaymentStateAndQrContent(string status, string invoiceStatus, int paid, bool hasQr)
    {
        using var db = CreateInMemoryDbContext();
        var type = new UnitType { Id = 1, Code = "S", Name = "Small", IsActive = true };
        var unit = new StorageUnit { Id = 1, UnitType = type, UnitCode = "A-101", PhysicalStatus = "occupied" };
        var agreement = new RentalAgreement { Id = 1, CustomerId = 7, AgreementNo = "AGR-1", Status = "active" };
        db.RentalAgreements.Add(agreement);
        db.UnitTransferRequests.Add(new UnitTransferRequest
        {
            Id = 9, Agreement = agreement, FromUnit = unit, RequestedUnitType = type,
            RequestedBy = 7, RequestedFacilityId = 1, Reason = "Transfer", Status = status,
            TotalPaidAmount = 8067, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        db.Invoices.Add(new Invoice
        {
            Id = 49333, InvoiceNo = "INV-TRF-9", CustomerId = 7, AgreementId = 1,
            BillingPeriod = "TRANSFER:9", Currency = "VND", Status = invoiceStatus, TotalAmount = 8067, PaidAmount = paid
        });
        await db.SaveChangesAsync();
        var result = await CreateService(db).GetLatestTransferRequestAsync(7, 1);
        Assert.NotNull(result);
        Assert.Equal(status, result.Status);
        Assert.Equal(paid, result.PaidAmount);
        Assert.Equal("DH49333", result.PaymentContent);
        Assert.Equal(hasQr, result.PaymentQrUrl != null);
        if (hasQr)
        {
            Assert.Contains("des=DH49333", result.PaymentQrUrl);
            Assert.Contains($"amount={8067 - paid}", result.PaymentQrUrl);
            Assert.DoesNotContain("memo=", result.PaymentQrUrl);
        }
        await Assert.ThrowsAsync<AppNotFoundException>(() => CreateService(db).GetLatestTransferRequestAsync(8, 1));
    }

    [Fact]
    public async Task GetHandoverRecordAsync_MissingRecord_DoesNotInventInspectionOrSignatures()
    {
        using var db = CreateInMemoryDbContext();
        var facility = new Facility { Id = 1, Code = "F1", Name = "Facility", AddressLine = "123 Road", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var staff = new EmployeeProfile { UserId = 501, EmployeeCode = "EMP-01", FullName = "Staff", EmploymentStatus = "active" };
        var agreement = new RentalAgreement { Id = 301, CustomerId = 1001, AgreementNo = "AGR-301", Facility = facility, Status = "active", CheckedInAt = DateTimeOffset.UtcNow };
        var unit = new StorageUnit { Id = 1, Facility = facility, UnitCode = "A-101", PhysicalStatus = "occupied" };
        db.RentalAgreements.Add(agreement);
        db.UnitAllocations.Add(new UnitAllocation { Agreement = agreement, StorageUnit = unit, AllocationKind = "rental", Status = "active" });
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment
        {
            Employee = staff, Facility = facility, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<AppNotFoundException>(() => CreateService(db).GetHandoverRecordAsync(1001, 301));
        Assert.Empty(await db.HandoverRecords.ToListAsync());
        Assert.Empty(await db.Inspections.ToListAsync());
        Assert.Empty(await db.InspectionItems.ToListAsync());
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

    [Fact]
    public async Task GetTransferPreviewAsync_CrossFacilityAndLargerUnit_CalculatesPriceDiffAndCleaningFee()
    {
        using var dbContext = CreateInMemoryDbContext();
        long customerId = 1001;
        long agreementId = 501;

        var facility1 = new Facility { Id = 1, Name = "Facility A", Status = "active", Code = "FA", AddressLine = "123 St", City = "HCM", Timezone = "Asia/Ho_Chi_Minh" };
        var facility2 = new Facility { Id = 2, Name = "Facility B", Status = "active", Code = "FB", AddressLine = "456 St", City = "HN", Timezone = "Asia/Ho_Chi_Minh" };

        var unitTypeSmall = new UnitType { Id = 10, Code = "SM", Name = "Small Locker 1m2" };
        var unitTypeLarge = new UnitType { Id = 20, Code = "LG", Name = "Large Unit 10m2" };

        var currentUnit = new StorageUnit { Id = 101, FacilityId = 1, UnitTypeId = 10, UnitCode = "A-101", PhysicalStatus = "occupied" };
        var targetUnit = new StorageUnit { Id = 201, FacilityId = 2, UnitTypeId = 20, UnitCode = "B-201", PhysicalStatus = "available" };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var agreement = new RentalAgreement
        {
            Id = agreementId,
            AgreementNo = "AGR-TRF-01",
            CustomerId = customerId,
            FacilityId = 1,
            Status = RentalAgreementStatusConstants.Active,
            StartDate = today.AddDays(-10),
            EndDate = today.AddDays(20),
            MonthlyRateSnapshot = 1000m,
            DepositBalance = 1000m,
            Facility = facility1
        };

        var unitAllocation = new UnitAllocation
        {
            Id = 1,
            AgreementId = agreementId,
            StorageUnitId = 101,
            AllocationKind = "rental",
            Status = AllocationStatusConstants.Active,
            StorageUnit = currentUnit
        };

        var targetRate = new FacilityRate
        {
            Id = 1,
            FacilityId = 2,
            UnitTypeId = 20,
            MonthlyRate = 4000m,
            DepositAmount = 4000m,
            ValidFrom = today.AddDays(-30)
        };

        await dbContext.Facilities.AddRangeAsync(facility1, facility2);
        await dbContext.UnitTypes.AddRangeAsync(unitTypeSmall, unitTypeLarge);
        await dbContext.StorageUnits.AddRangeAsync(currentUnit, targetUnit);
        await dbContext.RentalAgreements.AddAsync(agreement);
        await dbContext.UnitAllocations.AddAsync(unitAllocation);
        await dbContext.FacilityRates.AddAsync(targetRate);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, rentalSettings: new RentalSettings { CrossFacilityCleaningFee = 50000m, MaxTransfersPerAgreement = 2 });

        var preview = await service.GetTransferPreviewAsync(
            customerId, agreementId, requestedFacilityId: 2, requestedUnitTypeId: 20, moveOutDate: today, CancellationToken.None);

        Assert.NotNull(preview);
        Assert.True(preview.CanTransfer);
        Assert.True(preview.IsCrossFacility);
        Assert.Equal(50000m, preview.CleaningFee);
        Assert.Equal(3000m, preview.DiffDepositAmount);
        Assert.True(preview.DiffRentAmount > 0);
        Assert.Equal(0, preview.TransferCount);
        Assert.Equal(2, preview.MaxTransfers);
    }

    [Fact]
    public async Task RequestUnitTransferAsync_ExceedsMaxTransferLimit_ThrowsConflictException()
    {
        using var dbContext = CreateInMemoryDbContext();
        long customerId = 1001;
        long agreementId = 502;

        var facility = new Facility { Id = 1, Name = "Facility A", Status = "active", Code = "FA", AddressLine = "123 St", City = "HCM", Timezone = "Asia/Ho_Chi_Minh" };
        var unitType = new UnitType { Id = 10, Code = "SM", Name = "Small Locker" };
        var unit = new StorageUnit { Id = 101, FacilityId = 1, UnitTypeId = 10, UnitCode = "A-101", PhysicalStatus = "occupied" };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var agreement = new RentalAgreement
        {
            Id = agreementId,
            AgreementNo = "AGR-TRF-02",
            CustomerId = customerId,
            FacilityId = 1,
            Status = RentalAgreementStatusConstants.Active,
            StartDate = today.AddDays(-10),
            EndDate = today.AddDays(20),
            MonthlyRateSnapshot = 1000m,
            DepositBalance = 1000m,
            Facility = facility
        };

        var unitAllocation = new UnitAllocation
        {
            Id = 1,
            AgreementId = agreementId,
            StorageUnitId = 101,
            AllocationKind = "rental",
            Status = AllocationStatusConstants.Active,
            StorageUnit = unit
        };

        var tr1 = new UnitTransferRequest
        {
            Id = 1,
            AgreementId = agreementId,
            FromUnitId = 101,
            RequestedUnitTypeId = 10,
            RequestedFacilityId = 1,
            Status = TransferRequestStatusConstants.Completed,
            CompletedAt = DateTimeOffset.UtcNow.AddDays(-5),
            RequestedBy = customerId,
            Reason = "First move"
        };
        var tr2 = new UnitTransferRequest
        {
            Id = 2,
            AgreementId = agreementId,
            FromUnitId = 101,
            RequestedUnitTypeId = 10,
            RequestedFacilityId = 1,
            Status = TransferRequestStatusConstants.Completed,
            CompletedAt = DateTimeOffset.UtcNow.AddDays(-2),
            RequestedBy = customerId,
            Reason = "Second move"
        };

        await dbContext.Facilities.AddAsync(facility);
        await dbContext.UnitTypes.AddAsync(unitType);
        await dbContext.StorageUnits.AddAsync(unit);
        await dbContext.RentalAgreements.AddAsync(agreement);
        await dbContext.UnitAllocations.AddAsync(unitAllocation);
        await dbContext.UnitTransferRequests.AddRangeAsync(tr1, tr2);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, rentalSettings: new RentalSettings { MaxTransfersPerAgreement = 2 });

        var request = new CreateTransferRequest(
            RequestedUnitTypeId: 10,
            RequestedFacilityId: 1,
            MoveOutDate: today,
            MoveInDate: today,
            Reason: "Third attempt"
        );

        var ex = await Assert.ThrowsAsync<AppConflictException>(
            () => service.RequestUnitTransferAsync(customerId, agreementId, request, CancellationToken.None));

        Assert.Equal(RentalErrors.TransferLimitReached.Code, ex.Error.Code);
    }

    [Fact]
    public async Task UnlockUnitAsync_ShouldSucceed_WhenSecretDigestIsNull_AndDisplayHintIsEncrypted()
    {
        using var dbContext = CreateInMemoryDbContext();
        long customerId = 1001;
        long agreementId = 302;
        string correctPin = "654321";

        var facility = new Facility
        {
            Id = 2,
            Name = "Cau Giay Storage Facility 2",
            Code = "FAC-CG2",
            AddressLine = "123 Cau Giay",
            Timezone = "Asia/Ho_Chi_Minh",
            City = "Hanoi",
            Status = "active",
            OpeningTime = new TimeOnly(0, 0),
            ClosingTime = new TimeOnly(23, 59)
        };

        var unit = new StorageUnit
        {
            Id = 2,
            FacilityId = 2,
            UnitCode = "CG-B202",
            PhysicalStatus = StorageUnitStatusConstants.Occupied
        };

        var agreement = new RentalAgreement
        {
            Id = agreementId,
            AgreementNo = "AGR-302",
            CustomerId = customerId,
            FacilityId = 2,
            Status = RentalAgreementStatusConstants.Active,
            CheckedInAt = DateTimeOffset.UtcNow.AddDays(-2),
            Facility = facility
        };

        var unitAllocation = new UnitAllocation
        {
            Id = 2,
            AgreementId = agreementId,
            StorageUnitId = 2,
            AllocationKind = "rental",
            Status = AllocationStatusConstants.Active,
            StorageUnit = unit
        };

        var credential = new AccessCredential
        {
            Id = 2,
            AgreementId = agreementId,
            CredentialType = CredentialTypeConstants.Pin,
            SecretDigest = null,
            DisplayHint = PinProtectionHelper.Encrypt(correctPin),
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
        Assert.Equal("CG-B202", result.UnitCode);
    }

    [Fact]
    public async Task RequestMoveOutAsync_ShouldCapReasonTo250Chars_WhenBankDetailsAndReasonAreLong()
    {
        using var dbContext = CreateInMemoryDbContext();
        long customerId = 1001;
        long agreementId = 303;

        var agreement = new RentalAgreement
        {
            Id = agreementId,
            AgreementNo = "AGR-303",
            CustomerId = customerId,
            FacilityId = 1,
            Status = RentalAgreementStatusConstants.Active,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)),
            MonthlyRateSnapshot = 1000000,
            DepositSnapshot = 1000000,
            DepositBalance = 1000000,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await dbContext.RentalAgreements.AddAsync(agreement);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var longReason = new string('A', 200);
        var longBank = "Ngan Hang Thuong Mai Co Phan Ngoai Thuong Viet Nam Vietcombank";
        var request = new RequestMoveOutRequest(
            RequestedMoveOutDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            Reason: longReason,
            BankName: longBank,
            BankAccountNumber: "9999888877776666",
            BankAccountHolder: "NGUYEN VAN LONG NAME HOLDER FOR TESTING PURPOSE"
        );

        var result = await service.RequestMoveOutAsync(customerId, agreementId, request, CancellationToken.None);

        Assert.NotNull(result);
        var savedMoveOut = await dbContext.MoveOutRequests.FirstOrDefaultAsync(m => m.AgreementId == agreementId);
        Assert.NotNull(savedMoveOut);
        Assert.NotNull(savedMoveOut.Reason);
        Assert.True(savedMoveOut.Reason.Length <= 250, $"Reason length was {savedMoveOut.Reason.Length}, expected <= 250");
    }

    [Fact]
    public async Task CancelInvoiceAsync_WhenInvoiceIsForReservation_ShouldCancelReservationAndReleaseStorageUnit()
    {
        using var dbContext = CreateInMemoryDbContext();
        long customerId = 2001;
        long facilityId = 1;

        var facility = new Facility
        {
            Id = facilityId,
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
            Id = 88,
            FacilityId = facilityId,
            UnitCode = "CG-A88",
            PhysicalStatus = StorageUnitStatusConstants.Reserved
        };

        var reservation = new Reservation
        {
            Id = 501,
            CustomerId = customerId,
            FacilityId = facilityId,
            UnitTypeId = 1,
            ReservationCode = "RSV-TEST-501",
            Status = ReservationStatusConstants.Pending,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            MonthlyRateSnapshot = 1000000,
            DepositSnapshot = 1000000,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var allocation = new UnitAllocation
        {
            Id = 501,
            ReservationId = reservation.Id,
            StorageUnitId = unit.Id,
            AllocationKind = AllocationKindConstants.Reservation,
            Status = AllocationStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            StorageUnit = unit
        };

        var invoice = new Invoice
        {
            Id = 901,
            InvoiceNo = "INV-TEST-901",
            CustomerId = customerId,
            ReservationId = reservation.Id,
            TotalAmount = 2000000,
            PaidAmount = 0,
            Status = InvoiceStatusConstants.Open,
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Currency = "VND",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Reservation = reservation
        };

        await dbContext.Facilities.AddAsync(facility);
        await dbContext.StorageUnits.AddAsync(unit);
        await dbContext.Reservations.AddAsync(reservation);
        await dbContext.UnitAllocations.AddAsync(allocation);
        await dbContext.Invoices.AddAsync(invoice);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        // Act: Customer cancels the reservation invoice
        var result = await service.CancelInvoiceAsync(customerId, invoice.Id);

        // Assert
        Assert.True(result);
        Assert.Equal(InvoiceStatusConstants.Voided, invoice.Status);
        Assert.Equal(ReservationStatusConstants.Cancelled, reservation.Status);
        Assert.Equal(AllocationStatusConstants.Cancelled, allocation.Status);
        Assert.Equal(StorageUnitStatusConstants.Available, unit.PhysicalStatus);
    }

    [Fact]
    public async Task RequestMoveOutAsync_ShouldPreserveVietnameseUtf8Characters_WithoutUnicodeEscaping()
    {
        using var dbContext = CreateInMemoryDbContext();
        long customerId = 1002;
        long agreementId = 304;

        var agreement = new RentalAgreement
        {
            Id = agreementId,
            AgreementNo = "AGR-304",
            CustomerId = customerId,
            FacilityId = 1,
            Status = RentalAgreementStatusConstants.Active,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)),
            MonthlyRateSnapshot = 1000000,
            DepositSnapshot = 1000000,
            DepositBalance = 1000000,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await dbContext.RentalAgreements.AddAsync(agreement);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var vietnameseBank = "Ngân hàng Thương mại Cổ phần Ngoại thương Việt Nam";
        var vietnameseHolder = "NGUYỄN VĂN ĐỨC";
        var vietnameseReason = "Hết hạn hợp đồng và chuyển văn phòng về cơ sở mới";

        var request = new RequestMoveOutRequest(
            RequestedMoveOutDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            Reason: vietnameseReason,
            BankName: vietnameseBank,
            BankAccountNumber: "01234567890123",
            BankAccountHolder: vietnameseHolder
        );

        var result = await service.RequestMoveOutAsync(customerId, agreementId, request, CancellationToken.None);

        Assert.NotNull(result);
        var savedMoveOut = await dbContext.MoveOutRequests.FirstOrDefaultAsync(m => m.AgreementId == agreementId);
        Assert.NotNull(savedMoveOut);
        Assert.NotNull(savedMoveOut.Reason);
        Assert.True(savedMoveOut.Reason.Length <= 250, $"Reason length was {savedMoveOut.Reason.Length}, expected <= 250");
        Assert.DoesNotContain("\\u", savedMoveOut.Reason);
        Assert.Contains(vietnameseHolder, savedMoveOut.Reason);
        Assert.Contains(vietnameseBank, savedMoveOut.Reason);
    }
}
