using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SelfStorageSystem.Contracts.Auth;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Payments;
using SelfStorageSystem.Contracts.Customer.Rentals;
using SelfStorageSystem.Contracts.Customer.Reservations;
using SelfStorageSystem.Contracts.Staff;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Infrastructure.Persistence;
using Xunit;

namespace SelfStorageSystem.Tests;

public class Cum1E2ETests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public Cum1E2ETests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Cum1_FullFlow_RegisterProfile_ReservePay_CheckInSwapCccd_Pin_AutoTransfer_MoveOutNegativeDebt()
    {
        using var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var email = $"cum1.{suffix}@test.local";
        var cccd = $"079200{Random.Shared.Next(100000, 999999)}";
        var phone = $"090{Random.Shared.Next(1000000, 9999999)}";

        // 1. Register with FullName, PhoneNumber, IdentityNumber (CCCD)
        var regResp = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
        {
            FullName = "Nguyen Van Cum1",
            Email = email,
            Password = "Password@123",
            PhoneNumber = phone,
            IdentityNumber = cccd
        });
        Assert.Equal(HttpStatusCode.OK, regResp.StatusCode);

        // Activate user directly in DB for test speed (bypassing SMTP) & grab seeded staff/facility
        long customerId;
        long facilityId;
        long unitTypeId;
        long secondUnitTypeId;
        long staffUserId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            user.Status = UserStatusConstants.Active;
            await db.SaveChangesAsync();
            customerId = user.Id;

            var availableUnit = await db.StorageUnits
                .FirstAsync(u => u.PhysicalStatus == StorageUnitStatusConstants.Available && u.IsListed);
            facilityId = availableUnit.FacilityId;
            unitTypeId = availableUnit.UnitTypeId;
            secondUnitTypeId = unitTypeId;

            var staffRole = await db.Roles.FirstAsync(r => r.Code == RoleConstants.Staff);
            var staffUr = await db.UserRoles.FirstAsync(ur => ur.RoleId == staffRole.Id);
            staffUserId = staffUr.UserId;
        }

        // Login as newly registered customer
        var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password@123"
        });
        loginResp.EnsureSuccessStatusCode();
        var authData = (await loginResp.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(JsonOpts))!.Data!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authData.AccessToken);

        // 2. Profile: CCCD locked (422) + max 2 edits/month (3rd edit -> 409)
        var lockedCccdResp = await client.PutAsJsonAsync("/api/v1/profile", new UpdateCustomerProfileRequest
        {
            FullName = "Nguyen Van Cum1",
            PhoneNumber = phone,
            IdentityNumber = "000000000000"
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, lockedCccdResp.StatusCode);

        var edit1 = await client.PutAsJsonAsync("/api/v1/profile", new UpdateCustomerProfileRequest
        {
            FullName = "Nguyen Van Edit1",
            PhoneNumber = phone
        });
        edit1.EnsureSuccessStatusCode();

        var edit2 = await client.PutAsJsonAsync("/api/v1/profile", new UpdateCustomerProfileRequest
        {
            FullName = "Nguyen Van Edit2",
            PhoneNumber = phone
        });
        edit2.EnsureSuccessStatusCode();

        var edit3 = await client.PutAsJsonAsync("/api/v1/profile", new UpdateCustomerProfileRequest
        {
            FullName = "Nguyen Van Edit3",
            PhoneNumber = phone
        });
        Assert.Equal(HttpStatusCode.Conflict, edit3.StatusCode);

        // 3. Flow 1: Reserve unit + SePay webhook
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var rsvResp = await client.PostAsJsonAsync("/api/v1/reservations", new CreateReservationRequest
        {
            FacilityId = facilityId,
            UnitTypeId = unitTypeId,
            StartDate = startDate,
            DurationMonths = 1
        });
        var rsvBody = await rsvResp.Content.ReadAsStringAsync();
        Assert.True(rsvResp.IsSuccessStatusCode, $"Reserve failed ({rsvResp.StatusCode}): {rsvBody}");
        var rsvJson = (await rsvResp.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(JsonOpts))!.Data;
        var rsvId = rsvJson.GetProperty("reservationId").GetInt64();
        var invoiceId = rsvJson.GetProperty("invoiceId").GetInt64();
        var firstPayAmount = rsvJson.GetProperty("firstPaymentAmount").GetDecimal();

        var webhookResp = await client.PostAsJsonAsync("/api/v1/webhooks/sepay", new SePayWebhookPayload
        {
            Id = Random.Shared.Next(100000, 999999),
            Gateway = "MBBank",
            TransactionDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            AccountNumber = "0123456789",
            TransferType = "in",
            TransferAmount = firstPayAmount,
            Content = $"DH{invoiceId}",
            ReferenceCode = $"FT{suffix}"
        });
        webhookResp.EnsureSuccessStatusCode();

        // Get Check-in QR
        var rsvDetailResp = await client.GetAsync($"/api/v1/reservations/{rsvId}");
        rsvDetailResp.EnsureSuccessStatusCode();
        var rsvDetailJson = (await rsvDetailResp.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(JsonOpts))!.Data;
        var checkInQrToken = rsvDetailJson.GetProperty("checkInQrToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(checkInQrToken));

        // 4. Flow 2: Staff login -> verify-qr -> swap backup unit -> confirm check-in (CCCD mismatch -> 422, match -> 200)
        string staffToken;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
            var staffUser = await db.Users.FirstAsync(u => u.Id == staffUserId);
            var jwt = scope.ServiceProvider.GetRequiredService<SelfStorageSystem.Application.Interfaces.IJwtTokenService>();
            staffToken = jwt.GenerateToken(staffUser, new List<string> { RoleConstants.Staff }, "Staff Test");
        }
        using var staffClient = _factory.CreateClient();
        staffClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        var verifyResp = await staffClient.PostAsJsonAsync("/api/v1/staff/check-ins/verify-qr",
            new VerifyCheckInQrRequest(checkInQrToken!, facilityId, cccd));
        verifyResp.EnsureSuccessStatusCode();
        var verifyData = (await verifyResp.Content.ReadFromJsonAsync<ApiResponse<VerifyCheckInQrResponse>>(JsonOpts))!.Data!;
        Assert.True(verifyData.IdentityVerified);
        Assert.NotNull(verifyData.AvailableBackupUnits);

        if (verifyData.AvailableBackupUnits!.Count > 0)
        {
            var backupUnitId = verifyData.AvailableBackupUnits[0].UnitId;
            var swapResp = await staffClient.PostAsJsonAsync($"/api/v1/staff/reservations/{rsvId}/assign-unit",
                new AssignReservationUnitRequest(backupUnitId, "E2E backup swap"));
            swapResp.EnsureSuccessStatusCode();
        }

        // Wrong CCCD -> 422
        var badCheckIn = await staffClient.PostAsJsonAsync("/api/v1/staff/check-ins/confirm", new ConfirmCheckInRequest
        {
            ReservationId = rsvId,
            IdentityVerification = new IdentityVerificationInput { Method = "government_id", DocumentNumber = "999999999999", FullNameMatched = true },
            Inspection = new CheckInInspectionInput { OverallCondition = "good", Items = [new CheckInInspectionItemInput { ItemName = "Door", Condition = "good" }] }
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badCheckIn.StatusCode);

        // Matching CCCD -> 200
        var goodCheckIn = await staffClient.PostAsJsonAsync("/api/v1/staff/check-ins/confirm", new ConfirmCheckInRequest
        {
            ReservationId = rsvId,
            IdentityVerification = new IdentityVerificationInput { Method = "government_id", DocumentNumber = cccd, FullNameMatched = true },
            Inspection = new CheckInInspectionInput { OverallCondition = "good", Items = [new CheckInInspectionItemInput { ItemName = "Door", Condition = "good" }] }
        });
        goodCheckIn.EnsureSuccessStatusCode();
        var checkedIn = (await goodCheckIn.Content.ReadFromJsonAsync<ApiResponse<ConfirmCheckInResponse>>(JsonOpts))!.Data!;

        // 5. Flow 3: Access Control PIN view & change
        var credResp = await client.GetAsync($"/api/v1/rentals/{checkedIn.AgreementId}/access-credentials");
        credResp.EnsureSuccessStatusCode();
        var cred = (await credResp.Content.ReadFromJsonAsync<ApiResponse<AccessCredentialDto>>(JsonOpts))!.Data!;
        Assert.False(string.IsNullOrWhiteSpace(cred.KeypadPin));

        var changePinResp = await client.PutAsJsonAsync($"/api/v1/rentals/{checkedIn.AgreementId}/pin",
            new ChangePinRequest { CurrentPin = cred.KeypadPin!, NewPin = "482910" });
        changePinResp.EnsureSuccessStatusCode();
        var pinChanged = (await changePinResp.Content.ReadFromJsonAsync<ApiResponse<ChangePinResponseDto>>(JsonOpts))!.Data!;
        Assert.Equal("synced", pinChanged.SyncStatus);

        // 6. Flow 3: Early Move-out with bank info -> Staff inspect with damage > deposit -> negative deposit debt alert
        var moveOutResp = await client.PostAsJsonAsync($"/api/v1/rentals/{checkedIn.AgreementId}/move-out",
            new RequestMoveOutRequest(DateOnly.FromDateTime(DateTime.UtcNow), "E2E early move-out", "MB Bank", "0901234567", "NGUYEN VAN CUM1"));
        moveOutResp.EnsureSuccessStatusCode();
        var moveOut = (await moveOutResp.Content.ReadFromJsonAsync<ApiResponse<MoveOutResponseDto>>(JsonOpts))!.Data!;

        var inspectResp = await staffClient.PostAsJsonAsync($"/api/v1/staff/move-outs/{moveOut.Id}/inspect", new InspectMoveOutRequest
        {
            OverallCondition = "damaged",
            Summary = "Damaged door",
            DamageItems = [new StaffInspectionDamageItemDto { ItemName = "Door", Condition = "damaged", ChargeAmount = 50_000_000m }]
        });
        inspectResp.EnsureSuccessStatusCode();

        var debtResp = await client.GetAsync("/api/v1/rentals/my-debt-alerts");
        debtResp.EnsureSuccessStatusCode();
        var debtJson = await debtResp.Content.ReadAsStringAsync();
        Assert.Contains("\"hasNegativeDepositDebt\":true", debtJson);
    }
}
