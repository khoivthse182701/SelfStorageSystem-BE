using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Auth;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Facilities;
using SelfStorageSystem.Contracts.Customer.Payments;
using SelfStorageSystem.Contracts.Customer.Reservations;
using SelfStorageSystem.Contracts.Manager;
using SelfStorageSystem.Contracts.Staff;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;

namespace SelfStorageSystem.Tests;

public class Phase3Group1SkepticVerificationTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static SelfStorageDbContext CreateInMemoryDb() =>
        new(new DbContextOptionsBuilder<SelfStorageDbContext>()
            .UseInMemoryDatabase($"Skeptic_{Guid.NewGuid():N}")
            .Options);

    private static async Task<string> LoginAndGetTokenAsync(HttpClient client, string email, string password)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Email = email, Password = password });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(JsonOpts);
        return body!.Data!.AccessToken;
    }

    // =========================================================================
    // 1. SignalR HubConnection Integration Verification
    // =========================================================================

    [Fact]
    public async Task SignalR_HubConnection_EnforcesAuth_FacilityJoin_AndDeliversWebhookEvents()
    {
        await using var factory = new Phase3Group1WebFactory();
        await factory.SeedBaselineAsync();

        // 1a. Unauthenticated connection rejected
        await using var anonConn = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/notifications"), opts =>
            {
                opts.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        await Assert.ThrowsAnyAsync<Exception>(() => anonConn.StartAsync());

        var authClient = factory.CreateClient();
        var custToken = await LoginAndGetTokenAsync(authClient, "cust1@test.local", "Pass@123");
        var staffToken = await LoginAndGetTokenAsync(authClient, "staff1@test.local", "Pass@123");

        // 1b. Staff assigned to facility 1 can join facility 1, rejected on facility 2
        var staffUnitChangeTcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var staffConn = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/notifications"), opts =>
            {
                opts.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                opts.AccessTokenProvider = () => Task.FromResult<string?>(staffToken);
            })
            .Build();
        staffConn.On<JsonElement>("UnitStatusChanged", payload => staffUnitChangeTcs.TrySetResult(payload));
        await staffConn.StartAsync();

        await staffConn.InvokeAsync("JoinFacility", 1L);
        var hubEx = await Assert.ThrowsAsync<HubException>(() => staffConn.InvokeAsync("JoinFacility", 2L));
        Assert.Contains("JoinFacility", hubEx.Message, StringComparison.OrdinalIgnoreCase);

        // 1c. Customer receives PaymentSucceeded & NotificationReceived when SePay webhook completes payment
        var paymentSucceededTcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var notifTcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var custConn = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/notifications"), opts =>
            {
                opts.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                opts.AccessTokenProvider = () => Task.FromResult<string?>(custToken);
            })
            .Build();
        custConn.On<JsonElement>("PaymentSucceeded", payload => paymentSucceededTcs.TrySetResult(payload));
        custConn.On<JsonElement>("NotificationReceived", payload => notifTcs.TrySetResult(payload));
        await custConn.StartAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", custToken);
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);

        var rsvResp = await client.PostAsJsonAsync("/api/v1/reservations", new CreateReservationRequest
        {
            FacilityId = 1,
            UnitTypeId = 1,
            StorageUnitId = 101,
            StartDate = today.AddDays(1),
            DurationMonths = 1
        });
        rsvResp.EnsureSuccessStatusCode();
        var rsv = (await rsvResp.Content.ReadFromJsonAsync<ApiResponse<CreateReservationResponse>>(JsonOpts))!.Data!;

        var payReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/sepay")
        {
            Content = JsonContent.Create(new
            {
                id = 77001,
                gateway = "MBBank",
                transferType = "in",
                transferAmount = rsv.FirstPaymentAmount,
                content = $"DH{rsv.InvoiceId}",
                referenceCode = "SR-77001"
            })
        };
        payReq.Headers.Authorization = new AuthenticationHeaderValue("Apikey", factory.SePayApiKey);
        var webhookResp = await client.SendAsync(payReq);
        webhookResp.EnsureSuccessStatusCode();

        var completedPayment = await paymentSucceededTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var notifPayload = await notifTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(rsv.InvoiceId, completedPayment.GetProperty("invoiceId").GetInt64());
        Assert.Contains(rsv.ReservationCode, notifPayload.GetProperty("message").GetString()!);

        // Check wrong-customer QR rejected + staff unit status broadcast on confirm check-in
        using (var scope = factory.Services.CreateScope())
        {
            var qrSvc = scope.ServiceProvider.GetRequiredService<IQrTokenService>();
            var wrongCustQr = qrSvc.IssueToken("checkin", rsv.ReservationId, null, 999, 1);
            var qrToken = qrSvc.IssueToken("checkin", rsv.ReservationId, null, 10, 1);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

            var wrongCustResp = await client.PostAsJsonAsync("/api/v1/staff/check-ins/verify-qr", new VerifyCheckInQrRequest(wrongCustQr, 1));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongCustResp.StatusCode);

            var verifyResp = await client.PostAsJsonAsync("/api/v1/staff/check-ins/verify-qr", new VerifyCheckInQrRequest(qrToken, 1));
            verifyResp.EnsureSuccessStatusCode();

            var confirmResp = await client.PostAsJsonAsync("/api/v1/staff/check-ins/confirm", new ConfirmCheckInRequest
            {
                ReservationId = rsv.ReservationId,
                IdentityVerification = new IdentityVerificationInput { Method = "government_id", DocumentNumber = "079200001234", FullNameMatched = true },
                Inspection = new CheckInInspectionInput { OverallCondition = "good" }
            });
            confirmResp.EnsureSuccessStatusCode();
        }

        var unitChanged = await staffUnitChangeTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("occupied", unitChanged.GetProperty("physicalStatus").GetString());
    }

    // =========================================================================
    // 2. Regression Unit Tests for Pricing, Catalog, Cancellation, Validation & Security Headers
    // =========================================================================

    [Fact]
    public async Task PricingEngine_SupportsFreeDays_PromotionOperators_NewCustomer_AndLatestVatRule()
    {
        await using var db = CreateInMemoryDb();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(7)).DateTime);

        db.Users.AddRange(
            new User { Id = 1, Email = "new@test.local", PasswordHash = "x", Status = "active" },
            new User { Id = 2, Email = "old@test.local", PasswordHash = "x", Status = "active" });
        db.CustomerProfiles.AddRange(
            new CustomerProfile { UserId = 1, FullName = "Brand New" },
            new CustomerProfile { UserId = 2, FullName = "Returning" });
        db.Facilities.Add(new Facility { Id = 1, Code = "F1", Name = "F1", AddressLine = "A", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" });
        db.UnitTypes.Add(new UnitType { Id = 1, Code = "U1", Name = "U1", IsActive = true });
        db.FacilityRates.Add(new FacilityRate { Id = 1, FacilityId = 1, UnitTypeId = 1, MonthlyRate = 900_000m, DepositAmount = 900_000m, BookingFee = 0m, ValidFrom = today.AddDays(-10) });

        // Older facility VAT 8% vs newer facility VAT 10%
        db.FeeRules.AddRange(
            new FeeRule { Id = 1, FacilityId = 1, Code = "VAT_OLD", FeeType = FeeRuleTypeConstants.Tax, CalculationMethod = "percentage", RatePercent = 8m, Conditions = "{}", IsActive = true, ValidFrom = today.AddDays(-30) },
            new FeeRule { Id = 2, FacilityId = 1, Code = "VAT_NEW", FeeType = FeeRuleTypeConstants.VatTax, CalculationMethod = "percentage", RatePercent = 10m, Conditions = "{}", IsActive = true, ValidFrom = today.AddDays(-2) });

        // Confirmed historical reservation for Customer 2
        db.Reservations.Add(new Reservation
        {
            Id = 500,
            CustomerId = 2,
            FacilityId = 1,
            UnitTypeId = 1,
            FacilityRateId = 1,
            ReservationCode = "RSV-HIST-1",
            StartDate = today.AddDays(-20),
            EndDate = today.AddDays(10),
            MonthlyRateSnapshot = 900_000m,
            DepositSnapshot = 900_000m,
            QuotedTotal = 1_800_000m,
            HoldUntil = now.AddMinutes(-10),
            Status = ReservationStatusConstants.Confirmed
        });

        // 15 free days promo restricted to new_customer=true, facility in [1,3], month lte 6
        db.Promotions.Add(new Promotion
        {
            Id = 1,
            Code = "FREE15",
            Name = "15 Free Days",
            DiscountType = PromotionDiscountTypeConstants.FreeDays,
            DiscountValue = 15m,
            IsActive = true,
            ValidFrom = now.AddDays(-1),
            ValidTo = now.AddDays(10),
            PromotionRules =
            {
                new PromotionRule { Id = 1, PromotionId = 1, RuleType = PromotionRuleTypeConstants.NewCustomer, Operator = PromotionOperatorConstants.Eq, RuleValue = "true" },
                new PromotionRule { Id = 2, PromotionId = 1, RuleType = PromotionRuleTypeConstants.Facility, Operator = PromotionOperatorConstants.In, RuleValue = "1,3" },
                new PromotionRule { Id = 3, PromotionId = 1, RuleType = PromotionRuleTypeConstants.Month, Operator = PromotionOperatorConstants.Lte, RuleValue = "6" }
            }
        });
        await db.SaveChangesAsync();

        var engine = new PricingEngine(db);

        // Anonymous rejected on new_customer=true
        await Assert.ThrowsAsync<AppValidationException>(() =>
            engine.CalculateQuoteAsync(new PricingQuoteRequest { FacilityId = 1, UnitTypeId = 1, StartDate = today, DurationMonths = 3, PromotionCode = "FREE15" }, customerId: null));

        // Returning customer rejected
        await Assert.ThrowsAsync<AppValidationException>(() =>
            engine.CalculateQuoteAsync(new PricingQuoteRequest { FacilityId = 1, UnitTypeId = 1, StartDate = today, DurationMonths = 3, PromotionCode = "FREE15" }, customerId: 2));

        // Month > 6 rejected by lte rule
        await Assert.ThrowsAsync<AppValidationException>(() =>
            engine.CalculateQuoteAsync(new PricingQuoteRequest { FacilityId = 1, UnitTypeId = 1, StartDate = today, DurationMonths = 8, PromotionCode = "FREE15" }, customerId: 1));

        // New customer with valid duration gets 15/30 * 900k = 450k discount + latest 10% VAT
        var quote = await engine.CalculateQuoteAsync(new PricingQuoteRequest { FacilityId = 1, UnitTypeId = 1, StartDate = today, DurationMonths = 3, PromotionCode = "FREE15" }, customerId: 1);
        Assert.Equal(450_000m, quote.DiscountAmount);
        Assert.Equal(10m, quote.VatRatePercent);
        Assert.Equal(45_000m, quote.TaxAmount); // 10% of (900k - 450k)
    }

    [Fact]
    public async Task CatalogAndReservation_AgreeOnReservedUnitAvailability_AndOrphanReservedExclusion()
    {
        await using var db = CreateInMemoryDb();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(7)).DateTime);

        db.Users.Add(new User { Id = 1, Email = "c1@test.local", PasswordHash = "x", Status = "active" });
        db.CustomerProfiles.Add(new CustomerProfile { UserId = 1, FullName = "Cust 1" });
        db.Facilities.Add(new Facility { Id = 1, Code = "F1", Name = "F1", AddressLine = "A", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" });
        db.UnitTypes.Add(new UnitType { Id = 1, Code = "U1", Name = "U1", IsActive = true });
        db.FacilityRates.Add(new FacilityRate { Id = 1, FacilityId = 1, UnitTypeId = 1, MonthlyRate = 1_000_000m, DepositAmount = 1_000_000m, BookingFee = 0m, ValidFrom = today.AddDays(-5) });

        // Unit 10: Reserved with confirmed allocation for Month 1 (today..today+1m)
        // Unit 11: Orphan Reserved with ZERO allocations (should NOT appear or be reservable)
        db.StorageUnits.AddRange(
            new StorageUnit { Id = 10, FacilityId = 1, UnitTypeId = 1, UnitCode = "U-10", PhysicalStatus = StorageUnitStatusConstants.Reserved, IsListed = true },
            new StorageUnit { Id = 11, FacilityId = 1, UnitTypeId = 1, UnitCode = "U-11", PhysicalStatus = StorageUnitStatusConstants.Reserved, IsListed = true });

        db.Reservations.Add(new Reservation
        {
            Id = 50,
            CustomerId = 1,
            FacilityId = 1,
            UnitTypeId = 1,
            FacilityRateId = 1,
            ReservationCode = "RSV-M1",
            StartDate = today,
            EndDate = today.AddMonths(1),
            MonthlyRateSnapshot = 1_000_000m,
            DepositSnapshot = 1_000_000m,
            QuotedTotal = 2_000_000m,
            HoldUntil = now.AddMinutes(10),
            Status = ReservationStatusConstants.Confirmed
        });
        db.UnitAllocations.Add(new UnitAllocation
        {
            Id = 50,
            StorageUnitId = 10,
            ReservationId = 50,
            AllocationKind = AllocationKindConstants.ReservationHold,
            AllocationStartDate = today,
            AllocationEndDate = today.AddMonths(1),
            Status = AllocationStatusConstants.Active
        });
        await db.SaveChangesAsync();

        var catalog = new CustomerCatalogService(db);
        // Overlapping search today -> neither Unit 10 nor orphan Unit 11 is returned
        var nowAvail = await catalog.GetAvailableUnitsAsync(1, 1, null, today, 1);
        Assert.Empty(nowAvail);

        // Non-overlapping future search today.AddMonths(2) -> Unit 10 IS returned, orphan Unit 11 is NOT
        var futureStart = today.AddMonths(2);
        var futureAvail = await catalog.GetAvailableUnitsAsync(1, 1, null, futureStart, 1);
        var listed = Assert.Single(futureAvail);
        Assert.Equal(10L, listed.Id);

        // CreateReservationAsync accepts the listed unit 10 for futureStart!
        var rsvSvc = new CustomerReservationService(db, Options.Create(new ReservationSettings()), NullLogger<CustomerReservationService>.Instance);
        var created = await rsvSvc.CreateReservationAsync(1, new CreateReservationRequest
        {
            FacilityId = 1,
            UnitTypeId = 1,
            StorageUnitId = 10,
            StartDate = futureStart,
            DurationMonths = 1
        });
        Assert.Equal(10L, created.UnitId);

        // Orphan unit 11 still rejected
        await Assert.ThrowsAsync<AppConflictException>(() => rsvSvc.CreateReservationAsync(1, new CreateReservationRequest
        {
            FacilityId = 1,
            UnitTypeId = 1,
            StorageUnitId = 11,
            StartDate = futureStart,
            DurationMonths = 1
        }));
    }

    [Fact]
    public async Task HoldExpiry_DoesNotFlipReservedUnitWhenOtherActiveAllocationExists_AndKeepsPartialPaidInvoice()
    {
        await using var db = CreateInMemoryDb();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        db.StorageUnits.Add(new StorageUnit { Id = 10, FacilityId = 1, UnitTypeId = 1, UnitCode = "A-10", PhysicalStatus = StorageUnitStatusConstants.Reserved, IsListed = true });
        db.Reservations.AddRange(
            new Reservation { Id = 1, CustomerId = 1, FacilityId = 1, UnitTypeId = 1, FacilityRateId = 1, ReservationCode = "RSV-EXP", StartDate = today, EndDate = today.AddMonths(1), Status = ReservationStatusConstants.Pending, HoldUntil = now.AddMinutes(-10), CreatedAt = now.AddMinutes(-25) },
            new Reservation { Id = 2, CustomerId = 2, FacilityId = 1, UnitTypeId = 1, FacilityRateId = 1, ReservationCode = "RSV-CONF", StartDate = today.AddMonths(2), EndDate = today.AddMonths(3), Status = ReservationStatusConstants.Confirmed, HoldUntil = now.AddMinutes(10), CreatedAt = now });
        db.UnitAllocations.AddRange(
            new UnitAllocation { Id = 1, ReservationId = 1, StorageUnitId = 10, AllocationKind = AllocationKindConstants.ReservationHold, AllocationStartDate = today, AllocationEndDate = today.AddMonths(1), Status = AllocationStatusConstants.Active },
            new UnitAllocation { Id = 2, ReservationId = 2, StorageUnitId = 10, AllocationKind = AllocationKindConstants.ReservationHold, AllocationStartDate = today.AddMonths(2), AllocationEndDate = today.AddMonths(3), Status = AllocationStatusConstants.Active });
        db.Invoices.Add(new Invoice { Id = 1, CustomerId = 1, ReservationId = 1, InvoiceNo = "INV-PART", IssueDate = today, DueDate = today, Currency = "VND", TotalAmount = 1_000_000m, PaidAmount = 400_000m, Status = InvoiceStatusConstants.PartiallyPaid });
        await db.SaveChangesAsync();

        var svc = new ReservationHoldExpiryService(db, Options.Create(new ReservationSettings()));
        var released = await svc.ReleaseExpiredHoldsAsync(respectGracePeriod: true);
        Assert.Equal(1, released);
        Assert.Equal(StorageUnitStatusConstants.Reserved, (await db.StorageUnits.FindAsync(10L))!.PhysicalStatus);
        Assert.Equal(InvoiceStatusConstants.PartiallyPaid, (await db.Invoices.FindAsync(1L))!.Status);
    }

    [Fact]
    public async Task SecurityHeaders_FluentValidation_AndStrictStaffAuth_BehaveCorrectlyOnHttp()
    {
        await using var factory = new Phase3Group1WebFactory();
        await factory.SeedBaselineAsync();
        var client = factory.CreateClient();

        // Cache-Control: no-store on /api/v1/auth/login
        var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Email = "staff1@test.local", Password = "Pass@123" });
        loginResp.EnsureSuccessStatusCode();
        Assert.True(loginResp.Headers.CacheControl?.NoStore == true);
        var staffToken = (await loginResp.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(JsonOpts))!.Data!.AccessToken;

        // 422 validation on oversized check-in incident description (> 255)
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);
        var tooLongTicket = await client.PostAsJsonAsync("/api/v1/staff/tickets", new StaffCreateCheckInIncidentTicketRequest
        {
            FacilityId = 1,
            StorageUnitId = 101,
            Description = new string('X', 300)
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooLongTicket.StatusCode);

        // Unassigned manager (userId=30 is assigned to Fac 1 only) cannot query Fac 2 staff endpoints -> 403
        var mgrToken = await LoginAndGetTokenAsync(client, "mgr1@test.local", "Pass@123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgrToken);

        var crossFacUnits = await client.GetAsync("/api/v1/staff/units?facilityId=2");
        Assert.Equal(HttpStatusCode.Forbidden, crossFacUnits.StatusCode);
    }

    [Fact]
    public async Task Manager_EmergencyCancelDraftAgreement_AndReassignWithoutMaintenance_WorkCleanly()
    {
        await using var factory = new Phase3Group1WebFactory();
        await factory.SeedBaselineAsync();
        var client = factory.CreateClient();

        var custToken = await LoginAndGetTokenAsync(client, "cust1@test.local", "Pass@123");
        var mgrToken = await LoginAndGetTokenAsync(client, "mgr1@test.local", "Pass@123");
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", custToken);
        var rsvResp = await client.PostAsJsonAsync("/api/v1/reservations", new CreateReservationRequest
        {
            FacilityId = 1,
            UnitTypeId = 1,
            StorageUnitId = 101,
            StartDate = today.AddDays(3),
            DurationMonths = 1
        });
        rsvResp.EnsureSuccessStatusCode();
        var rsv = (await rsvResp.Content.ReadFromJsonAsync<ApiResponse<CreateReservationResponse>>(JsonOpts))!.Data!;

        var payReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/sepay")
        {
            Content = JsonContent.Create(new
            {
                id = 88001,
                gateway = "MBBank",
                transferType = "in",
                transferAmount = rsv.FirstPaymentAmount,
                content = $"DH{rsv.InvoiceId}",
                referenceCode = "SR-88001"
            })
        };
        payReq.Headers.Authorization = new AuthenticationHeaderValue("Apikey", factory.SePayApiKey);
        (await client.SendAsync(payReq)).EnsureSuccessStatusCode();

        long draftAgreementId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
            draftAgreementId = await db.RentalAgreements
                .Where(a => a.ReservationId == rsv.ReservationId)
                .Select(a => a.Id)
                .SingleAsync();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgrToken);
        var cancelResp = await client.PostAsJsonAsync($"/api/v1/manager/agreements/{draftAgreementId}/emergency-cancel-refund",
            new ManagerEmergencyCancelRefundRequest { Reason = "Pre-checkin unit water leak" });
        cancelResp.EnsureSuccessStatusCode();
        var cancelData = (await cancelResp.Content.ReadFromJsonAsync<ApiResponse<ManagerEmergencyCancelRefundResponseDto>>(JsonOpts))!.Data!;
        Assert.Equal("approved", cancelData.RefundStatus);
        Assert.Equal(rsv.FirstPaymentAmount, cancelData.TotalRefundAmount);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
            var rsvEntity = await db.Reservations.Include(r => r.UnitAllocation).SingleAsync(r => r.Id == rsv.ReservationId);
            Assert.Equal(ReservationStatusConstants.Cancelled, rsvEntity.Status);
            Assert.Equal(AllocationStatusConstants.Released, rsvEntity.UnitAllocation!.Status);
            Assert.Equal(StorageUnitStatusConstants.Maintenance, (await db.StorageUnits.FindAsync(101L))!.PhysicalStatus);
        }
    }
}

