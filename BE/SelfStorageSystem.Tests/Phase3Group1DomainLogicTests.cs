using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Customer.Facilities;
using SelfStorageSystem.Contracts.Customer.Payments;
using SelfStorageSystem.Contracts.Customer.Reservations;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;

namespace SelfStorageSystem.Tests;

public class Phase3Group1DomainLogicTests
{
    private static SelfStorageDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<SelfStorageDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    [Fact]
    public void QrTokenService_ValidatesSignature_Expiry_AndTokenType()
    {
        var qr = new QrTokenService("UnitTestingQrSigningSecret_AtLeast32CharsLong!");
        var token = qr.IssueToken("checkin", 105, 88, 12, 1, TimeSpan.FromMinutes(15));

        Assert.StartsWith("v1.", token);
        Assert.True(qr.TryVerifyToken(token, "checkin", out var payload));
        Assert.Equal(105L, payload!.RsvId);
        Assert.Equal(88L, payload.AgrId);
        Assert.Equal(12L, payload.CustId);
        Assert.Equal(1L, payload.FacId);

        // Wrong expected type
        Assert.False(qr.TryVerifyToken(token, "gate", out _));

        // Tampered payload
        var parts = token.Split('.');
        var tampered = $"{parts[0]}.{parts[1]}A.{parts[2]}";
        Assert.False(qr.TryVerifyToken(tampered, "checkin", out _));

        // Expired token
        var expired = qr.IssueToken("checkin", 105, null, 12, 1, TimeSpan.FromSeconds(-5));
        Assert.False(qr.TryVerifyToken(expired, "checkin", out _));

        // Unsigned legacy token
        Assert.False(qr.TryVerifyToken("CHECKIN:RSV-105:12:1", "checkin", out _));
    }

    [Fact]
    public async Task PricingEngine_EvaluatesAllPromotionRulesAndCaps_AndRejectsPastDateInFacilityTimezone()
    {
        await using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        db.Facilities.Add(new Facility { Id = 1, Code = "F1", Name = "F1", AddressLine = "Addr", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = FacilityStatusConstants.Active });
        db.UnitTypes.Add(new UnitType { Id = 1, Code = "U1", Name = "Type 1", IsActive = true });
        db.FacilityRates.Add(new FacilityRate { Id = 1, FacilityId = 1, UnitTypeId = 1, MonthlyRate = 1_000_000m, DepositAmount = 1_000_000m, BookingFee = 50_000m, ValidFrom = today.AddDays(-10) });
        db.FeeRules.Add(new FeeRule { Id = 1, Code = "VAT_10", FeeType = FeeRuleTypeConstants.Tax, CalculationMethod = "percentage", RatePercent = 10m, Conditions = "{}", IsActive = true, ValidFrom = today.AddDays(-10) });

        var promo = new Promotion
        {
            Id = 1,
            Code = "MIN3M",
            Name = "Fixed 500k off for >= 3 months and >= 2.5M subtotal",
            DiscountType = PromotionDiscountTypeConstants.Fixed,
            DiscountValue = 500_000m,
            MaxDiscountAmount = 300_000m,
            IsActive = true,
            ValidFrom = now.AddDays(-1),
            ValidTo = now.AddDays(10)
        };
        promo.PromotionRules.Add(new PromotionRule { Id = 1, PromotionId = 1, RuleType = "minimum_months", Operator = "gte", RuleValue = "3" });
        promo.PromotionRules.Add(new PromotionRule { Id = 2, PromotionId = 1, RuleType = "minimum_amount", Operator = "gte", RuleValue = "2500000" });
        promo.PromotionRules.Add(new PromotionRule { Id = 3, PromotionId = 1, RuleType = "facility", Operator = "eq", RuleValue = "1" });
        db.Promotions.Add(promo);
        await db.SaveChangesAsync();

        var engine = new PricingEngine(db);

        // Past date rejected
        await Assert.ThrowsAsync<AppValidationException>(() => engine.CalculateQuoteAsync(new PricingQuoteRequest
        {
            FacilityId = 1,
            UnitTypeId = 1,
            StartDate = today.AddDays(-5),
            DurationMonths = 3
        }));

        // 2 months fails minimum_months=3 & minimum_amount=2.5M
        await Assert.ThrowsAsync<AppValidationException>(() => engine.CalculateQuoteAsync(new PricingQuoteRequest
        {
            FacilityId = 1,
            UnitTypeId = 1,
            StartDate = today.AddDays(1),
            DurationMonths = 2,
            PromotionCode = "MIN3M"
        }));

        // 3 months succeeds and applies 300k cap
        var quote = await engine.CalculateQuoteAsync(new PricingQuoteRequest
        {
            FacilityId = 1,
            UnitTypeId = 1,
            StartDate = today.AddDays(1),
            DurationMonths = 3,
            PromotionCode = "MIN3M"
        });
        Assert.Equal(300_000m, quote.DiscountAmount);
        // First payment: (1,000,000 - 300,000) + 1,000,000 deposit + 50,000 fee + 75,000 tax = 1,825,000
        Assert.Equal(75_000m, quote.TaxAmount);
        Assert.Equal(1_825_000m, quote.FirstPaymentAmount);
    }

