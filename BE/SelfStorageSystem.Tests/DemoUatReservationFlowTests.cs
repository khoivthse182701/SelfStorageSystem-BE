using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Customer.Payments;
using SelfStorageSystem.Contracts.Customer.Reservations;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;

namespace SelfStorageSystem.Tests;

/// <summary>
/// Regression tests for the records in DB/Seed_Demo_UAT_EndToEnd.sql.
/// The tests use isolated in-memory data so they do not require SQL Server.
/// </summary>
public class DemoUatReservationFlowTests
{
    [Fact]
    public async Task CreateReservation_AvailableUnit_CreatesPendingInvoiceAndHoldsTheUnit()
    {
        await using var db = CreateDb();
        await SeedBookableScopeAsync(db, unitStatus: StorageUnitStatusConstants.Available);

        var result = await CreateService(db).CreateReservationAsync(7, Request(unitId: 101));

        Assert.Equal(ReservationStatusConstants.Pending, result.Status);
        Assert.InRange(result.ExpiresInSeconds, 15 * 60 - 1, 15 * 60);
        Assert.Equal(5_000m, result.FirstPaymentAmount); // 2,000 rent + 2,000 deposit + 1,000 booking fee
        Assert.Equal(StorageUnitStatusConstants.Reserved, (await db.StorageUnits.FindAsync(101L))!.PhysicalStatus);
        Assert.Single(await db.UnitAllocations.Where(x => x.ReservationId == result.ReservationId && x.Status == AllocationStatusConstants.Active).ToListAsync());
        Assert.Equal(InvoiceStatusConstants.Open, (await db.Invoices.FindAsync(result.InvoiceId))!.Status);
    }

    [Theory]
    [InlineData(StorageUnitStatusConstants.Reserved)]
    [InlineData(StorageUnitStatusConstants.Occupied)]
    [InlineData(StorageUnitStatusConstants.Maintenance)]
    [InlineData(StorageUnitStatusConstants.PendingInspection)]
    public async Task CreateReservation_NonBookableUnit_RejectsWithoutWritingAnyBooking(string unitStatus)
    {
        await using var db = CreateDb();
        await SeedBookableScopeAsync(db, unitStatus);

        await Assert.ThrowsAsync<AppConflictException>(() => CreateService(db).CreateReservationAsync(7, Request(unitId: 101)));

        Assert.Empty(await db.Reservations.ToListAsync());
        Assert.Empty(await db.Invoices.ToListAsync());
        Assert.Empty(await db.UnitAllocations.ToListAsync());
    }

    [Fact]
    public async Task CancelReservation_PendingDemoReservation_ReleasesUnitAndVoidsInvoice()
    {
        await using var db = CreateDb();
        await SeedBookableScopeAsync(db, StorageUnitStatusConstants.Reserved);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var reservation = new Reservation
        {
            Id = 901,
            ReservationCode = "DEMO-UAT-PENDING",
            CustomerId = 7,
            FacilityId = 1,
            UnitTypeId = 1,
            FacilityRateId = 1,
            StartDate = today,
            EndDate = today.AddMonths(1),
            HoldUntil = DateTimeOffset.UtcNow.AddMinutes(15),
            Status = ReservationStatusConstants.Pending
        };
        db.Reservations.Add(reservation);
        db.UnitAllocations.Add(new UnitAllocation
        {
            Id = 901,
            ReservationId = reservation.Id,
            StorageUnitId = 101,
            AllocationKind = AllocationKindConstants.ReservationHold,
            AllocationStartDate = today,
            AllocationEndDate = today.AddMonths(1),
            Status = AllocationStatusConstants.Active
        });
        db.Invoices.Add(new Invoice
        {
            Id = 901,
            ReservationId = reservation.Id,
            CustomerId = 7,
            InvoiceNo = "DEMO-UAT-INV-PENDING",
            IssueDate = today,
            DueDate = today,
            Currency = "VND",
            TotalAmount = 5_000m,
            Status = InvoiceStatusConstants.Open
        });
        await db.SaveChangesAsync();

        var cancelled = await CreateService(db).CancelReservationAsync(7, reservation.Id, "UAT cancellation");

        Assert.True(cancelled);
        Assert.Equal(ReservationStatusConstants.Cancelled, (await db.Reservations.FindAsync(901L))!.Status);
        Assert.Equal(StorageUnitStatusConstants.Available, (await db.StorageUnits.FindAsync(101L))!.PhysicalStatus);
        Assert.Equal(AllocationStatusConstants.Released, (await db.UnitAllocations.FindAsync(901L))!.Status);
        Assert.Equal(InvoiceStatusConstants.Voided, (await db.Invoices.FindAsync(901L))!.Status);
    }

    [Fact]
    public async Task CreateCheckout_StandaloneChargeInvoice_ReturnsDynamicQrWithoutReservation()
    {
        await using var db = CreateDb();
        await SeedBookableScopeAsync(db, StorageUnitStatusConstants.Available);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.Invoices.Add(new Invoice
        {
            Id = 902,
            CustomerId = 7,
            InvoiceNo = "INV-CHG-DEMO-902",
            IssueDate = today,
            DueDate = today,
            Currency = "VND",
            TotalAmount = 200_000m,
            PaidAmount = 0,
            Status = InvoiceStatusConstants.Open
        });
        await db.SaveChangesAsync();

        var checkout = await CreatePaymentService(db).CreateCheckoutAsync(7, new CreateCheckoutRequest { InvoiceId = 902 });

        Assert.Equal(0, checkout.ReservationId);
        Assert.Equal(902, checkout.InvoiceId);
        Assert.Equal(200_000m, checkout.Amount);
        Assert.NotNull(checkout.VietQr);
        Assert.Contains("DH902", checkout.VietQr.TransferContent);
        Assert.Single(await db.Payments.Where(p => p.TargetInvoiceId == 902 && p.Status == PaymentConstants.StatusPending).ToListAsync());
    }