public sealed class SqlServerCleanInstallFixture : IAsyncLifetime
{
    public string TempDbName { get; private set; } = string.Empty;
    public string ConnectionString { get; private set; } = string.Empty;

    private static readonly string MasterConnectionString =
        "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";

    public async Task InitializeAsync()
    {
        TempDbName = $"SelfStorageTest_{Guid.NewGuid():N}";
        ConnectionString = $"Server=localhost;Database={TempDbName};Trusted_Connection=True;TrustServerCertificate=True;";

        var sqlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "DB", "SelfStoragePRN222_SQLServer_CleanInstall.sql"));
        if (!File.Exists(sqlPath))
            throw new FileNotFoundException("CleanInstall SQL script not found.", sqlPath);

        var script = await File.ReadAllTextAsync(sqlPath);
        script = script.Replace("SelfStoragePRN222", TempDbName, StringComparison.Ordinal);

        var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Select(b => b.Trim())
            .Where(b => !string.IsNullOrWhiteSpace(b));

        await using var conn = new SqlConnection(MasterConnectionString);
        await conn.OpenAsync();
        foreach (var batch in batches)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 120;
            cmd.CommandText = batch;
            await cmd.ExecuteNonQueryAsync();
        }

        // Set real BCrypt password hashes on seeded demo users 1..4 & add 1 extra UnitType 1 unit (A-103) + VAT rule so full Flow 1+2 runs cleanly
        var pwHash = BCrypt.Net.BCrypt.HashPassword("Pass@123");
        await using var dbConn = new SqlConnection(ConnectionString);
        await dbConn.OpenAsync();
        await using var setupCmd = dbConn.CreateCommand();
        setupCmd.CommandText = """
            UPDATE [core].[users] SET password_hash = @pw WHERE id IN (1, 2, 3, 4);
            INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed)
            VALUES (1, 1, 3, 'A-103-SQL', '1', 'A', 'available', 1);
            INSERT INTO [core].[fee_rules] (facility_id, code, fee_type, calculation_method, rate_percent, conditions, is_active, valid_from, created_by)
            VALUES (NULL, 'VAT_10', 'tax', 'percentage', 10.0, '{}', 1, '2026-01-01', 4);
            """;
        setupCmd.Parameters.AddWithValue("@pw", pwHash);
        await setupCmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(TempDbName)) return;
        try
        {
            SqlConnection.ClearAllPools();
            await using var conn = new SqlConnection(MasterConnectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                IF DB_ID(N'{TempDbName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{TempDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{TempDbName}];
                END;
                """;
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best-effort cleanup of temp database
        }
    }
}

public sealed class SqlServerWebFactory(string connectionString) : WebApplicationFactory<Program>
{
    public string SePayApiKey { get; } = "SqlServerTest_SePay_Key_987654321!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["JwtSettings:Issuer"] = "SelfStorageSystem",
                ["JwtSettings:Audience"] = "SelfStorageSystem.Api",
                ["JwtSettings:Secret"] = "SuperSecretSqlServerKey_AtLeast32Chars!",
                ["JwtSettings:AccessTokenMinutes"] = "60",
                ["SecuritySettings:PinProtectionKey"] = "TestPinProtectionKey_AtLeast32Chars!!",
                ["SecuritySettings:QrSigningKey"] = "TestQrSigningSecret_AtLeast32CharsLong!",
                ["PaymentSettings:SePay:ApiKey"] = SePayApiKey,
                ["PaymentSettings:SePay:InvoicePrefix"] = "DH",
                ["PaymentSettings:VietQr:BaseUrl"] = "https://vietqr.app/img",
                ["PaymentSettings:VietQr:BankCode"] = "MBBank",
                ["PaymentSettings:VietQr:AccountNo"] = "0001234567",
                ["PaymentSettings:VietQr:AccountName"] = "SELF STORAGE SQL TEST"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<SelfStorageDbContext>>();
            services.RemoveAll<SelfStorageDbContext>();
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IEmailService>();

            services.AddDbContext<SelfStorageDbContext>(options => options.UseSqlServer(connectionString));
            services.AddScoped(_ => Mock.Of<IEmailService>());
        });
    }
}

public class Phase3Group1SqlServerEndToEndTests : IClassFixture<SqlServerCleanInstallFixture>
{
    private readonly SqlServerCleanInstallFixture _fixture;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public Phase3Group1SqlServerEndToEndTests(SqlServerCleanInstallFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task LiveSqlServer_TriggersAndConstraints_PassFullFlow1And2AndEdgeCases()
    {
        await using var factory = new SqlServerWebFactory(_fixture.ConnectionString);

        // Verify DbSeeder.SeedAsync runs idempotently against real SQL Server CleanInstall schema
        using (var seedScope = factory.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
            await DbSeeder.SeedAsync(db);
        }

        async Task<string> LoginAsync(string email)
        {
            var c = factory.CreateClient();
            var resp = await c.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Email = email, Password = "Pass@123" });
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(JsonOpts))!.Data!.AccessToken;
        }

        var cust1Token = await LoginAsync("customer.one@example.test");
        var cust2Token = await LoginAsync("customer.two@example.test");
        var staffToken = await LoginAsync("staff.hcm@example.test");
        var mgrToken = await LoginAsync("manager.hcm@example.test");

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
        var startFuture = today.AddDays(5);

        // 1. Real SQL Server concurrency race: two customers simultaneously reserve unitId=1 (A-101)
        var c1 = factory.CreateClient();
        c1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cust1Token);
        var c2 = factory.CreateClient();
        c2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cust2Token);

        var raceReq = new CreateReservationRequest
        {
            FacilityId = 1,
            UnitTypeId = 1,
            StorageUnitId = 1,
            StartDate = startFuture,
            DurationMonths = 1
        };

        var raceResults = await Task.WhenAll(
            c1.PostAsJsonAsync("/api/v1/reservations", raceReq),
            c2.PostAsJsonAsync("/api/v1/reservations", raceReq));

        Assert.Single(raceResults, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(raceResults, r => r.StatusCode == HttpStatusCode.Conflict);

        var winnerResp = raceResults.Single(r => r.StatusCode == HttpStatusCode.Created);
        var overpaidRsv = (await winnerResp.Content.ReadFromJsonAsync<ApiResponse<CreateReservationResponse>>(JsonOpts))!.Data!;

        // 2. Concurrent idempotency race on unitId=2 (A-102)
        var idemKey = $"sql-idem-{Guid.NewGuid():N}";
        var idemReq = new CreateReservationRequest
        {
            FacilityId = 1,
            UnitTypeId = 1,
            StorageUnitId = 2,
            StartDate = startFuture,
            DurationMonths = 1,
            IdempotencyKey = idemKey
        };
        var idemResults = await Task.WhenAll(
            c1.PostAsJsonAsync("/api/v1/reservations", idemReq),
            c1.PostAsJsonAsync("/api/v1/reservations", idemReq));
        Assert.All(idemResults, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var idemRsv1 = (await idemResults[0].Content.ReadFromJsonAsync<ApiResponse<CreateReservationResponse>>(JsonOpts))!.Data!;
        var idemRsv2 = (await idemResults[1].Content.ReadFromJsonAsync<ApiResponse<CreateReservationResponse>>(JsonOpts))!.Data!;
        Assert.Equal(idemRsv1.ReservationId, idemRsv2.ReservationId);

        // 3. Partial + Overpayment SePay webhook on overpaidRsv (unitId=1), then cancel -> triggers trg_refunds_guard check!
        async Task<SePayWebhookResultDto> SendWebhookAsync(long gwId, decimal amount, string content, string? code = null)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/sepay")
            {
                Content = JsonContent.Create(new
                {
                    id = gwId,
                    gateway = "MBBank",
                    transferType = "in",
                    transferAmount = amount,
                    code,
                    content,
                    referenceCode = $"REF-{gwId}"
                })
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Apikey", factory.SePayApiKey);
            var resp = await c1.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<ApiResponse<SePayWebhookResultDto>>(JsonOpts))!.Data!;
        }

        var partialAmt = 400_000m;
        var partialRes = await SendWebhookAsync(91001, partialAmt, "", $"DH{overpaidRsv.InvoiceId}"); // uses payload.Code only!
        Assert.Equal("partial_recorded", partialRes.Action);

        var remainingPlusOverpay = (overpaidRsv.FirstPaymentAmount - partialAmt) + 150_000m;
        var overpayRes = await SendWebhookAsync(91002, remainingPlusOverpay, $"DH{overpaidRsv.InvoiceId}");
        Assert.Equal("overpayment_refund_created", overpayRes.Action);

        // Duplicate replay ignored
        var replayRes = await SendWebhookAsync(91002, remainingPlusOverpay, $"DH{overpaidRsv.InvoiceId}");
        Assert.Equal("duplicate_ignored", replayRes.Action);

        // Cancel overpaid confirmed reservation on SQL Server (proves trg_refunds_guard 51047 does NOT blow up!)
        var cancelClient = factory.CreateClient();
        var winnerToken = raceResults[0].StatusCode == HttpStatusCode.Created ? cust1Token : cust2Token;
        cancelClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", winnerToken);
        var cancelResp = await cancelClient.PostAsJsonAsync($"/api/v1/reservations/{overpaidRsv.ReservationId}/cancel",
            new { reason = "Testing overpaid cancel refund cap on SQL Server" });
        cancelResp.EnsureSuccessStatusCode();

        // 4. Complete full Flow 1 -> Flow 2 on idemRsv1 (unitId=2 A-102), with future StartDate > today!
        var fullRes = await SendWebhookAsync(91003, idemRsv1.FirstPaymentAmount, $"DH{idemRsv1.InvoiceId}");
        Assert.Equal("confirmed_full", fullRes.Action);

        var statusResp = await c1.GetAsync($"/api/v1/payments/{fullRes.PaymentId}/status");
        statusResp.EnsureSuccessStatusCode();
        var payStatus = (await statusResp.Content.ReadFromJsonAsync<ApiResponse<PaymentStatusDto>>(JsonOpts))!.Data!;
        Assert.False(string.IsNullOrWhiteSpace(payStatus.CheckInQrToken));

        var staffClient = factory.CreateClient();
        staffClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        var verifyQr = await staffClient.PostAsJsonAsync("/api/v1/staff/check-ins/verify-qr",
            new VerifyCheckInQrRequest(payStatus.CheckInQrToken!, 1));
        verifyQr.EnsureSuccessStatusCode();

        // Swap to unitId=1 (now available after cancel) -> confirm check-in
        var assignResp = await staffClient.PostAsJsonAsync($"/api/v1/staff/reservations/{idemRsv1.ReservationId}/assign-unit",
            new AssignReservationUnitRequest(1, "Swap back to A-101"));
        assignResp.EnsureSuccessStatusCode();

        var confirmResp = await staffClient.PostAsJsonAsync("/api/v1/staff/check-ins/confirm", new ConfirmCheckInRequest
        {
            ReservationId = idemRsv1.ReservationId,
            IdentityVerification = new IdentityVerificationInput { Method = "government_id", DocumentNumber = "079200009999", FullNameMatched = true, Notes = "KYC matched" },
            Inspection = new CheckInInspectionInput { OverallCondition = "good", Summary = "Clean", Items = [new CheckInInspectionItemInput { ItemName = "Door Lock", Condition = "good" }] },
            Notes = "Handover complete"
        });
        confirmResp.EnsureSuccessStatusCode();
        var confirmed = (await confirmResp.Content.ReadFromJsonAsync<ApiResponse<ConfirmCheckInResponse>>(JsonOpts))!.Data!;

        // 5. Staff creates incident ticket (verifies CK_support_tickets_category passes on SQL Server!)
        var tckResp = await staffClient.PostAsJsonAsync("/api/v1/staff/tickets", new StaffCreateCheckInIncidentTicketRequest
        {
            FacilityId = 1,
            AgreementId = confirmed.AgreementId,
            StorageUnitId = confirmed.UnitId,
            Description = "Door seal damaged during move-in"
        });
        Assert.Equal(HttpStatusCode.Created, tckResp.StatusCode);
        var ticket = (await tckResp.Content.ReadFromJsonAsync<ApiResponse<CreateCheckInTicketResponse>>(JsonOpts))!.Data!;

        // 6. Manager reassigns unit A-101 -> A-103 (StartDate > today! Proves trg_unit_allocations_validate_scope THROW 51027 & CK_support_tickets_resolved_consistency pass!)
        var mgrClient = factory.CreateClient();
        mgrClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgrToken);

        long extraUnitId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
            extraUnitId = await db.StorageUnits.Where(u => u.FacilityId == 1 && u.UnitCode == "A-103-SQL").Select(u => u.Id).SingleAsync();
        }

        var reassignResp = await mgrClient.PostAsJsonAsync($"/api/v1/manager/agreements/{confirmed.AgreementId}/reassign-unit",
            new ManagerReassignUnitRequest { NewStorageUnitId = extraUnitId, TicketId = ticket.TicketId, Reason = "Door seal repair needed" });
        reassignResp.EnsureSuccessStatusCode();
        var reassigned = (await reassignResp.Content.ReadFromJsonAsync<ApiResponse<ManagerReassignUnitResponseDto>>(JsonOpts))!.Data!;
        Assert.Equal(extraUnitId, reassigned.NewStorageUnitId);

        // 7. Manager emergency-cancel-refund -> RefundStatus == "approved", passes trg_refunds_guard & nvarchar(255) limits; repeat returns 409!
        var cancelMgrResp = await mgrClient.PostAsJsonAsync($"/api/v1/manager/agreements/{confirmed.AgreementId}/emergency-cancel-refund",
            new ManagerEmergencyCancelRefundRequest { TicketId = ticket.TicketId, Reason = "Facility water pipe burst near zone A" });
        cancelMgrResp.EnsureSuccessStatusCode();
        var mgrCanceled = (await cancelMgrResp.Content.ReadFromJsonAsync<ApiResponse<ManagerEmergencyCancelRefundResponseDto>>(JsonOpts))!.Data!;
        Assert.Equal("approved", mgrCanceled.RefundStatus);
        Assert.Equal(idemRsv1.FirstPaymentAmount, mgrCanceled.TotalRefundAmount);

        var secondMgrCancel = await mgrClient.PostAsJsonAsync($"/api/v1/manager/agreements/{confirmed.AgreementId}/emergency-cancel-refund",
            new ManagerEmergencyCancelRefundRequest { Reason = "Duplicate cancel call" });
        Assert.Equal(HttpStatusCode.Conflict, secondMgrCancel.StatusCode);
    }
}
