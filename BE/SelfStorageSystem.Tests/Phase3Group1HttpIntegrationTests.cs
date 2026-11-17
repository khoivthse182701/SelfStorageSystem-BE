using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Auth;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Facilities;
using SelfStorageSystem.Contracts.Customer.Payments;
using SelfStorageSystem.Contracts.Customer.Reservations;
using SelfStorageSystem.Contracts.Manager;
using SelfStorageSystem.Contracts.Staff;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Hubs;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Tests;

public sealed class Phase3Group1WebFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"Phase3G1_{Guid.NewGuid():N}";
    public string SePayApiKey { get; set; } = "Test_SePay_Key_1234567890!";
    public Action<IServiceCollection>? ExtraTestServices { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=Dummy;",
                ["JwtSettings:Issuer"] = "SelfStorageSystem",
                ["JwtSettings:Audience"] = "SelfStorageSystem.Api",
                ["JwtSettings:Secret"] = "SuperSecretIntegrationKey_AtLeast32Chars!",
                ["JwtSettings:AccessTokenMinutes"] = "60",
                ["SecuritySettings:PinProtectionKey"] = "TestPinProtectionKey_AtLeast32Chars!!",
                ["SecuritySettings:QrSigningKey"] = "TestQrSigningSecret_AtLeast32CharsLong!",
                ["PaymentSettings:SePay:ApiKey"] = SePayApiKey,
                ["PaymentSettings:SePay:InvoicePrefix"] = "DH",
                ["PaymentSettings:VietQr:BaseUrl"] = "https://vietqr.app/img",
                ["PaymentSettings:VietQr:BankCode"] = "MBBank",
                ["PaymentSettings:VietQr:AccountNo"] = "0001234567",
                ["PaymentSettings:VietQr:AccountName"] = "SELF STORAGE TEST"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<SelfStorageDbContext>>();
            services.RemoveAll<SelfStorageDbContext>();
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IEmailService>();

            services.AddDbContext<SelfStorageDbContext>(options =>
                options.UseInMemoryDatabase(_dbName)
                       .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

            services.AddScoped(_ => Mock.Of<IEmailService>());
            ExtraTestServices?.Invoke(services);
        });
    }

    public async Task SeedBaselineAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var custRole = new Role { Id = 1, Code = RoleConstants.Customer, DisplayName = "Customer" };
        var staffRole = new Role { Id = 2, Code = RoleConstants.FacilityStaff, DisplayName = "Staff" };
        var mgrRole = new Role { Id = 3, Code = RoleConstants.FacilityManager, DisplayName = "Manager" };
        db.Roles.AddRange(custRole, staffRole, mgrRole);

        var pwHash = BCrypt.Net.BCrypt.HashPassword("Pass@123");
        var custUser = new User { Id = 10, Email = "cust1@test.local", PhoneNumber = "0901000010", PasswordHash = pwHash, Status = UserStatusConstants.Active, CreatedAt = now, UpdatedAt = now };
        var staffUser = new User { Id = 20, Email = "staff1@test.local", PhoneNumber = "0902000020", PasswordHash = pwHash, Status = UserStatusConstants.Active, CreatedAt = now, UpdatedAt = now };
        var mgrUser = new User { Id = 30, Email = "mgr1@test.local", PhoneNumber = "0903000030", PasswordHash = pwHash, Status = UserStatusConstants.Active, CreatedAt = now, UpdatedAt = now };
        db.Users.AddRange(custUser, staffUser, mgrUser);

        db.CustomerProfiles.Add(new CustomerProfile { UserId = 10, FullName = "Nguyen Van Customer", CreatedAt = now, UpdatedAt = now });
        db.EmployeeProfiles.AddRange(
            new EmployeeProfile { UserId = 20, EmployeeCode = "STF-01", FullName = "Tran Staff Fac1", EmploymentStatus = "active", CreatedAt = now, UpdatedAt = now },
            new EmployeeProfile { UserId = 30, EmployeeCode = "MGR-01", FullName = "Le Manager Fac1", EmploymentStatus = "active", CreatedAt = now, UpdatedAt = now });

        db.UserRoles.AddRange(
            new UserRole { UserId = 10, RoleId = 1, GrantedAt = now },
            new UserRole { UserId = 20, RoleId = 2, GrantedAt = now },
            new UserRole { UserId = 30, RoleId = 3, GrantedAt = now });

        var fac1 = new Facility { Id = 1, Code = "FAC-01", Name = "Thu Duc Hub", AddressLine = "1 Vo Van Ngan", City = "HCMC", Timezone = "Asia/Ho_Chi_Minh", Status = FacilityStatusConstants.Active, CreatedAt = now, UpdatedAt = now };
        var fac2 = new Facility { Id = 2, Code = "FAC-02", Name = "District 7 Hub", AddressLine = "99 Nguyen Van Linh", City = "HCMC", Timezone = "Asia/Ho_Chi_Minh", Status = FacilityStatusConstants.Active, CreatedAt = now, UpdatedAt = now };
        db.Facilities.AddRange(fac1, fac2);

        // Staff & Manager strictly assigned ONLY to Facility 1
        db.StaffFacilityAssignments.AddRange(
            new StaffFacilityAssignment { Id = 1, EmployeeId = 20, FacilityId = 1, AssignmentRole = StaffAssignmentRoles.FacilityStaff, StartsAt = now.AddDays(-5) },
            new StaffFacilityAssignment { Id = 2, EmployeeId = 30, FacilityId = 1, AssignmentRole = StaffAssignmentRoles.FacilityManager, StartsAt = now.AddDays(-5) });

        var ut1 = new UnitType { Id = 1, Code = "S-DRY", Name = "Small Dry 3m2", WidthM = 1.5m, LengthM = 2m, HeightM = 2.5m, AreaM2 = 3m, VolumeM3 = 7.5m, IsActive = true, CreatedAt = now, UpdatedAt = now };
        db.UnitTypes.Add(ut1);

        var rate1 = new FacilityRate { Id = 1, FacilityId = 1, UnitTypeId = 1, MonthlyRate = 1_000_000m, DepositAmount = 1_000_000m, BookingFee = 50_000m, ValidFrom = today.AddDays(-30), CreatedAt = now };
        db.FacilityRates.Add(rate1);

        db.FeeRules.Add(new FeeRule
        {
            Id = 1,
            FacilityId = null,
            Code = "VAT_10",
            FeeType = FeeRuleTypeConstants.Tax,
            CalculationMethod = "percentage",
            RatePercent = 10m,
            Conditions = "{}",
            IsActive = true,
            ValidFrom = today.AddDays(-30),
            CreatedAt = now
        });

        var promo = new Promotion
        {
            Id = 1,
            Code = "SALE10",
            Name = "10% off first month",
            DiscountType = PromotionDiscountTypeConstants.Percentage,
            DiscountValue = 10m,
            MaxDiscountAmount = 200_000m,
            IsActive = true,
            ValidFrom = now.AddDays(-5),
            ValidTo = now.AddDays(30),
            CreatedAt = now
        };
        db.Promotions.Add(promo);

        db.PolicyVersions.Add(new PolicyVersion
        {
            Id = 1,
            PolicyType = "rental_terms",
            Version = "1.0",
            Content = "Standard terms",
            ValidFrom = today.AddDays(-30),
            CreatedAt = now
        });

        db.StorageUnits.AddRange(
            new StorageUnit { Id = 101, FacilityId = 1, UnitTypeId = 1, UnitCode = "A-101", FloorLabel = "1", ZoneLabel = "A", PhysicalStatus = StorageUnitStatusConstants.Available, IsListed = true, CreatedAt = now, UpdatedAt = now },
            new StorageUnit { Id = 102, FacilityId = 1, UnitTypeId = 1, UnitCode = "A-102", FloorLabel = "1", ZoneLabel = "A", PhysicalStatus = StorageUnitStatusConstants.Available, IsListed = true, CreatedAt = now, UpdatedAt = now },
            new StorageUnit { Id = 103, FacilityId = 1, UnitTypeId = 1, UnitCode = "A-103", FloorLabel = "1", ZoneLabel = "A", PhysicalStatus = StorageUnitStatusConstants.Available, IsListed = true, CreatedAt = now, UpdatedAt = now });

        await db.SaveChangesAsync();
    }
}