    [Fact]
    public async Task CreateCheckout_StandaloneInvoiceOfAnotherCustomer_IsNotAccessible()
    {
        await using var db = CreateDb();
        await SeedBookableScopeAsync(db, StorageUnitStatusConstants.Available);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.Invoices.Add(new Invoice
        {
            Id = 903,
            CustomerId = 999,
            InvoiceNo = "INV-CHG-OTHER-CUSTOMER",
            IssueDate = today,
            DueDate = today,
            Currency = "VND",
            TotalAmount = 50_000m,
            Status = InvoiceStatusConstants.Open
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<AppNotFoundException>(() =>
            CreatePaymentService(db).CreateCheckoutAsync(7, new CreateCheckoutRequest { InvoiceId = 903 }));

        Assert.Empty(await db.Payments.ToListAsync());
    }

    [Fact]
    public async Task CreateCheckout_WithBothTargets_RejectsAmbiguousPaymentTarget()
    {
        await using var db = CreateDb();
        await SeedBookableScopeAsync(db, StorageUnitStatusConstants.Available);

        await Assert.ThrowsAsync<AppNotFoundException>(() => CreatePaymentService(db).CreateCheckoutAsync(
            7,
            new CreateCheckoutRequest { ReservationId = 1, InvoiceId = 1 }));

        Assert.Empty(await db.Payments.ToListAsync());
    }

    [Fact]
    public async Task GetMyUnpaidInvoices_ReturnsOnlyTheCustomerOutstandingBalance()
    {
        await using var db = CreateDb();
        await SeedBookableScopeAsync(db, StorageUnitStatusConstants.Available);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.Invoices.AddRange(
            new Invoice { Id = 904, CustomerId = 7, InvoiceNo = "INV-CHG-OPEN", IssueDate = today, DueDate = today, Currency = "VND", TotalAmount = 200_000m, PaidAmount = 50_000m, Status = InvoiceStatusConstants.PartiallyPaid },
            new Invoice { Id = 905, CustomerId = 7, InvoiceNo = "INV-PAID", IssueDate = today, DueDate = today, Currency = "VND", TotalAmount = 100_000m, PaidAmount = 100_000m, Status = InvoiceStatusConstants.Paid },
            new Invoice { Id = 906, CustomerId = 99, InvoiceNo = "INV-OTHER", IssueDate = today, DueDate = today, Currency = "VND", TotalAmount = 100_000m, Status = InvoiceStatusConstants.Open });
        await db.SaveChangesAsync();

        var invoices = await CreatePaymentService(db).GetMyUnpaidInvoicesAsync(7);

        var invoice = Assert.Single(invoices);
        Assert.Equal(904, invoice.InvoiceId);
        Assert.Equal(150_000m, invoice.AmountDue);
    }

    private static SelfStorageDbContext CreateDb() => new(new DbContextOptionsBuilder<SelfStorageDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private static CustomerReservationService CreateService(SelfStorageDbContext db) => new(
        db,
        Options.Create(new ReservationSettings { HoldDurationMinutes = 15, MinDurationMonths = 1, MaxDurationMonths = 12 }),
        NullLogger<CustomerReservationService>.Instance);

    private static CustomerPaymentService CreatePaymentService(SelfStorageDbContext db) => new(
        db,
        Mock.Of<IEmailService>(),
        Options.Create(new PaymentSettings
        {
            SePay = new SePayConfig { InvoicePrefix = "DH" },
            VietQr = new VietQrConfig { BaseUrl = "https://vietqr.app/img", BankCode = "MBBank", AccountNo = "123", AccountName = "Demo" }
        }),
        NullLogger<CustomerPaymentService>.Instance);

    private static CreateReservationRequest Request(long unitId) => new()
    {
        FacilityId = 1,
        UnitTypeId = 1,
        StorageUnitId = unitId,
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
        DurationMonths = 1
    };

    private static async Task SeedBookableScopeAsync(SelfStorageDbContext db, string unitStatus)
    {
        db.Users.Add(new User { Id = 7, Email = "minh.demo@example.com", PasswordHash = "hash", Status = UserStatusConstants.Active });
        db.CustomerProfiles.Add(new CustomerProfile { UserId = 7, FullName = "Anh Minh" });
        db.Facilities.Add(new Facility { Id = 1, Code = "Q7-NVL", Name = "Kho Quận 7 - Nguyễn Văn Linh", AddressLine = "Nguyễn Văn Linh", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = FacilityStatusConstants.Active });
        db.UnitTypes.Add(new UnitType { Id = 1, Code = "M", Name = "Size M", WidthM = 2, LengthM = 2.5m, HeightM = 2.5m, IsActive = true });
        db.FacilityRates.Add(new FacilityRate { Id = 1, FacilityId = 1, UnitTypeId = 1, MonthlyRate = 2_000m, DepositAmount = 2_000m, BookingFee = 1_000m, ValidFrom = new DateOnly(2020, 1, 1) });
        db.StorageUnits.Add(new StorageUnit { Id = 101, FacilityId = 1, UnitTypeId = 1, UnitCode = "A-101", PhysicalStatus = unitStatus, IsListed = true });
        await db.SaveChangesAsync();
    }
}
