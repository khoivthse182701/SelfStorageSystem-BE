using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Customer.Payments;
using SelfStorageSystem.Contracts.Customer.Reservations;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;
using Xunit;

namespace SelfStorageSystem.Tests;

public class CustomerReservationAndPaymentTests
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
    public void ReservationSettings_DefaultValues_ShouldMatchBusinessRules()
    {
        var settings = new ReservationSettings();

        // BR-RSV-01: Maximum hold duration is 15 minutes
        Assert.Equal(15, settings.HoldDurationMinutes);

        // Buffer Grace Period
        Assert.Equal(3, settings.HoldGracePeriodMinutes);

        // BR-RSV-02: Rental duration between 1 and 12 months
        Assert.Equal(1, settings.MinDurationMonths);
        Assert.Equal(12, settings.MaxDurationMonths);
    }

    [Fact]
    public void PaymentSettings_SePayAndVietQrConfig_ShouldHaveValidDefaultsAndApiKey()
    {
        var paymentSettings = new PaymentSettings
        {
            SePay = new SePayConfig
            {
                ApiKey = "Admin@123",
                InvoicePrefix = "DH"
            },
            VietQr = new VietQrConfig
            {
                BaseUrl = "https://vietqr.app/img",
                BankCode = "MBBank",
                AccountNo = "0817495759",
                AccountName = "VO THAI HOANG KHOI",
                Template = "compact",
                ShowInfo = true
            }
        };

        Assert.Equal("Admin@123", paymentSettings.SePay.ApiKey);
        Assert.Equal("DH", paymentSettings.SePay.InvoicePrefix);
        Assert.Equal("MBBank", paymentSettings.VietQr.BankCode);
        Assert.Equal("0817495759", paymentSettings.VietQr.AccountNo);
        Assert.Equal("VO THAI HOANG KHOI", paymentSettings.VietQr.AccountName);
        Assert.Equal("compact", paymentSettings.VietQr.Template);
        Assert.True(paymentSettings.VietQr.ShowInfo);
        Assert.Equal("https://vietqr.app/img", paymentSettings.VietQr.BaseUrl);
    }

    [Fact]
    public async Task CreateCheckoutAsync_VietQr_ShouldGenerateValidCheckoutResponseAndQrUrl()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var user = new User { Id = 1, Email = "checkout@example.com", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 1, FullName = "John Doe", User = user };
        var facility = new Facility { Id = 1, Code = "F01", Name = "Central Facility", AddressLine = "123 Main St", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var unitType = new UnitType { Id = 1, Name = "Locker", Code = "LCK", IsActive = true };
        var reservation = new Reservation
        {
            Id = 50,
            CustomerId = 1,
            FacilityId = 1,
            UnitTypeId = 1,
            FacilityRateId = 1,
            ReservationCode = "RSV-TEST-CHK",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            Status = "pending",
            HoldUntil = DateTimeOffset.UtcNow.AddMinutes(15)
        };
        var invoice = new Invoice
        {
            CustomerId = 1,
            ReservationId = 50,
            InvoiceNo = "INV-TEST-CHK",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Currency = "VND",
            TotalAmount = 1000000m,
            PaidAmount = 0,
            Status = "open"
        };

        dbContext.Users.Add(user);
        dbContext.CustomerProfiles.Add(customer);
        dbContext.Facilities.Add(facility);
        dbContext.UnitTypes.Add(unitType);
        dbContext.Reservations.Add(reservation);
        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync();

        var paymentSettings = new PaymentSettings
        {
            SePay = new SePayConfig { ApiKey = "Admin@123", InvoicePrefix = "DH" },
            VietQr = new VietQrConfig
            {
                BaseUrl = "https://vietqr.app/img",
                BankCode = "MBBank",
                AccountNo = "0817495759",
                AccountName = "VO THAI HOANG KHOI",
                Template = "compact",
                ShowInfo = true
            }
        };

        var service = new CustomerPaymentService(
            dbContext,
            Mock.Of<IEmailService>(),
            Options.Create(paymentSettings),
            NullLogger<CustomerPaymentService>.Instance);

        // Act
        var result = await service.CreateCheckoutAsync(1, new CreateCheckoutRequest { ReservationId = 50 });

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.VietQr);
        Assert.Equal(1000000m, result.Amount);
        Assert.Equal($"DH{invoice.Id}", result.VietQr.TransferContent);
        Assert.Contains("vietqr.app/img", result.VietQr.QrImageUrl);
        Assert.Contains("bank=MBBank", result.VietQr.QrImageUrl);
        Assert.Contains("acc=0817495759", result.VietQr.QrImageUrl);
        Assert.Contains("holder=VO%20THAI%20HOANG%20KHOI", result.VietQr.QrImageUrl);
        Assert.Contains($"memo=DH{invoice.Id}", result.VietQr.QrImageUrl);
    }

    [Theory]
    [InlineData("Transfer DH108 Customer payment", "DH", 108)]
    [InlineData("SEPAY DH999 rental payment", "DH", 999)]
    [InlineData("DH12345", "DH", 12345)]
    [InlineData("Invoice HD55 Customer Name", "HD", 55)]
    public void SePayContentRegex_ValidInvoiceSyntax_ShouldExtractInvoiceId(string content, string prefix, long expectedInvoiceId)
    {
        var regexPattern = $@"{Regex.Escape(prefix)}(\d+)";
        var match = Regex.Match(content, regexPattern, RegexOptions.IgnoreCase);
        Assert.True(match.Success);
        Assert.True(long.TryParse(match.Groups[1].Value, out var invoiceId));
        Assert.Equal(expectedInvoiceId, invoiceId);
    }

    [Theory]
    [InlineData("Random transfer memo 12345", "DH")]
    [InlineData("No invoice code here", "DH")]
    public void SePayContentRegex_InvalidSyntax_ShouldNotMatch(string content, string prefix)
    {
        var regexPattern = $@"{Regex.Escape(prefix)}(\d+)";
        var match = Regex.Match(content, regexPattern, RegexOptions.IgnoreCase);
        Assert.False(match.Success);
    }

    // =========================================================================
    // 4 EDGE CASE UNIT TESTS
    // =========================================================================

    [Fact]
    public async Task Test1_SePayWebhook_AmountSmallerThanInvoice_ShouldNotConfirmReservation()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var user = new User { Id = 10, Email = "test1@example.com", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Customer A", User = user };
        var facility = new Facility { Id = 1, Code = "F01", Name = "Facility A", AddressLine = "123 St", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var unitType = new UnitType { Id = 1, Name = "Locker", Code = "LCK", IsActive = true };
        var reservation = new Reservation
        {
            Id = 101,
            CustomerId = 10,
            FacilityId = 1,
            UnitTypeId = 1,
            FacilityRateId = 1,
            ReservationCode = "RSV-TEST-001",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            Status = "pending",
            HoldUntil = DateTimeOffset.UtcNow.AddMinutes(15)
        };
        var invoice = new Invoice
        {
            CustomerId = 10,
            ReservationId = 101,
            InvoiceNo = "INV-TEST-555",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Currency = "VND",
            TotalAmount = 2850000m,
            PaidAmount = 0,
            Status = "open"
        };

        dbContext.Users.Add(user);
        dbContext.CustomerProfiles.Add(customer);
        dbContext.Facilities.Add(facility);
        dbContext.UnitTypes.Add(unitType);
        dbContext.Reservations.Add(reservation);
        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync();

        var paymentService = new CustomerPaymentService(
            dbContext,
            Mock.Of<IEmailService>(),
            Options.Create(new PaymentSettings { SePay = new SePayConfig { ApiKey = "Admin@123", InvoicePrefix = "DH" } }),
            NullLogger<CustomerPaymentService>.Instance);

        // Act: Customer transfers less than required invoice balance (10,000 VND instead of 2,850,000 VND)
        var payload = new SePayWebhookPayload
        {
            Id = 9999,
            Content = $"Payment DH{invoice.Id} Customer A",
            TransferAmount = 10000m
        };

        var result = await paymentService.ProcessSePayWebhookAsync(payload);

        // Assert
        Assert.True(result);
        var updatedReservation = await dbContext.Reservations.FindAsync(reservation.Id);
        Assert.NotNull(updatedReservation);
        // Reservation remains pending, NOT confirmed
        Assert.Equal("pending", updatedReservation.Status);
        Assert.Null(updatedReservation.ConfirmedAt);

        // Partial payment recorded
        var payments = await dbContext.Payments.Where(p => p.TargetInvoiceId == invoice.Id).ToListAsync();
        Assert.Single(payments);
        Assert.Equal(10000m, payments[0].Amount);
    }

    [Fact]
    public async Task Test2_SePayWebhook_DuplicateTransactionCall_ShouldBeIdempotent()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var user = new User { Id = 20, Email = "test2@example.com", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 20, FullName = "Customer B", User = user };
        var facility = new Facility { Id = 2, Code = "F02", Name = "Facility B", AddressLine = "456 St", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var unitType = new UnitType { Id = 2, Name = "Standard", Code = "STD", IsActive = true };
        var reservation = new Reservation
        {
            Id = 102,
            CustomerId = 20,
            FacilityId = 2,
            UnitTypeId = 2,
            FacilityRateId = 2,
            ReservationCode = "RSV-TEST-002",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            Status = "pending",
            HoldUntil = DateTimeOffset.UtcNow.AddMinutes(15)
        };
        var invoice = new Invoice
        {
            CustomerId = 20,
            ReservationId = 102,
            InvoiceNo = "INV-TEST-556",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Currency = "VND",
            TotalAmount = 1500000m,
            PaidAmount = 0,
            Status = "open"
        };

        dbContext.Users.Add(user);
        dbContext.CustomerProfiles.Add(customer);
        dbContext.Facilities.Add(facility);
        dbContext.UnitTypes.Add(unitType);
        dbContext.Reservations.Add(reservation);
        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync();

        var paymentService = new CustomerPaymentService(
            dbContext,
            Mock.Of<IEmailService>(),
            Options.Create(new PaymentSettings { SePay = new SePayConfig { ApiKey = "Admin@123", InvoicePrefix = "DH" } }),
            NullLogger<CustomerPaymentService>.Instance);

        var payload = new SePayWebhookPayload
        {
            Id = 8888,
            ReferenceCode = "SEPAY_TXN_UNIQUE_8888",
            Content = $"Transfer DH{invoice.Id}",
            TransferAmount = 1500000m
        };

        // Act 1: Initial successful processing
        var firstCallResult = await paymentService.ProcessSePayWebhookAsync(payload);
        Assert.True(firstCallResult);

        // Act 2: Webhook replay simulation
        var secondCallResult = await paymentService.ProcessSePayWebhookAsync(payload);

        // Assert: Both calls return true (200 OK) without duplicate payment record creation
        Assert.True(secondCallResult);
        var paymentCount = await dbContext.Payments
            .CountAsync(p => p.Provider == "SePay" && p.ProviderTransactionId == "SEPAY_TXN_UNIQUE_8888");
        Assert.Equal(1, paymentCount);
    }

    [Fact]
    public async Task Test3_UserACallsUserBReservation_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var userA = new CustomerProfile { UserId = 100, FullName = "User A" };
        var userB = new CustomerProfile { UserId = 200, FullName = "User B" };
        var facility = new Facility { Id = 3, Code = "F03", Name = "Facility C", AddressLine = "789 St", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var unitType = new UnitType { Id = 3, Name = "Large", Code = "LRG", IsActive = true };

        // Reservation belongs to User B (CustomerId = 200)
        var reservationB = new Reservation
        {
            Id = 300,
            CustomerId = 200,
            FacilityId = 3,
            UnitTypeId = 3,
            FacilityRateId = 3,
            ReservationCode = "RSV-USER-B",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            Status = "pending",
            HoldUntil = DateTimeOffset.UtcNow.AddMinutes(15)
        };

        dbContext.CustomerProfiles.AddRange(userA, userB);
        dbContext.Facilities.Add(facility);
        dbContext.UnitTypes.Add(unitType);
        dbContext.Reservations.Add(reservationB);
        await dbContext.SaveChangesAsync();

        var reservationService = new CustomerReservationService(
            dbContext,
            Options.Create(new ReservationSettings()),
            NullLogger<CustomerReservationService>.Instance);

        // Act & Assert: User A tries to access User B's reservation details
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            reservationService.GetReservationDetailAsync(customerId: 100, reservationId: 300));

        Assert.Contains("permission", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test4_SePayWebhook_WhenReservationAlreadyExpired_ShouldHandleGracefullyWithoutCrashing500()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var user = new User { Id = 30, Email = "test4@example.com", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 30, FullName = "Customer C", User = user };
        var facility = new Facility { Id = 4, Code = "F04", Name = "Facility D", AddressLine = "321 St", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var unitType = new UnitType { Id = 4, Name = "Container", Code = "CNT", IsActive = true };

        // Reservation has expired
        var reservation = new Reservation
        {
            Id = 104,
            CustomerId = 30,
            FacilityId = 4,
            UnitTypeId = 4,
            FacilityRateId = 4,
            ReservationCode = "RSV-EXPIRED-104",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            Status = "expired",
            HoldUntil = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        var invoice = new Invoice
        {
            CustomerId = 30,
            ReservationId = 104,
            InvoiceNo = "INV-TEST-557",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Currency = "VND",
            TotalAmount = 2000000m,
            PaidAmount = 0,
            Status = "voided"
        };

        dbContext.Users.Add(user);
        dbContext.CustomerProfiles.Add(customer);
        dbContext.Facilities.Add(facility);
        dbContext.UnitTypes.Add(unitType);
        dbContext.Reservations.Add(reservation);
        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync();

        var paymentService = new CustomerPaymentService(
            dbContext,
            Mock.Of<IEmailService>(),
            Options.Create(new PaymentSettings { SePay = new SePayConfig { ApiKey = "Admin@123", InvoicePrefix = "DH" } }),
            NullLogger<CustomerPaymentService>.Instance);

        // Act: Late payment received after expiration
        var payload = new SePayWebhookPayload
        {
            Id = 7777,
            ReferenceCode = "SEPAY_LATE_7777",
            Content = $"Payment DH{invoice.Id}",
            TransferAmount = 2000000m
        };

        // Assert: Successfully processed without 500 error
        var result = await paymentService.ProcessSePayWebhookAsync(payload);
        Assert.True(result);

        // Reservation remains expired
        var checkReservation = await dbContext.Reservations.FindAsync(104L);
        Assert.NotNull(checkReservation);
        Assert.Equal("expired", checkReservation.Status);

        // Payment recorded with late payment flag
        var payment = await dbContext.Payments
            .FirstOrDefaultAsync(p => p.ProviderTransactionId == "SEPAY_LATE_7777");
        Assert.NotNull(payment);
        Assert.Equal("succeeded", payment.Status);
        Assert.Contains("Late payment", payment.Metadata);
    }

    [Fact]
    public async Task Test5_SePayWebhook_FullPayment_ShouldConfirmReservationAndMarkInvoicePaid()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var user = new User { Id = 40, Email = "test5@example.com", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 40, FullName = "Customer Full Pay", User = user };
        var facility = new Facility { Id = 5, Code = "F05", Name = "Facility E", AddressLine = "555 St", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var unitType = new UnitType { Id = 5, Name = "Medium", Code = "MED", IsActive = true };
        var reservation = new Reservation
        {
            Id = 105,
            CustomerId = 40,
            FacilityId = 5,
            UnitTypeId = 5,
            FacilityRateId = 5,
            ReservationCode = "RSV-TEST-FULLPAY",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            Status = "pending",
            HoldUntil = DateTimeOffset.UtcNow.AddMinutes(15)
        };
        var invoice = new Invoice
        {
            CustomerId = 40,
            ReservationId = 105,
            InvoiceNo = "INV-TEST-FULLPAY",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Currency = "VND",
            TotalAmount = 2500000m,
            PaidAmount = 0,
            Status = "open"
        };

        dbContext.Users.Add(user);
        dbContext.CustomerProfiles.Add(customer);
        dbContext.Facilities.Add(facility);
        dbContext.UnitTypes.Add(unitType);
        dbContext.Reservations.Add(reservation);
        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync();

        var paymentService = new CustomerPaymentService(
            dbContext,
            Mock.Of<IEmailService>(),
            Options.Create(new PaymentSettings { SePay = new SePayConfig { ApiKey = "Admin@123", InvoicePrefix = "DH" } }),
            NullLogger<CustomerPaymentService>.Instance);

        var payload = new SePayWebhookPayload
        {
            Id = 9901,
            ReferenceCode = "SEPAY_FULL_9901",
            Content = $"Thanh toan DH{invoice.Id} noi dung chuyen khoan",
            TransferAmount = 2500000m,
            TransferType = "in"
        };

        // Act
        var result = await paymentService.ProcessSePayWebhookAsync(payload);

        // Assert
        Assert.True(result);

        var updatedReservation = await dbContext.Reservations.FindAsync(105L);
        Assert.NotNull(updatedReservation);
        Assert.Equal("confirmed", updatedReservation.Status);
        Assert.NotNull(updatedReservation.ConfirmedAt);

        var updatedInvoice = await dbContext.Invoices.FindAsync(invoice.Id);
        Assert.NotNull(updatedInvoice);
        Assert.Equal("paid", updatedInvoice.Status);
        Assert.Equal(2500000m, updatedInvoice.PaidAmount);

        var allocation = await dbContext.PaymentAllocations.FirstOrDefaultAsync(pa => pa.InvoiceId == invoice.Id);
        Assert.NotNull(allocation);
        Assert.Equal(2500000m, allocation.AllocatedAmount);
    }

    [Fact]
    public async Task Test6_SePayWebhook_OutgoingOrNonPositiveTransfer_ShouldBeIgnored()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var paymentService = new CustomerPaymentService(
            dbContext,
            Mock.Of<IEmailService>(),
            Options.Create(new PaymentSettings { SePay = new SePayConfig { ApiKey = "Admin@123", InvoicePrefix = "DH" } }),
            NullLogger<CustomerPaymentService>.Instance);

        // Act 1: Non-positive transfer amount
        var zeroPayload = new SePayWebhookPayload
        {
            Id = 9902,
            Content = "DH100 Transfer",
            TransferAmount = 0m
        };
        var zeroResult = await paymentService.ProcessSePayWebhookAsync(zeroPayload);

        // Act 2: Outgoing account debit
        var outPayload = new SePayWebhookPayload
        {
            Id = 9903,
            Content = "DH100 Transfer",
            TransferAmount = 500000m,
            TransferType = "out"
        };
        var outResult = await paymentService.ProcessSePayWebhookAsync(outPayload);

        // Assert
        Assert.False(zeroResult);
        Assert.False(outResult);
    }

    [Fact]
    public async Task Test7_CancelReservationAsync_ShouldReleaseUnitToAvailableAndCancelAllocation()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var user = new User { Id = 50, Email = "test7@example.com", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 50, FullName = "Customer Cancel", User = user };
        var facility = new Facility { Id = 6, Code = "F06", Name = "Facility F", AddressLine = "666 St", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var unitType = new UnitType { Id = 6, Name = "Small", Code = "SML", IsActive = true };
        var unit = new StorageUnit
        {
            Id = 601,
            FacilityId = 6,
            UnitTypeId = 6,
            UnitCode = "U-601",
            PhysicalStatus = "reserved"
        };
        var reservation = new Reservation
        {
            Id = 106,
            CustomerId = 50,
            FacilityId = 6,
            UnitTypeId = 6,
            FacilityRateId = 6,
            ReservationCode = "RSV-TEST-CANCEL",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            Status = "pending",
            HoldUntil = DateTimeOffset.UtcNow.AddMinutes(15)
        };
        var allocation = new UnitAllocation
        {
            Id = 1,
            StorageUnitId = 601,
            ReservationId = 106,
            AllocationKind = "reservation",
            AllocationStartDate = reservation.StartDate,
            AllocationEndDate = reservation.EndDate,
            Status = "active"
        };

        dbContext.Users.Add(user);
        dbContext.CustomerProfiles.Add(customer);
        dbContext.Facilities.Add(facility);
        dbContext.UnitTypes.Add(unitType);
        dbContext.StorageUnits.Add(unit);
        dbContext.Reservations.Add(reservation);
        dbContext.UnitAllocations.Add(allocation);
        await dbContext.SaveChangesAsync();

        var reservationService = new CustomerReservationService(
            dbContext,
            Options.Create(new ReservationSettings()),
            NullLogger<CustomerReservationService>.Instance);

        // Act
        var result = await reservationService.CancelReservationAsync(customerId: 50, reservationId: 106, reason: "Customer changed mind");

        // Assert
        Assert.True(result);

        var updatedReservation = await dbContext.Reservations.FindAsync(106L);
        Assert.NotNull(updatedReservation);
        Assert.Equal("cancelled", updatedReservation.Status);

        var updatedUnit = await dbContext.StorageUnits.FindAsync(601L);
        Assert.NotNull(updatedUnit);
        Assert.Equal("available", updatedUnit.PhysicalStatus);

        var updatedAllocation = await dbContext.UnitAllocations.FindAsync(1L);
        Assert.NotNull(updatedAllocation);
        Assert.Equal("cancelled", updatedAllocation.Status);
    }

    [Fact]
    public async Task Test8_CreateCheckoutAsync_ReusedPendingPayment_PreventsDuplicatePendingSpam()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var user = new User { Id = 60, Email = "test8@example.com", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 60, FullName = "Customer Spam Check", User = user };
        var facility = new Facility { Id = 7, Code = "F07", Name = "Facility G", AddressLine = "777 St", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var unitType = new UnitType { Id = 7, Name = "Standard", Code = "STD2", IsActive = true };
        var reservation = new Reservation
        {
            Id = 107,
            CustomerId = 60,
            FacilityId = 7,
            UnitTypeId = 7,
            FacilityRateId = 7,
            ReservationCode = "RSV-TEST-IDEMP",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            Status = "pending",
            HoldUntil = DateTimeOffset.UtcNow.AddMinutes(15)
        };
        var invoice = new Invoice
        {
            CustomerId = 60,
            ReservationId = 107,
            InvoiceNo = "INV-TEST-IDEMP",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Currency = "VND",
            TotalAmount = 1800000m,
            PaidAmount = 0,
            Status = "open"
        };

        dbContext.Users.Add(user);
        dbContext.CustomerProfiles.Add(customer);
        dbContext.Facilities.Add(facility);
        dbContext.UnitTypes.Add(unitType);
        dbContext.Reservations.Add(reservation);
        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync();

        var paymentService = new CustomerPaymentService(
            dbContext,
            Mock.Of<IEmailService>(),
            Options.Create(new PaymentSettings
            {
                SePay = new SePayConfig { ApiKey = "Admin@123", InvoicePrefix = "DH" },
                VietQr = new VietQrConfig { BankCode = "MBBank", AccountNo = "123456", AccountName = "TEST HOLDER" }
            }),
            NullLogger<CustomerPaymentService>.Instance);

        // Act: User opens checkout twice
        var firstResult = await paymentService.CreateCheckoutAsync(60, new CreateCheckoutRequest { ReservationId = 107 });
        var secondResult = await paymentService.CreateCheckoutAsync(60, new CreateCheckoutRequest { ReservationId = 107 });

        // Assert: Same PaymentId reused, exactly 1 payment record in database
        Assert.Equal(firstResult.PaymentId, secondResult.PaymentId);
        var pendingCount = await dbContext.Payments.CountAsync(p => p.TargetInvoiceId == invoice.Id);
        Assert.Equal(1, pendingCount);
    }
}