public class Phase3Group1HttpIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static async Task<string> LoginAndGetTokenAsync(HttpClient client, string email, string password)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Email = email, Password = password });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(JsonOpts);
        Assert.NotNull(body?.Data?.AccessToken);
        return body!.Data!.AccessToken;
    }

    [Fact]
    public async Task GlobalExceptionHandler_InDevelopment_NeverLeaksExceptionMessageOrInnerException()
    {
        var throwingMock = new Mock<ICustomerCatalogService>();
        throwingMock
            .Setup(s => s.GetFacilitiesPagedAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SENSITIVE_DB_HOST=10.0.0.5", new Exception("INNER_SQL_PASSWORD_LEAK")));

        await using var factory = new Phase3Group1WebFactory
        {
            ExtraTestServices = services =>
            {
                services.RemoveAll<ICustomerCatalogService>();
                services.AddScoped(_ => throwingMock.Object);
            }
        };
        await factory.SeedBaselineAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/facilities");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SENSITIVE_DB_HOST", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INNER_SQL_PASSWORD_LEAK", raw, StringComparison.OrdinalIgnoreCase);

        var envelope = JsonSerializer.Deserialize<ApiResponse<object>>(raw, JsonOpts);
        Assert.NotNull(envelope);
        Assert.False(envelope!.Success);
        Assert.Equal("System.InternalError", envelope.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(envelope.TraceId));
    }

    [Fact]
    public async Task FluentValidation_Returns422_WithFieldErrorsDictionaryAndTraceId()
    {
        await using var factory = new Phase3Group1WebFactory();
        await factory.SeedBaselineAsync();
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/pricing/quote", new
        {
            facilityId = 0,
            unitTypeId = 1,
            startDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            durationMonths = 99
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
        var raw = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(raw);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Validation.Failed", doc.RootElement.GetProperty("errorCode").GetString());
        Assert.True(doc.RootElement.TryGetProperty("traceId", out var trace) && !string.IsNullOrWhiteSpace(trace.GetString()));
        var errors = doc.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("facilityId", out _));
        Assert.True(errors.TryGetProperty("durationMonths", out _));
    }

    [Fact]
    public async Task ThreeRoleAuth_AndFacilityAssignment_AndHubSecurity_EnforcedStrictly()
    {
        await using var factory = new Phase3Group1WebFactory();
        await factory.SeedBaselineAsync();
        var client = factory.CreateClient();

        var custToken = await LoginAndGetTokenAsync(client, "cust1@test.local", "Pass@123");
        var staffToken = await LoginAndGetTokenAsync(client, "staff1@test.local", "Pass@123");
        var mgrToken = await LoginAndGetTokenAsync(client, "mgr1@test.local", "Pass@123");

        // GET /api/v1/auth/me merges roles, permissions, and assignedFacilityIds
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);
        var meResp = await client.GetFromJsonAsync<ApiResponse<AuthUserDto>>("/api/v1/auth/me", JsonOpts);
        Assert.NotNull(meResp?.Data);
        Assert.Contains("Staff", meResp!.Data!.Roles);
        Assert.Contains("qr:verify", meResp.Data.Permissions);
        Assert.Contains(1L, meResp.Data.AssignedFacilityIds);

        // Staff assigned ONLY to Facility 1 cannot query Facility 2 -> 403
        var crossFacilityResp = await client.GetAsync("/api/v1/staff/reservations/lookup?facilityId=2");
        Assert.Equal(HttpStatusCode.Forbidden, crossFacilityResp.StatusCode);

        // Staff cannot access Manager-only endpoint -> 403
        var mgrOnlyResp = await client.PostAsJsonAsync("/api/v1/manager/agreements/1/reassign-unit", new ManagerReassignUnitRequest { NewStorageUnitId = 102, Reason = "Test" });
        Assert.Equal(HttpStatusCode.Forbidden, mgrOnlyResp.StatusCode);

        // Customer cannot access Staff endpoint -> 403
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", custToken);
        var staffOnlyResp = await client.GetAsync("/api/v1/staff/reservations/lookup");
        Assert.Equal(HttpStatusCode.Forbidden, staffOnlyResp.StatusCode);

        // SignalR /hubs/notifications negotiate rejects unauthenticated calls
        var anonClient = factory.CreateClient();
        var negResp = await anonClient.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, negResp.StatusCode);

        // Manager role present cleanly in /auth/me
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgrToken);
        var mgrMe = await client.GetFromJsonAsync<ApiResponse<AuthUserDto>>("/api/v1/auth/me", JsonOpts);
        Assert.Contains("Manager", mgrMe!.Data!.Roles);
        Assert.Contains(1L, mgrMe.Data.AssignedFacilityIds);
    }

    [Fact]
    public async Task NotificationHub_JoinFacility_RejectsCustomerAndCrossFacilityStaff()
    {
        await using var factory = new Phase3Group1WebFactory();
        await factory.SeedBaselineAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();

        var groupsMock = new Mock<IGroupManager>();
        var hub = new NotificationHub(db, NullLogger<NotificationHub>.Instance)
        {
            Groups = groupsMock.Object
        };

        var customerPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "10"),
            new Claim(ClaimTypes.Role, "Customer")
        ], "Bearer"));

        var ctxMock = new Mock<HubCallerContext>();
        ctxMock.Setup(c => c.User).Returns(customerPrincipal);
        ctxMock.Setup(c => c.ConnectionId).Returns("conn-1");
        ctxMock.Setup(c => c.ConnectionAborted).Returns(CancellationToken.None);
        hub.Context = ctxMock.Object;

        // Customer rejected
        await Assert.ThrowsAsync<HubException>(() => hub.JoinFacility(1));

        // Staff assigned to Facility 1 rejected from Facility 2
        var staffPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "20"),
            new Claim(ClaimTypes.Role, "Staff")
        ], "Bearer"));
        ctxMock.Setup(c => c.User).Returns(staffPrincipal);
        await Assert.ThrowsAsync<HubException>(() => hub.JoinFacility(2));

        // Staff assigned to Facility 1 succeeds for Facility 1
        await hub.JoinFacility(1);
        groupsMock.Verify(g => g.AddToGroupAsync("conn-1", "facility_1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SePayWebhook_V1_RejectsPlaceholderKey503_AndInvalidKey401()
    {
        await using var unconfiguredFactory = new Phase3Group1WebFactory { SePayApiKey = "__SET_VIA_ENVIRONMENT_OR_SECRETS__" };
        await unconfiguredFactory.SeedBaselineAsync();
        var c1 = unconfiguredFactory.CreateClient();

        var payload = new SePayWebhookPayload { Id = 1, Content = "DH1", TransferAmount = 1000m, TransferType = "in" };
        var r503 = await c1.PostAsJsonAsync("/api/v1/webhooks/sepay", payload);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, r503.StatusCode);

        await using var configuredFactory = new Phase3Group1WebFactory { SePayApiKey = "RealSecretKey_123456" };
        await configuredFactory.SeedBaselineAsync();
        var c2 = configuredFactory.CreateClient();
        c2.DefaultRequestHeaders.Add("Authorization", "Apikey WrongKey");
        var r401 = await c2.PostAsJsonAsync("/api/v1/webhooks/sepay", payload);
        Assert.Equal(HttpStatusCode.Unauthorized, r401.StatusCode);
    }

    [Fact]
    public async Task EndToEnd_Flow1AndFlow2_CustomerReservePay_WebhookIdempotency_StaffVerifyAssignCheckIn_ManagerEscalate()
    {
        await using var factory = new Phase3Group1WebFactory { SePayApiKey = "RealSePayKey_999!" };
        await factory.SeedBaselineAsync();
        var client = factory.CreateClient();

        var custToken = await LoginAndGetTokenAsync(client, "cust1@test.local", "Pass@123");
        var staffToken = await LoginAndGetTokenAsync(client, "staff1@test.local", "Pass@123");
        var mgrToken = await LoginAndGetTokenAsync(client, "mgr1@test.local", "Pass@123");

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        // 1. Catalog + Unified Quote (1,000,000 rate, 10% promo = 100,000 off, +50k booking = 950k taxable, +10% VAT = 95k tax, +1M deposit = 2,045,000)
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", custToken);
        var facResp = await client.GetFromJsonAsync<ApiResponse<PagedResult<FacilitySummaryDto>>>("/api/v1/facilities", JsonOpts);
        Assert.Equal(2, facResp!.Data!.TotalCount);

        var quoteResp = await client.PostAsJsonAsync("/api/v1/pricing/quote", new PricingQuoteRequest
        {
            FacilityId = 1,
            UnitTypeId = 1,
            StartDate = startDate,
            DurationMonths = 3,
            PromotionCode = "SALE10"
        });
        quoteResp.EnsureSuccessStatusCode();
        var quote = (await quoteResp.Content.ReadFromJsonAsync<ApiResponse<PricingQuoteDto>>(JsonOpts))!.Data!;
        Assert.Equal(100_000m, quote.DiscountAmount);
        Assert.Equal(95_000m, quote.TaxAmount);
        Assert.Equal(2_045_000m, quote.FirstPaymentAmount);

        // 2. Create Reservation with IdempotencyKey + Replay
        var reqBody = new CreateReservationRequest
        {
            FacilityId = 1,
            UnitTypeId = 1,
            StorageUnitId = 101,
            StartDate = startDate,
            DurationMonths = 3,
            PromotionCode = "SALE10",
            IdempotencyKey = "idem-e2e-001"
        };
        var createResp1 = await client.PostAsJsonAsync("/api/v1/reservations", reqBody);
        Assert.Equal(HttpStatusCode.Created, createResp1.StatusCode);
        var rsv = (await createResp1.Content.ReadFromJsonAsync<ApiResponse<CreateReservationResponse>>(JsonOpts))!.Data!;
        Assert.Equal(quote.FirstPaymentAmount, rsv.FirstPaymentAmount);
        Assert.Equal(quote.TaxAmount, rsv.TaxAmount);

        var createResp2 = await client.PostAsJsonAsync("/api/v1/reservations", reqBody);
        var rsvReplay = (await createResp2.Content.ReadFromJsonAsync<ApiResponse<CreateReservationResponse>>(JsonOpts))!.Data!;
        Assert.Equal(rsv.ReservationId, rsvReplay.ReservationId);

        // 3. Checkout + Status Poll (pending)
        var checkoutResp = await client.PostAsJsonAsync("/api/v1/payments/checkout", new CreateCheckoutRequest
        {
            ReservationId = rsv.ReservationId,
            PaymentMethod = "bank_transfer"
        });
        checkoutResp.EnsureSuccessStatusCode();
        var checkout = (await checkoutResp.Content.ReadFromJsonAsync<ApiResponse<CheckoutResponse>>(JsonOpts))!.Data!;
        Assert.Equal(2_045_000m, checkout.Amount);

        // 4. SePay Webhook: Partial Payment (500k) -> Overpayment (1,600k => 55k refund) -> Duplicate Replay
        var webhookClient = factory.CreateClient();
        webhookClient.DefaultRequestHeaders.Add("Authorization", "Apikey RealSePayKey_999!");

        var partialResp = await webhookClient.PostAsJsonAsync("/api/v1/webhooks/sepay", new SePayWebhookPayload
        {
            Id = 5001,
            ReferenceCode = "SEPAY-5001",
            Content = checkout.VietQr!.TransferContent,
            TransferType = "in",
            TransferAmount = 500_000m
        });
        partialResp.EnsureSuccessStatusCode();
        var partialData = (await partialResp.Content.ReadFromJsonAsync<ApiResponse<SePayWebhookResultDto>>(JsonOpts))!.Data!;
        Assert.Equal("partial_recorded", partialData.Action);

        var settlePayload = new SePayWebhookPayload
        {
            Id = 5002,
            ReferenceCode = "SEPAY-5002",
            Content = checkout.VietQr!.TransferContent,
            TransferType = "in",
            TransferAmount = 1_600_000m
        };
        var settleResp = await webhookClient.PostAsJsonAsync("/api/v1/webhooks/sepay", settlePayload);
        settleResp.EnsureSuccessStatusCode();
        var settleData = (await settleResp.Content.ReadFromJsonAsync<ApiResponse<SePayWebhookResultDto>>(JsonOpts))!.Data!;
        Assert.Equal("overpayment_refund_created", settleData.Action);

        var dupResp = await webhookClient.PostAsJsonAsync("/api/v1/webhooks/sepay", settlePayload);
        var dupData = (await dupResp.Content.ReadFromJsonAsync<ApiResponse<SePayWebhookResultDto>>(JsonOpts))!.Data!;
        Assert.Equal("duplicate_ignored", dupData.Action);

        // 5. Customer retrieves signed CheckInQrToken
        var statusDto = (await client.GetFromJsonAsync<ApiResponse<PaymentStatusDto>>($"/api/v1/payments/{checkout.PaymentId}/status", JsonOpts))!.Data!;
        Assert.Equal("confirmed", statusDto.ReservationStatus);
        Assert.StartsWith("v1.", statusDto.CheckInQrToken);

        // 6. Staff verifies QR (unsigned rejected 422, signed accepted 200), swaps unit 101 -> 102, completes KYC Check-in + PIN!
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        var forgedQrResp = await client.PostAsJsonAsync("/api/v1/staff/check-ins/verify-qr", new VerifyCheckInQrRequest("CHECKIN:FORGED:10:1", 1));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, forgedQrResp.StatusCode);

        var verifyQrResp = await client.PostAsJsonAsync("/api/v1/staff/check-ins/verify-qr", new VerifyCheckInQrRequest(statusDto.CheckInQrToken!, 1));
        verifyQrResp.EnsureSuccessStatusCode();
        var verified = (await verifyQrResp.Content.ReadFromJsonAsync<ApiResponse<VerifyCheckInQrResponse>>(JsonOpts))!.Data!;
        Assert.True(verified.Valid);
        Assert.Equal(rsv.ReservationId, verified.Reservation.ReservationId);

        var swapResp = await client.PostAsJsonAsync($"/api/v1/staff/reservations/{rsv.ReservationId}/assign-unit",
            new AssignReservationUnitRequest(102, "Door latch sticking on A-101"));
        swapResp.EnsureSuccessStatusCode();
        var swapData = (await swapResp.Content.ReadFromJsonAsync<ApiResponse<AssignReservationUnitResponse>>(JsonOpts))!.Data!;
        Assert.Equal(101L, swapData.OldUnitId);
        Assert.Equal(102L, swapData.NewUnitId);
        Assert.Equal("reserved", swapData.UnitStatus);

        // KYC fullNameMatched=false rejected 422
        var badKycResp = await client.PostAsJsonAsync("/api/v1/staff/check-ins/confirm", new ConfirmCheckInRequest
        {
            ReservationId = rsv.ReservationId,
            IdentityVerification = new IdentityVerificationInput { Method = "government_id", DocumentNumber = "079200008901", FullNameMatched = false },
            Inspection = new CheckInInspectionInput { OverallCondition = "good" }
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badKycResp.StatusCode);

        // Atomic check-in confirm succeeds
        var confirmResp = await client.PostAsJsonAsync("/api/v1/staff/check-ins/confirm", new ConfirmCheckInRequest
        {
            ReservationId = rsv.ReservationId,
            IdentityVerification = new IdentityVerificationInput { Method = "government_id", DocumentNumber = "079200008901", FullNameMatched = true, Notes = "Matched CCCD" },
            Inspection = new CheckInInspectionInput
            {
                OverallCondition = "good",
                Summary = "Unit clean",
                Items = [new CheckInInspectionItemInput { ItemName = "Door Lock", Condition = "good" }]
            },
            Notes = "Handover complete"
        });
        confirmResp.EnsureSuccessStatusCode();
        var confirmData = (await confirmResp.Content.ReadFromJsonAsync<ApiResponse<ConfirmCheckInResponse>>(JsonOpts))!.Data!;
        Assert.Equal("active", confirmData.AgreementStatus);
        Assert.Equal("converted", confirmData.ReservationStatus);
        Assert.Equal("occupied", confirmData.UnitStatus);
        Assert.Equal(102L, confirmData.UnitId);
        Assert.Matches(@"^\d{6}$", confirmData.IssuedPin);

        // Double check-in rejected 409
        var secondCheckIn = await client.PostAsJsonAsync("/api/v1/staff/check-ins/confirm", new ConfirmCheckInRequest
        {
            ReservationId = rsv.ReservationId,
            IdentityVerification = new IdentityVerificationInput { Method = "government_id", DocumentNumber = "079200008901", FullNameMatched = true },
            Inspection = new CheckInInspectionInput { OverallCondition = "good" }
        });
        Assert.Equal(HttpStatusCode.Conflict, secondCheckIn.StatusCode);

        // 7. Staff creates incident ticket -> Manager reassigns unit 102 -> 103 -> Manager emergency cancels & refunds
        var ticketResp = await client.PostAsJsonAsync("/api/v1/staff/tickets", new StaffCreateCheckInIncidentTicketRequest
        {
            FacilityId = 1,
            AgreementId = confirmData.AgreementId,
            StorageUnitId = 102,
            UnitCode = "A-102",
            Description = "Leak spotted right after move-in"
        });
        Assert.Equal(HttpStatusCode.Created, ticketResp.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgrToken);
        var mgrReassign = await client.PostAsJsonAsync($"/api/v1/manager/agreements/{confirmData.AgreementId}/reassign-unit",
            new ManagerReassignUnitRequest { NewStorageUnitId = 103, Reason = "Emergency relocation due to leak" });
        mgrReassign.EnsureSuccessStatusCode();
        var reassignData = (await mgrReassign.Content.ReadFromJsonAsync<ApiResponse<ManagerReassignUnitResponseDto>>(JsonOpts))!.Data!;
        Assert.Equal(103L, reassignData.NewUnitId);
        Assert.Equal("occupied", reassignData.UnitStatus);

        var mgrEmergency = await client.PostAsJsonAsync($"/api/v1/manager/agreements/{confirmData.AgreementId}/emergency-cancel-refund",
            new ManagerEmergencyCancelRefundRequest { Reason = "Customer requested emergency cancellation after facility leak" });
        mgrEmergency.EnsureSuccessStatusCode();
        var cancelData = (await mgrEmergency.Content.ReadFromJsonAsync<ApiResponse<ManagerEmergencyCancelRefundResponseDto>>(JsonOpts))!.Data!;
        Assert.True(cancelData.TotalRefundAmount > 0);
        Assert.Equal("cancelled", cancelData.AgreementStatus);
    }
}