    [Fact]
    public async Task CreateReservation_TwoCustomersSameUnit_SecondThrowsConflict()
    {
        await using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        db.CustomerProfiles.AddRange(
            new CustomerProfile { UserId = 1, FullName = "Cust 1" },
            new CustomerProfile { UserId = 2, FullName = "Cust 2" });
        db.Facilities.Add(new Facility { Id = 1, Code = "F1", Name = "F1", AddressLine = "A", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = FacilityStatusConstants.Active });
        db.UnitTypes.Add(new UnitType { Id = 1, Code = "U1", Name = "U1", IsActive = true });
        db.FacilityRates.Add(new FacilityRate { Id = 1, FacilityId = 1, UnitTypeId = 1, MonthlyRate = 500_000m, DepositAmount = 500_000m, BookingFee = 0m, ValidFrom = today.AddDays(-5) });
        db.StorageUnits.Add(new StorageUnit { Id = 10, FacilityId = 1, UnitTypeId = 1, UnitCode = "A-01", PhysicalStatus = StorageUnitStatusConstants.Available, IsListed = true });
        await db.SaveChangesAsync();

        var service = new CustomerReservationService(db, Options.Create(new ReservationSettings()), NullLogger<CustomerReservationService>.Instance);
        var req = new CreateReservationRequest { FacilityId = 1, UnitTypeId = 1, StorageUnitId = 10, StartDate = today.AddDays(1), DurationMonths = 1 };

        var first = await service.CreateReservationAsync(1, req);
        Assert.Equal("pending", first.Status);

        await Assert.ThrowsAsync<AppConflictException>(() => service.CreateReservationAsync(2, req));
        Assert.Single(await db.Reservations.ToListAsync());
    }

    [Fact]
    public async Task ReservationHoldExpiryService_ReleasesExpiredHoldAndWritesHistory_AndLateWebhookCreatesRefund()
    {
        await using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var user = new User { Id = 1, Email = "late@test.local", PasswordHash = "x", Status = "active" };
        db.Users.Add(user);
        db.CustomerProfiles.Add(new CustomerProfile { UserId = 1, FullName = "Late Cust", User = user });
        db.Facilities.Add(new Facility { Id = 1, Code = "F1", Name = "F1", AddressLine = "A", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = FacilityStatusConstants.Active });
        db.UnitTypes.Add(new UnitType { Id = 1, Code = "U1", Name = "U1", IsActive = true });
        db.StorageUnits.Add(new StorageUnit { Id = 10, FacilityId = 1, UnitTypeId = 1, UnitCode = "A-01", PhysicalStatus = StorageUnitStatusConstants.Reserved, IsListed = true });
        var rsv = new Reservation
        {
            Id = 50,
            CustomerId = 1,
            FacilityId = 1,
            UnitTypeId = 1,
            FacilityRateId = 1,
            ReservationCode = "RSV-EXP-50",
            StartDate = today.AddDays(1),
            EndDate = today.AddMonths(1),
            Status = ReservationStatusConstants.Pending,
            HoldUntil = now.AddMinutes(-10),
            CreatedAt = now.AddMinutes(-25),
            UpdatedAt = now.AddMinutes(-25)
        };
        var alloc = new UnitAllocation { Id = 50, ReservationId = 50, StorageUnitId = 10, AllocationKind = AllocationKindConstants.Reservation, AllocationStartDate = today.AddDays(1), AllocationEndDate = today.AddMonths(1), Status = AllocationStatusConstants.Active };
        var inv = new Invoice { Id = 700, CustomerId = 1, ReservationId = 50, InvoiceNo = "INV-700", IssueDate = today, DueDate = today, Currency = "VND", TotalAmount = 1_000_000m, Status = InvoiceStatusConstants.Open };
        db.Reservations.Add(rsv);
        db.UnitAllocations.Add(alloc);
        db.Invoices.Add(inv);
        await db.SaveChangesAsync();

        var expiryService = new ReservationHoldExpiryService(db, Options.Create(new ReservationSettings()));
        var released = await expiryService.ReleaseExpiredHoldsAsync(respectGracePeriod: true);
        Assert.Equal(1, released);
        Assert.Equal(ReservationStatusConstants.Expired, rsv.Status);
        Assert.Equal(AllocationStatusConstants.Released, alloc.Status);
        Assert.Equal(StorageUnitStatusConstants.Available, (await db.StorageUnits.FindAsync(10L))!.PhysicalStatus);
        Assert.Equal(InvoiceStatusConstants.Voided, inv.Status);
        Assert.Single(await db.UnitStatusHistories.ToListAsync());

        // Now late payment arrives on voided invoice -> flagged & refund created
        var payService = new CustomerPaymentService(
            db,
            Mock.Of<IEmailService>(),
            Options.Create(new PaymentSettings { SePay = new SePayConfig { ApiKey = "k", InvoicePrefix = "DH" } }),
            NullLogger<CustomerPaymentService>.Instance);

        var webhookRes = await payService.ProcessSePayWebhookWithResultAsync(new SePayWebhookPayload
        {
            Id = 8801,
            ReferenceCode = "LATE-8801",
            Content = "DH700",
            TransferType = "in",
            TransferAmount = 1_000_000m
        });

        Assert.True(webhookRes.Processed);
        Assert.Equal("late_payment_flagged", webhookRes.Action);
        Assert.Single(await db.Refunds.ToListAsync());
        Assert.Equal(ReservationStatusConstants.Expired, (await db.Reservations.FindAsync(50L))!.Status);
    }
}
