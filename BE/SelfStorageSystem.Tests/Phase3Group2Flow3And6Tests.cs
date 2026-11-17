using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Auth;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer;
using SelfStorageSystem.Contracts.Customer.Payments;
using SelfStorageSystem.Contracts.Customer.Rentals;
using SelfStorageSystem.Contracts.Customer.Reservations;
using SelfStorageSystem.Contracts.Manager;
using SelfStorageSystem.Contracts.Staff;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Infrastructure.Common;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;

namespace SelfStorageSystem.Tests;

public class Phase3Group2Flow3And6HttpTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Email = email, Password = "Pass@123" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(JsonOpts);
        return body!.Data!.AccessToken;
    }

    private static async Task<(long AgreementId, long UnitId, string IssuedPin)> CreateActiveCheckedInAgreementAsync(
        Phase3Group1WebFactory factory,
        long customerId,
        string customerToken,
        string staffToken,
        long storageUnitId,
        DateOnly startDate,
        int durationMonths)
    {
        var custClient = factory.CreateClient();
        custClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);

        var rsvResp = await custClient.PostAsJsonAsync("/api/v1/reservations", new CreateReservationRequest
        {
            FacilityId = 1,
            UnitTypeId = 1,
            StorageUnitId = storageUnitId,
            StartDate = startDate,
            DurationMonths = durationMonths,
            IdempotencyKey = $"flow36-{Guid.NewGuid():N}"
        });
        Assert.Equal(HttpStatusCode.Created, rsvResp.StatusCode);
        var rsv = (await rsvResp.Content.ReadFromJsonAsync<ApiResponse<CreateReservationResponse>>(JsonOpts))!.Data!;

        var checkoutResp = await custClient.PostAsJsonAsync("/api/v1/payments/checkout", new CreateCheckoutRequest
        {
            ReservationId = rsv.ReservationId
        });
        checkoutResp.EnsureSuccessStatusCode();
        var checkout = (await checkoutResp.Content.ReadFromJsonAsync<ApiResponse<CheckoutResponse>>(JsonOpts))!.Data!;

        var hookClient = factory.CreateClient();
        hookClient.DefaultRequestHeaders.Add("X-Api-Key", factory.SePayApiKey);
        var hookResp = await hookClient.PostAsJsonAsync("/api/v1/webhooks/sepay", new SePayWebhookPayload
        {
            Id = Random.Shared.NextInt64(100000, 9999999),
            Gateway = "MBBank",
            TransferType = "in",
            TransferAmount = checkout.Amount,
            Content = checkout.VietQr!.TransferContent,
            ReferenceCode = $"FT-SETUP-{Guid.NewGuid():N}"
        });
        hookResp.EnsureSuccessStatusCode();

        var staffClient = factory.CreateClient();
        staffClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);
        var checkInResp = await staffClient.PostAsJsonAsync("/api/v1/staff/check-ins/confirm", new ConfirmCheckInRequest
        {
            ReservationId = rsv.ReservationId,
            IdentityVerification = new IdentityVerificationInput
            {
                Method = "government_id",
                DocumentNumber = $"07920000{customerId:D4}",
                FullNameMatched = true,
                Notes = "Verified CCCD"
            },
            Inspection = new CheckInInspectionInput
            {
                OverallCondition = "good",
                Summary = "Clean",
                Items = [new CheckInInspectionItemInput { ItemName = "Door Lock", Condition = "good" }]
            },
            CustomerSignatureRef = "sig-c",
            StaffSignatureRef = "sig-s",
            Notes = "Check-in OK"
        });
        checkInResp.EnsureSuccessStatusCode();
        var checkedIn = (await checkInResp.Content.ReadFromJsonAsync<ApiResponse<ConfirmCheckInResponse>>(JsonOpts))!.Data!;
        return (checkedIn.AgreementId, checkedIn.UnitId, checkedIn.IssuedPin);
    }

    [Fact]
    public async Task Flow3_FullCustomerAndStaffWorkflow_WorksAcrossAllV1Endpoints()
    {
        await using var factory = new Phase3Group1WebFactory();
        await factory.SeedBaselineAsync();

        var now = DateTimeOffset.UtcNow;
        using (var seedScope = factory.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
            var pwHash = BCrypt.Net.BCrypt.HashPassword("Pass@123");

            // Seed second customer, facility 2 staff, and facility access point
            db.Users.AddRange(
                new User { Id = 11, Email = "cust2@test.local", PhoneNumber = "0901000011", PasswordHash = pwHash, Status = UserStatusConstants.Active, CreatedAt = now, UpdatedAt = now },
                new User { Id = 21, Email = "staff2@test.local", PhoneNumber = "0902000021", PasswordHash = pwHash, Status = UserStatusConstants.Active, CreatedAt = now, UpdatedAt = now });
            db.CustomerProfiles.Add(new CustomerProfile { UserId = 11, FullName = "Other Customer", CreatedAt = now, UpdatedAt = now });
            db.EmployeeProfiles.Add(new EmployeeProfile { UserId = 21, EmployeeCode = "STF-02", FullName = "Staff Fac2", EmploymentStatus = "active", CreatedAt = now, UpdatedAt = now });
            db.UserRoles.AddRange(
                new UserRole { UserId = 11, RoleId = 1, GrantedAt = now },
                new UserRole { UserId = 21, RoleId = 2, GrantedAt = now });
            db.StaffFacilityAssignments.Add(new StaffFacilityAssignment
            {
                Id = 10, EmployeeId = 21, FacilityId = 2, AssignmentRole = StaffAssignmentRoles.FacilityStaff, StartsAt = now.AddDays(-5)
            });
            db.AccessPoints.Add(new AccessPoint
            {
                Id = 1, FacilityId = 1, Code = "AP-FAC1-DOOR", Name = "Zone A Controller", AccessPointType = "unit_controller", Status = "active"
            });
            var fac1ToOpen = await db.Facilities.FindAsync(1L);
            if (fac1ToOpen != null)
            {
                fac1ToOpen.OpeningTime = null;
                fac1ToOpen.ClosingTime = null;
            }
            await db.SaveChangesAsync();
        }

        var authClient = factory.CreateClient();
        var cust1Token = await LoginAsync(authClient, "cust1@test.local");
        var cust2Token = await LoginAsync(authClient, "cust2@test.local");
        var staff1Token = await LoginAsync(authClient, "staff1@test.local");
        var staff2Token = await LoginAsync(authClient, "staff2@test.local");

        var today = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(7)).DateTime);
        var (agrId, _, issuedPin) = await CreateActiveCheckedInAgreementAsync(
            factory, 10, cust1Token, staff1Token, storageUnitId: 101, startDate: today, durationMonths: 2);

        var cust1 = factory.CreateClient();
        cust1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cust1Token);
        var cust2 = factory.CreateClient();
        cust2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cust2Token);

        // 1. Rental list & detail + ownership isolation (403 vs 404)
        var myRentals = (await (await cust1.GetAsync("/api/v1/rentals/my")).Content.ReadFromJsonAsync<ApiResponse<List<RentalSummaryDto>>>(JsonOpts))!.Data!;
        Assert.Single(myRentals);
        Assert.Equal(agrId, myRentals[0].AgreementId);

        var detailResp = await cust1.GetAsync($"/api/v1/rentals/{agrId}");
        Assert.Equal(HttpStatusCode.OK, detailResp.StatusCode);
        var detail = (await detailResp.Content.ReadFromJsonAsync<ApiResponse<RentalDetailDto>>(JsonOpts))!.Data!;
        Assert.Equal("A-101", detail.Unit.UnitCode);
        Assert.NotNull(detail.Handover);

        Assert.Equal(HttpStatusCode.Forbidden, (await cust2.GetAsync($"/api/v1/rentals/{agrId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cust1.GetAsync("/api/v1/rentals/999999")).StatusCode);

        // 2. Credentials & v1. gate QR token
        var creds = (await (await cust1.GetAsync($"/api/v1/rentals/{agrId}/access-credentials")).Content
            .ReadFromJsonAsync<ApiResponse<AccessCredentialDto>>(JsonOpts))!.Data!;
        Assert.Equal(issuedPin, creds.KeypadPin);
        Assert.StartsWith("v1.", creds.GateQrToken);
        var qrVerifier = factory.Services.GetRequiredService<IQrTokenService>();
        Assert.True(qrVerifier.TryVerifyToken(creds.GateQrToken, "gate", out var gatePayload));
        Assert.Equal(agrId, gatePayload!.AgrId);

        // 3. Authorized members CRUD + linked AccessCredential sync
        var addMemberResp = await cust1.PostAsJsonAsync($"/api/v1/rentals/{agrId}/authorized-members", new
        {
            fullName = "Tran Van Uy Quyen",
            identityFingerprint = "079200001234",
            relationshipToCustomer = "Partner",
            issuePin = true
        });
        Assert.Equal(HttpStatusCode.Created, addMemberResp.StatusCode);
        var member = (await addMemberResp.Content.ReadFromJsonAsync<ApiResponse<AuthorizedMemberDto>>(JsonOpts))!.Data!;
        Assert.NotNull(member.IssuedMemberPin);
        Assert.Matches(@"^\d{6}$", member.IssuedMemberPin!);

        var revokeMemberResp = await cust1.DeleteAsync($"/api/v1/rentals/{agrId}/authorized-members/{member.Id}");
        Assert.Equal(HttpStatusCode.OK, revokeMemberResp.StatusCode);

        using (var verifyScope = factory.Services.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
            var linkedCred = await db.AccessCredentials.SingleAsync(c => c.AuthorizedMemberId == member.Id);
            Assert.Equal(CredentialStatusConstants.Revoked, linkedCred.Status);
            Assert.NotNull(linkedCred.RevokedAt);
        }

        // 4. PIN validation (weak PIN, invalid current PIN, valid change) & Unlock writing AccessEvent
        var weakResp = await cust1.PutAsJsonAsync($"/api/v1/rentals/{agrId}/pin", new ChangePinRequest { CurrentPin = issuedPin, NewPin = "123456" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, weakResp.StatusCode);

        var wrongCurResp = await cust1.PutAsJsonAsync($"/api/v1/rentals/{agrId}/pin", new ChangePinRequest { CurrentPin = "999999", NewPin = "258046" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongCurResp.StatusCode);

        var okPinResp = await cust1.PutAsJsonAsync($"/api/v1/rentals/{agrId}/pin", new ChangePinRequest { CurrentPin = issuedPin, NewPin = "258046" });
        Assert.Equal(HttpStatusCode.OK, okPinResp.StatusCode);

        var badUnlock = await cust1.PostAsJsonAsync($"/api/v1/rentals/{agrId}/unlock", new UnlockStorageUnitRequest { Pin = "111222" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badUnlock.StatusCode);

        var goodUnlock = await cust1.PostAsJsonAsync($"/api/v1/rentals/{agrId}/unlock", new UnlockStorageUnitRequest { Pin = "258046" });
        Assert.Equal(HttpStatusCode.OK, goodUnlock.StatusCode);
        var unlockDto = (await goodUnlock.Content.ReadFromJsonAsync<ApiResponse<UnlockStorageUnitResponseDto>>(JsonOpts))!.Data!;
        Assert.True(unlockDto.Unlocked);
        Assert.True(unlockDto.AccessEventId > 0);

        var logs = (await (await cust1.GetAsync($"/api/v1/rentals/{agrId}/access-logs?page=1&pageSize=10")).Content
            .ReadFromJsonAsync<ApiResponse<PagedResult<AccessEventDto>>>(JsonOpts))!.Data!;
        Assert.True(logs.TotalCount >= 2);
        Assert.Contains(logs.Items, i => i.Result == "granted");
        Assert.Contains(logs.Items, i => i.Result == "denied");

        // 5. Staff access events monitoring + strict facility isolation
        var staff1 = factory.CreateClient();
        staff1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staff1Token);
        var staff2 = factory.CreateClient();
        staff2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staff2Token);

        Assert.Equal(HttpStatusCode.Forbidden, (await staff2.GetAsync("/api/v1/staff/access-events?facilityId=1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff1.GetAsync("/api/v1/staff/access-events?facilityId=2")).StatusCode);

        var staffLogResp = await staff1.PostAsJsonAsync("/api/v1/staff/access-events", new CreateStaffAccessEventRequest(
            AccessPointId: 1, CredentialId: null, AgreementId: agrId, Result: "granted", Reason: "Staff manual gate open"));
        Assert.Equal(HttpStatusCode.Created, staffLogResp.StatusCode);

        // 6. Stored items (prohibited rejected 422, valid CRUD 201/200)
        var prohibResp = await cust1.PostAsJsonAsync($"/api/v1/rentals/{agrId}/items", new DeclareStoredItemsRequest([
            new CreateStoredItemDto("Gasoline canister", "other", "Flammable fuel", 1, 100_000m)
        ]));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, prohibResp.StatusCode);

        var addItemResp = await cust1.PostAsJsonAsync($"/api/v1/rentals/{agrId}/items", new DeclareStoredItemsRequest([
            new CreateStoredItemDto("Office Boxes", "documents", "Tax archives", 4, 2_000_000m)
        ]));
        Assert.Equal(HttpStatusCode.Created, addItemResp.StatusCode);

        // 7. Early move-out refund preview + request + Staff inspect + Staff counter cash payout
        var earlyDate = today.AddDays(10);
        var preview = (await (await cust1.GetAsync($"/api/v1/rentals/{agrId}/move-out/refund-preview?requestedMoveOutDate={earlyDate:yyyy-MM-dd}")).Content
            .ReadFromJsonAsync<ApiResponse<RefundPreviewDto>>(JsonOpts))!.Data!;
        Assert.True(preview.OriginalDeposit > 0);
        Assert.True(preview.EstimatedUnusedRentRefund > 0);

        var moveOutResp = await cust1.PostAsJsonAsync($"/api/v1/rentals/{agrId}/move-out", new RequestMoveOutRequest(earlyDate, "Moving office"));
        Assert.Equal(HttpStatusCode.Created, moveOutResp.StatusCode);
        var moveOut = (await moveOutResp.Content.ReadFromJsonAsync<ApiResponse<MoveOutResponseDto>>(JsonOpts))!.Data!;
        Assert.StartsWith("v1.", moveOut.MoveOutQrToken);
        Assert.True(qrVerifier.TryVerifyToken(moveOut.MoveOutQrToken, "move_out", out _));

        var inspectResp = await staff1.PostAsJsonAsync($"/api/v1/staff/move-outs/{moveOut.Id}/inspect", new InspectMoveOutRequest(
            OverallCondition: "good",
            Summary: "Clean unit",
            DamageItems:
            [
                new StaffInspectionDamageItemDto { ItemName = "Cleaning fee", Condition = "acceptable", ChargeAmount = 100_000m }
            ]
        ));
        Assert.Equal(HttpStatusCode.OK, inspectResp.StatusCode);
        var inspected = (await inspectResp.Content.ReadFromJsonAsync<ApiResponse<InspectMoveOutResultDto>>(JsonOpts))!.Data!;
        Assert.Equal(100_000m, inspected.TotalDamageCharges);
        Assert.Equal(900_000m, inspected.RefundAmount);
        Assert.NotNull(inspected.RefundId);
        Assert.Equal(StorageUnitStatusConstants.Cleaning, inspected.UnitStatus);

        var payoutResp = await staff1.PostAsJsonAsync($"/api/v1/staff/refunds/{inspected.RefundId}/counter-cash-payout", new
        {
            receiptReference = "RCPT-CASH-001",
            notes = "Paid cash at counter"
        });
        Assert.Equal(HttpStatusCode.OK, payoutResp.StatusCode);
        var payout = (await payoutResp.Content.ReadFromJsonAsync<ApiResponse<StaffCounterCashPayoutResultDto>>(JsonOpts))!.Data!;
        Assert.Equal("completed", payout.Status);
        Assert.Equal("RCPT-CASH-001", payout.ReceiptReference);
    }

    [Fact]
    public async Task Flow6_LifecycleWorker_LateFeeIdempotency_OverdueLock_RenewalWebhookUnlock_AndLiquidation()
    {
        await using var factory = new Phase3Group1WebFactory();
        await factory.SeedBaselineAsync();

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(7)).DateTime);
        long agrT7Id = 701, agrOvdId = 702, agrDefId = 703;
        long invOvdId = 7020;

        using (var seedScope = factory.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
            var pwHash = BCrypt.Net.BCrypt.HashPassword("Pass@123");

            db.Users.Add(new User { Id = 31, Email = "mgr2@test.local", PhoneNumber = "0903000031", PasswordHash = pwHash, Status = UserStatusConstants.Active, CreatedAt = now, UpdatedAt = now });
            db.EmployeeProfiles.Add(new EmployeeProfile { UserId = 31, EmployeeCode = "MGR-02", FullName = "Mgr Fac2", EmploymentStatus = "active", CreatedAt = now, UpdatedAt = now });
            db.UserRoles.Add(new UserRole { UserId = 31, RoleId = 3, GrantedAt = now });
            db.StaffFacilityAssignments.Add(new StaffFacilityAssignment
            {
                Id = 11, EmployeeId = 31, FacilityId = 2, AssignmentRole = StaffAssignmentRoles.FacilityManager, StartsAt = now.AddDays(-5)
            });

            db.FeeRules.Add(new FeeRule
            {
                Id = 10,
                FacilityId = 1,
                Code = "LATE_50K_DAILY",
                FeeType = "late_fee",
                CalculationMethod = "per_day",
                Amount = 50_000m,
                GraceDays = 0,
                Conditions = "{}",
                IsActive = true,
                ValidFrom = today.AddDays(-60),
                CreatedAt = now
            });
            db.PolicyVersions.Add(new PolicyVersion
            {
                Id = 10,
                PolicyType = "overdue",
                Version = "2026.1",
                Content = "{\"grace_days\":0,\"suspend_pin_from_day\":1,\"default_after_days\":30}",
                ValidFrom = today.AddDays(-60),
                CreatedAt = now
            });
            db.AccessPoints.Add(new AccessPoint
            {
                Id = 1, FacilityId = 1, Code = "AP-1", Name = "Unit Door 1", AccessPointType = "unit_controller", Status = "active"
            });

            // Agreement A: T-7 expiring soon (EndDate = today + 5)
            var u1 = await db.StorageUnits.FindAsync(101L);
            var u2 = await db.StorageUnits.FindAsync(102L);
            var u3 = await db.StorageUnits.FindAsync(103L);
            u1!.PhysicalStatus = StorageUnitStatusConstants.Occupied;
            u2!.PhysicalStatus = StorageUnitStatusConstants.Occupied;
            u3!.PhysicalStatus = StorageUnitStatusConstants.Occupied;

            db.RentalAgreements.AddRange(
                new RentalAgreement
                {
                    Id = agrT7Id, AgreementNo = "AGR-T7-701", CustomerId = 10, FacilityId = 1, ReservationId = 0, PolicyVersionId = 1,
                    StartDate = today.AddDays(-25), EndDate = today.AddDays(5), MonthlyRateSnapshot = 1_000_000m,
                    DepositSnapshot = 1_000_000m, DepositBalance = 1_000_000m, Status = RentalAgreementStatusConstants.Active,
                    CheckedInAt = now.AddDays(-25), CreatedAt = now.AddDays(-25), UpdatedAt = now.AddDays(-25)
                },
                new RentalAgreement
                {
                    Id = agrOvdId, AgreementNo = "AGR-OVD-702", CustomerId = 10, FacilityId = 1, ReservationId = 0, PolicyVersionId = 1,
                    StartDate = today.AddDays(-35), EndDate = today.AddDays(-2), MonthlyRateSnapshot = 1_000_000m,
                    DepositSnapshot = 1_000_000m, DepositBalance = 1_000_000m, Status = RentalAgreementStatusConstants.Active,
                    CheckedInAt = now.AddDays(-35), CreatedAt = now.AddDays(-35), UpdatedAt = now.AddDays(-35)
                },
                new RentalAgreement
                {
                    Id = agrDefId, AgreementNo = "AGR-DEF-703", CustomerId = 10, FacilityId = 1, ReservationId = 0, PolicyVersionId = 1,
                    StartDate = today.AddDays(-65), EndDate = today.AddDays(-35), MonthlyRateSnapshot = 1_000_000m,
                    DepositSnapshot = 1_000_000m, DepositBalance = 1_000_000m, Status = RentalAgreementStatusConstants.Active,
                    CheckedInAt = now.AddDays(-65), CreatedAt = now.AddDays(-65), UpdatedAt = now.AddDays(-65)
                });

            db.UnitAllocations.AddRange(
                new UnitAllocation { Id = 701, AgreementId = agrT7Id, StorageUnitId = 101, AllocationKind = "rental", AllocationStartDate = today.AddDays(-25), AllocationEndDate = today.AddDays(5), Status = AllocationStatusConstants.Active, CreatedAt = now },
                new UnitAllocation { Id = 702, AgreementId = agrOvdId, StorageUnitId = 102, AllocationKind = "rental", AllocationStartDate = today.AddDays(-35), AllocationEndDate = today.AddDays(-2), Status = AllocationStatusConstants.Active, CreatedAt = now },
                new UnitAllocation { Id = 703, AgreementId = agrDefId, StorageUnitId = 103, AllocationKind = "rental", AllocationStartDate = today.AddDays(-65), AllocationEndDate = today.AddDays(-35), Status = AllocationStatusConstants.Active, CreatedAt = now });

            db.AccessCredentials.AddRange(
                new AccessCredential
                {
                    Id = 7021, AgreementId = agrOvdId, CredentialType = CredentialTypeConstants.Pin,
                    SecretDigest = BCrypt.Net.BCrypt.HashPassword("246810"), DisplayHint = PinProtectionHelper.Encrypt("246810"),
                    Status = CredentialStatusConstants.Active, IssuedAt = now.AddDays(-35), CreatedAt = now.AddDays(-35)
                },
                new AccessCredential
                {
                    Id = 7031, AgreementId = agrDefId, CredentialType = CredentialTypeConstants.Pin,
                    SecretDigest = BCrypt.Net.BCrypt.HashPassword("369258"), DisplayHint = PinProtectionHelper.Encrypt("369258"),
                    Status = CredentialStatusConstants.Active, IssuedAt = now.AddDays(-65), CreatedAt = now.AddDays(-65)
                });

            db.Invoices.AddRange(
                new Invoice
                {
                    Id = invOvdId, InvoiceNo = "INV-OVD-7020", AgreementId = agrOvdId, CustomerId = 10, BillingPeriod = "2026-10",
                    IssueDate = today.AddDays(-10), DueDate = today.AddDays(-2), Currency = "VND",
                    SubtotalAmount = 1_000_000m, TotalAmount = 1_000_000m, PaidAmount = 0m,
                    Status = InvoiceStatusConstants.Open, CreatedAt = now.AddDays(-10), UpdatedAt = now.AddDays(-10)
                },
                new Invoice
                {
                    Id = 7030, InvoiceNo = "INV-DEF-7030", AgreementId = agrDefId, CustomerId = 10, BillingPeriod = "2026-09",
                    IssueDate = today.AddDays(-40), DueDate = today.AddDays(-35), Currency = "VND",
                    SubtotalAmount = 1_200_000m, TotalAmount = 1_200_000m, PaidAmount = 0m,
                    Status = InvoiceStatusConstants.Open, CreatedAt = now.AddDays(-40), UpdatedAt = now.AddDays(-40)
                });

            await db.SaveChangesAsync();
        }

        // 1. Run Lifecycle Worker TWICE on the same day -> check idempotency of T-7, D+1 overdue, PIN suspension, daily late_fee, >30d defaulted
        using (var runScope1 = factory.Services.CreateScope())
        {
            await runScope1.ServiceProvider.GetRequiredService<IStorageLifecycleService>().RunCycleAsync();
        }
        using (var runScope2 = factory.Services.CreateScope())
        {
            await runScope2.ServiceProvider.GetRequiredService<IStorageLifecycleService>().RunCycleAsync();
        }

        using (var checkScope = factory.Services.CreateScope())
        {
            var db = checkScope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();

            var agrT7 = await db.RentalAgreements.FindAsync(agrT7Id);
            Assert.Equal(RentalAgreementStatusConstants.ExpiringSoon, agrT7!.Status);
            Assert.Single(await db.Notifications.Where(n => n.UserId == 10 && n.TemplateCode == "RENTAL_EXPIRY_T7").ToListAsync());

            var agrOvd = await db.RentalAgreements.FindAsync(agrOvdId);
            Assert.Equal(RentalAgreementStatusConstants.Overdue, agrOvd!.Status);
            var ovdCred = await db.AccessCredentials.FindAsync(7021L);
            Assert.Equal(CredentialStatusConstants.Suspended, ovdCred!.Status);

            var invOvd = await db.Invoices.Include(i => i.InvoiceLines).FirstAsync(i => i.Id == invOvdId);
            var lateFeeLines = invOvd.InvoiceLines.Where(l => l.LineType == InvoiceLineTypeConstants.LateFee).ToList();
            Assert.Single(lateFeeLines); // Idempotent: 2 runs same day -> only 1 late_fee line!
            Assert.Equal(50_000m, lateFeeLines[0].LineAmount);
            Assert.Equal(1_050_000m, invOvd.TotalAmount);

            var agrDef = await db.RentalAgreements.FindAsync(agrDefId);
            Assert.Equal("defaulted", agrDef!.Status);
        }

        var authClient = factory.CreateClient();
        var custToken = await LoginAsync(authClient, "cust1@test.local");
        var staffToken = await LoginAsync(authClient, "staff1@test.local");
        var mgrToken = await LoginAsync(authClient, "mgr1@test.local");
        var mgr2Token = await LoginAsync(authClient, "mgr2@test.local");

        var custClient = factory.CreateClient();
        custClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", custToken);

        // 2. PIN change & unlock MUST be blocked (409) while overdue
        var blockedPinResp = await custClient.PutAsJsonAsync($"/api/v1/rentals/{agrOvdId}/pin", new ChangePinRequest { CurrentPin = "246810", NewPin = "975310" });
        Assert.Equal(HttpStatusCode.Conflict, blockedPinResp.StatusCode);

        var blockedUnlockResp = await custClient.PostAsJsonAsync($"/api/v1/rentals/{agrOvdId}/unlock", new UnlockStorageUnitRequest { Pin = "246810" });
        Assert.Equal(HttpStatusCode.Conflict, blockedUnlockResp.StatusCode);

        // 3. Customer renews overdue agreement with promo code SALE10 -> checkout -> SePay webhook (with replay!) -> unlocks PIN & cures case
        var renewResp = await custClient.PostAsJsonAsync($"/api/v1/rentals/{agrOvdId}/renew", new
        {
            durationMonths = 2,
            promotionCode = "SALE10"
        });
        Assert.Equal(HttpStatusCode.Created, renewResp.StatusCode);
        var renewal = (await renewResp.Content.ReadFromJsonAsync<ApiResponse<RenewAgreementResponseDto>>(JsonOpts))!.Data!;
        Assert.Equal(1_050_000m, renewal.OverdueLateFeeAmount);
        Assert.Equal(1_800_000m, renewal.TotalRenewalAmount); // 2 * 1M - 10% (200k max) = 1.8M
        Assert.Equal(2_850_000m, renewal.TotalDueAmount);     // 1.8M + 1.05M overdue + late fee
        Assert.NotNull(renewal.InvoiceId);

        var rnwCheckoutResp = await custClient.PostAsJsonAsync("/api/v1/payments/checkout", new CreateCheckoutRequest
        {
            RenewalId = renewal.RenewalId
        });
        rnwCheckoutResp.EnsureSuccessStatusCode();
        var rnwCheckout = (await rnwCheckoutResp.Content.ReadFromJsonAsync<ApiResponse<CheckoutResponse>>(JsonOpts))!.Data!;
        Assert.True(rnwCheckout.PaymentId > 0);
        Assert.Equal(2_850_000m, rnwCheckout.Amount);

        var hookClient = factory.CreateClient();
        hookClient.DefaultRequestHeaders.Add("X-Api-Key", factory.SePayApiKey);
        var webhookPayload = new SePayWebhookPayload
        {
            Id = 888001,
            Gateway = "MBBank",
            TransferType = "in",
            TransferAmount = rnwCheckout.Amount,
            Content = rnwCheckout.VietQr!.TransferContent,
            ReferenceCode = "FT-RNW-OVD-888001"
        };
        var hook1 = await hookClient.PostAsJsonAsync("/api/v1/webhooks/sepay", webhookPayload);
        hook1.EnsureSuccessStatusCode();
        var hook2 = await hookClient.PostAsJsonAsync("/api/v1/webhooks/sepay", webhookPayload);
        hook2.EnsureSuccessStatusCode();
        var hook2Body = await hook2.Content.ReadFromJsonAsync<ApiResponse<SePayWebhookResultDto>>(JsonOpts);
        Assert.Equal("duplicate_ignored", hook2Body!.Data!.Action);

        using (var postRnwScope = factory.Services.CreateScope())
        {
            var db = postRnwScope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
            var agrOvd = await db.RentalAgreements.FindAsync(agrOvdId);
            Assert.Equal(RentalAgreementStatusConstants.Active, agrOvd!.Status);
            Assert.Equal(today.AddDays(-2).AddMonths(2), agrOvd.EndDate);

            var cred = await db.AccessCredentials.FindAsync(7021L);
            Assert.Equal(CredentialStatusConstants.Active, cred!.Status);
            Assert.Null(cred.RevokedAt);

            var dCase = await db.DelinquencyCases.FirstAsync(c => c.AgreementId == agrOvdId);
            Assert.Equal(DelinquencyCaseStatusConstants.Cured, dCase.Status);
            Assert.NotNull(dCase.ResolvedAt);
        }

        // Now PIN change & unlock MUST succeed!
        var okPinAfter = await custClient.PutAsJsonAsync($"/api/v1/rentals/{agrOvdId}/pin", new ChangePinRequest { CurrentPin = "246810", NewPin = "975310" });
        Assert.Equal(HttpStatusCode.OK, okPinAfter.StatusCode);
        var okUnlockAfter = await custClient.PostAsJsonAsync($"/api/v1/rentals/{agrOvdId}/unlock", new UnlockStorageUnitRequest { Pin = "975310" });
        Assert.Equal(HttpStatusCode.OK, okUnlockAfter.StatusCode);

        // 4. Staff overdue monitoring + manual lock access, Manager delinquency action + liquidation + expiring inspection assignment
        var staffClient = factory.CreateClient();
        staffClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);
        var mgrClient = factory.CreateClient();
        mgrClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgrToken);
        var mgr2Client = factory.CreateClient();
        mgr2Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgr2Token);

        // Cross-facility Manager 403 check
        Assert.Equal(HttpStatusCode.Forbidden, (await mgr2Client.GetAsync("/api/v1/manager/facilities/1/delinquency-cases")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mgr2Client.PostAsJsonAsync($"/api/v1/manager/agreements/{agrDefId}/liquidate", new ManagerLiquidateAgreementRequest { Reason = "Cross fac" })).StatusCode);

        // Manager expiring cases & assign inspection
        var expResp = await mgrClient.GetAsync("/api/v1/manager/facilities/1/expiring-cases");
        Assert.Equal(HttpStatusCode.OK, expResp.StatusCode);
        var assignResp = await mgrClient.PostAsJsonAsync($"/api/v1/manager/agreements/{agrT7Id}/assign-inspection", new ManagerAssignInspectionRequest { EmployeeId = 20, Notes = "Inspect T-7" });
        Assert.Equal(HttpStatusCode.Created, assignResp.StatusCode);

        // Manager adds manual late fee action on defaulted agreement case, then liquidates it
        var casesResp = await mgrClient.GetAsync("/api/v1/manager/facilities/1/delinquency-cases");
        casesResp.EnsureSuccessStatusCode();
        var cases = (await casesResp.Content.ReadFromJsonAsync<ApiResponse<List<ManagerDelinquencyCaseDto>>>(JsonOpts))!.Data!;
        var defCase = cases.Single(c => c.AgreementId == agrDefId);

        var manualFeeResp = await mgrClient.PostAsJsonAsync($"/api/v1/manager/delinquency-cases/{defCase.Id}/actions", new ManagerAddDelinquencyActionRequest
        {
            ActionType = "late_fee_added",
            Note = "Manual admin fee",
            LateFeeAmount = 50_000m
        });
        Assert.Equal(HttpStatusCode.Created, manualFeeResp.StatusCode);

        var liqResp = await mgrClient.PostAsJsonAsync($"/api/v1/manager/agreements/{agrDefId}/liquidate", new ManagerLiquidateAgreementRequest
        {
            Reason = "Defaulted >30 days, auctioned contents",
            AuctionProceedsAmount = 400_000m,
            DeductDeposit = true
        });
        Assert.Equal(HttpStatusCode.OK, liqResp.StatusCode);
        var liq = (await liqResp.Content.ReadFromJsonAsync<ApiResponse<ManagerLiquidateAgreementResultDto>>(JsonOpts))!.Data!;
        Assert.Equal(RentalAgreementStatusConstants.Terminated, liq.AgreementStatus);
        Assert.Equal(StorageUnitStatusConstants.PendingInspection, liq.UnitStatus);
        Assert.Equal(1, liq.RevokedCredentialsCount);
        Assert.Equal(1_000_000m, liq.DeductedDepositAmount);
        Assert.Equal(HttpStatusCode.Conflict, (await mgrClient.PostAsJsonAsync($"/api/v1/manager/agreements/{agrDefId}/liquidate", new ManagerLiquidateAgreementRequest { Reason = "Twice" })).StatusCode);
    }
}

public class Phase3Group2SqlServerEndToEndTests : IClassFixture<SqlServerCleanInstallFixture>
{
    private readonly SqlServerCleanInstallFixture _fixture;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public Phase3Group2SqlServerEndToEndTests(SqlServerCleanInstallFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task LiveSqlServer_TriggersAndConstraints_PassFullFlow3AndFlow6Lifecycle()
    {
        await using var factory = new SqlServerWebFactory(_fixture.ConnectionString);

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
        var staffToken = await LoginAsync("staff.hcm@example.test");
        var mgrToken = await LoginAsync("manager.hcm@example.test");

        var cust = factory.CreateClient();
        cust.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cust1Token);
        var staff = factory.CreateClient();
        staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);
        var mgr = factory.CreateClient();
        mgr.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgrToken);

        var tz = TimeZoneHelper.ResolveTimeZone("Asia/Ho_Chi_Minh");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime);

        // 1. Flow 1 + Flow 2 on SQL Server to obtain a real checked-in active agreement
        var rsvResp = await cust.PostAsJsonAsync("/api/v1/reservations", new CreateReservationRequest
        {
            FacilityId = 1,
            UnitTypeId = 1,
            StorageUnitId = 1,
            StartDate = today,
            DurationMonths = 1,
            IdempotencyKey = $"sql-f36-{Guid.NewGuid():N}"
        });
        Assert.Equal(HttpStatusCode.Created, rsvResp.StatusCode);
        var rsv = (await rsvResp.Content.ReadFromJsonAsync<ApiResponse<CreateReservationResponse>>(JsonOpts))!.Data!;

        var chk = (await (await cust.PostAsJsonAsync("/api/v1/payments/checkout", new CreateCheckoutRequest { ReservationId = rsv.ReservationId }))
            .Content.ReadFromJsonAsync<ApiResponse<CheckoutResponse>>(JsonOpts))!.Data!;

        var hook = factory.CreateClient();
        hook.DefaultRequestHeaders.Add("X-Api-Key", factory.SePayApiKey);
        var hookRes = await hook.PostAsJsonAsync("/api/v1/webhooks/sepay", new SePayWebhookPayload
        {
            Id = Random.Shared.NextInt64(100000, 9999999),
            Gateway = "MBBank",
            TransferType = "in",
            TransferAmount = chk.Amount,
            Content = chk.VietQr!.TransferContent,
            ReferenceCode = $"FT-SQL-F36-{Guid.NewGuid():N}"
        });
        hookRes.EnsureSuccessStatusCode();

        var ciResp = await staff.PostAsJsonAsync("/api/v1/staff/check-ins/confirm", new ConfirmCheckInRequest
        {
            ReservationId = rsv.ReservationId,
            IdentityVerification = new IdentityVerificationInput
            {
                Method = "government_id",
                DocumentNumber = "079200008999",
                FullNameMatched = true,
                Notes = "CCCD verified"
            },
            Inspection = new CheckInInspectionInput
            {
                OverallCondition = "good",
                Summary = "Unit ready",
                Items = [new CheckInInspectionItemInput { ItemName = "Lock", Condition = "good" }]
            },
            CustomerSignatureRef = "sig-cust",
            StaffSignatureRef = "sig-staff",
            Notes = "Check-in OK"
        });
        ciResp.EnsureSuccessStatusCode();
        var checkedIn = (await ciResp.Content.ReadFromJsonAsync<ApiResponse<ConfirmCheckInResponse>>(JsonOpts))!.Data!;
        var agrId = checkedIn.AgreementId;

        // 2. Flow 3 on live SQL Server: Authorized member + PIN sync + revoke + unlock writing AccessEvent + Staff access-events
        var addMem = await cust.PostAsJsonAsync($"/api/v1/rentals/{agrId}/authorized-members", new
        {
            fullName = "Nguyen Van SQL Member",
            identityFingerprint = "079200005555",
            relationshipToCustomer = "Family",
            issuePin = true
        });
        Assert.Equal(HttpStatusCode.Created, addMem.StatusCode);
        var mem = (await addMem.Content.ReadFromJsonAsync<ApiResponse<AuthorizedMemberDto>>(JsonOpts))!.Data!;
        Assert.NotNull(mem.IssuedMemberPin);
        Assert.Equal(HttpStatusCode.OK, (await cust.DeleteAsync($"/api/v1/rentals/{agrId}/authorized-members/{mem.Id}")).StatusCode);

        var unlockResp = await cust.PostAsJsonAsync($"/api/v1/rentals/{agrId}/unlock", new UnlockStorageUnitRequest { Pin = checkedIn.IssuedPin });
        if (unlockResp.StatusCode == HttpStatusCode.Conflict)
        {
            // If facility operating hours prevent unlock at late night UTC+7 test execution, open operating hours temporarily for SQL unlock verification
            using var openScope = factory.Services.CreateScope();
            var dbOpen = openScope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
            await dbOpen.Facilities.Where(f => f.Id == 1).ExecuteUpdateAsync(s => s
                .SetProperty(f => f.OpeningTime, (TimeOnly?)null)
                .SetProperty(f => f.ClosingTime, (TimeOnly?)null));
            unlockResp = await cust.PostAsJsonAsync($"/api/v1/rentals/{agrId}/unlock", new UnlockStorageUnitRequest { Pin = checkedIn.IssuedPin });
        }
        Assert.Equal(HttpStatusCode.OK, unlockResp.StatusCode);

        var staffEventsResp = await staff.GetAsync("/api/v1/staff/access-events?facilityId=1");
        Assert.Equal(HttpStatusCode.OK, staffEventsResp.StatusCode);

        // 3. Flow 6 on live SQL Server: make agreement overdue with past-due invoice -> run StorageLifecycleService twice -> verify trg_invoice_lines_guard & trg_invoice_lines_refresh_totals!
        long ovdInvId;
        using (var ovdScope = factory.Services.CreateScope())
        {
            var db = ovdScope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
            if (!await db.FeeRules.AnyAsync(f => f.FacilityId == 1 && f.FeeType == "late_fee" && f.IsActive))
            {
                db.FeeRules.Add(new FeeRule
                {
                    FacilityId = 1,
                    Code = "SQL-LATE-50K",
                    FeeType = "late_fee",
                    CalculationMethod = "per_day",
                    Amount = 50_000m,
                    GraceDays = 0,
                    Conditions = "{}",
                    IsActive = true,
                    ValidFrom = today.AddDays(-30),
                    CreatedBy = 4,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
            var ovdInv = new Invoice
            {
                InvoiceNo = $"INV-SQL-OVD-{agrId}",
                AgreementId = agrId,
                CustomerId = 1,
                BillingPeriod = "2026-10",
                IssueDate = today.AddDays(-10),
                DueDate = today.AddDays(-2),
                Currency = "VND",
                SubtotalAmount = 1_000_000m,
                DiscountAmount = 0m,
                TaxAmount = 0m,
                TotalAmount = 1_000_000m,
                PaidAmount = 0m,
                Status = InvoiceStatusConstants.Draft,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-10)
            };
            db.Invoices.Add(ovdInv);
            await db.SaveChangesAsync();

            db.InvoiceLines.Add(new InvoiceLine
            {
                InvoiceId = ovdInv.Id,
                LineType = InvoiceLineTypeConstants.Rent,
                Description = "Monthly storage rent",
                Quantity = 1,
                UnitPrice = 1_000_000m,
                LineAmount = 1_000_000m,
                Metadata = "{}"
            });
            await db.SaveChangesAsync();

            ovdInv.Status = InvoiceStatusConstants.Open;
            ovdInv.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            ovdInvId = ovdInv.Id;
        }

        // Run lifecycle worker twice on live SQL Server
        using (var s1 = factory.Services.CreateScope())
            await s1.ServiceProvider.GetRequiredService<IStorageLifecycleService>().RunCycleAsync();
        using (var s2 = factory.Services.CreateScope())
            await s2.ServiceProvider.GetRequiredService<IStorageLifecycleService>().RunCycleAsync();

        using (var verifySqlScope = factory.Services.CreateScope())
        {
            var db = verifySqlScope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
            var ovdInv = await db.Invoices.AsNoTracking().Include(i => i.InvoiceLines).FirstAsync(i => i.Id == ovdInvId);
            Assert.Equal(InvoiceStatusConstants.Overdue, ovdInv.Status);
            var lateLines = ovdInv.InvoiceLines.Where(l => l.LineType == InvoiceLineTypeConstants.LateFee).ToList();
            Assert.Single(lateLines); // Trigger + idempotency check
            Assert.Equal(1_050_000m, ovdInv.TotalAmount); // trg_invoice_lines_refresh_totals updated TotalAmount in SQL Server!

            var agr = await db.RentalAgreements.AsNoTracking().FirstAsync(a => a.Id == agrId);
            Assert.Equal(RentalAgreementStatusConstants.Overdue, agr.Status);
        }

        // PIN change MUST be 409 while overdue
        Assert.Equal(HttpStatusCode.Conflict, (await cust.PutAsJsonAsync($"/api/v1/rentals/{agrId}/pin",
            new ChangePinRequest { CurrentPin = checkedIn.IssuedPin, NewPin = "482615" })).StatusCode);

        // Renew 1st time -> Checkout -> SePay webhook settles overdue debt & cures case & unlocks PIN
        var rnw1 = (await (await cust.PostAsJsonAsync($"/api/v1/rentals/{agrId}/renew", new { durationMonths = 1 }))
            .Content.ReadFromJsonAsync<ApiResponse<RenewAgreementResponseDto>>(JsonOpts))!.Data!;
        var rnw1Chk = (await (await cust.PostAsJsonAsync("/api/v1/payments/checkout", new CreateCheckoutRequest { RenewalId = rnw1.RenewalId }))
            .Content.ReadFromJsonAsync<ApiResponse<CheckoutResponse>>(JsonOpts))!.Data!;

        await hook.PostAsJsonAsync("/api/v1/webhooks/sepay", new SePayWebhookPayload
        {
            Id = Random.Shared.NextInt64(100000, 9999999),
            Gateway = "MBBank",
            TransferType = "in",
            TransferAmount = rnw1Chk.Amount,
            Content = rnw1Chk.VietQr!.TransferContent,
            ReferenceCode = $"FT-SQL-RNW1-{Guid.NewGuid():N}"
        });

        // PIN change succeeds after webhook!
        Assert.Equal(HttpStatusCode.OK, (await cust.PutAsJsonAsync($"/api/v1/rentals/{agrId}/pin",
            new ChangePinRequest { CurrentPin = checkedIn.IssuedPin, NewPin = "482615" })).StatusCode);

        // Verify rental_renewals_one_open_uidx allows a 2nd consecutive renewal on the same agreement on SQL Server!
        var rnw2Resp = await cust.PostAsJsonAsync($"/api/v1/rentals/{agrId}/renew", new { durationMonths = 1 });
        Assert.Equal(HttpStatusCode.Created, rnw2Resp.StatusCode);
        var rnw2 = (await rnw2Resp.Content.ReadFromJsonAsync<ApiResponse<RenewAgreementResponseDto>>(JsonOpts))!.Data!;
        Assert.Equal(HttpStatusCode.OK, (await cust.PostAsync($"/api/v1/renewals/{rnw2.RenewalId}/cancel", null)).StatusCode);

        // 4. Flow 3 Move-Out + Staff Inspect (triggers: handover_records, inspections, refunds_guard, unit transition) + Customer Bank Info blocking Cash Payout
        var moResp = await cust.PostAsJsonAsync($"/api/v1/rentals/{agrId}/move-out", new RequestMoveOutRequest(today.AddDays(1), "SQL early move out"));
        Assert.Equal(HttpStatusCode.Created, moResp.StatusCode);
        var mo = (await moResp.Content.ReadFromJsonAsync<ApiResponse<MoveOutResponseDto>>(JsonOpts))!.Data!;

        var staffInspResp = await staff.PostAsJsonAsync($"/api/v1/staff/move-outs/{mo.Id}/inspect", new InspectMoveOutRequest(
            OverallCondition: "good",
            Summary: "SQL Server check-out clean",
            DamageItems: []
        ));
        Assert.Equal(HttpStatusCode.OK, staffInspResp.StatusCode);
        var inspRes = (await staffInspResp.Content.ReadFromJsonAsync<ApiResponse<InspectMoveOutResultDto>>(JsonOpts))!.Data!;
        Assert.NotNull(inspRes.RefundId);
        Assert.Equal(StorageUnitStatusConstants.Cleaning, inspRes.UnitStatus);

        var bankResp = await cust.PostAsJsonAsync($"/api/v1/rentals/refunds/{inspRes.RefundId}/bank-info", new
        {
            bankCode = "MBBank",
            bankAccountNo = "0123456789",
            bankAccountName = "NGUYEN VAN TEST"
        });
        Assert.Equal(HttpStatusCode.OK, bankResp.StatusCode);

        // Once customer chose bank transfer, Staff counter cash payout MUST fail with 409 Conflict!
        var cashBlocked = await staff.PostAsJsonAsync($"/api/v1/staff/refunds/{inspRes.RefundId}/counter-cash-payout", new { notes = "Should block" });
        Assert.Equal(HttpStatusCode.Conflict, cashBlocked.StatusCode);
    }

    [Fact]
    public async Task LiveSqlServer_ManagerLiquidation_PassesAllTriggersAndConstraints()
    {
        await using var factory = new SqlServerWebFactory(_fixture.ConnectionString);
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

        var cust = factory.CreateClient();
        cust.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync("customer.one@example.test"));
        var staff = factory.CreateClient();
        staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync("staff.hcm@example.test"));
        var mgr = factory.CreateClient();
        mgr.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync("manager.hcm@example.test"));

        var tz = TimeZoneHelper.ResolveTimeZone("Asia/Ho_Chi_Minh");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime);

        // Create & check in reservation on unit 2
        var rsvResp = await cust.PostAsJsonAsync("/api/v1/reservations", new CreateReservationRequest
        {
            FacilityId = 1,
            UnitTypeId = 1,
            StorageUnitId = 2,
            StartDate = today,
            DurationMonths = 1,
            IdempotencyKey = $"sql-liq-{Guid.NewGuid():N}"
        });
        Assert.Equal(HttpStatusCode.Created, rsvResp.StatusCode);
        var rsv = (await rsvResp.Content.ReadFromJsonAsync<ApiResponse<CreateReservationResponse>>(JsonOpts))!.Data!;

        var chk = (await (await cust.PostAsJsonAsync("/api/v1/payments/checkout", new CreateCheckoutRequest { ReservationId = rsv.ReservationId }))
            .Content.ReadFromJsonAsync<ApiResponse<CheckoutResponse>>(JsonOpts))!.Data!;

        var hook = factory.CreateClient();
        hook.DefaultRequestHeaders.Add("X-Api-Key", factory.SePayApiKey);
        await hook.PostAsJsonAsync("/api/v1/webhooks/sepay", new SePayWebhookPayload
        {
            Id = Random.Shared.NextInt64(100000, 9999999),
            Gateway = "MBBank",
            TransferType = "in",
            TransferAmount = chk.Amount,
            Content = chk.VietQr!.TransferContent,
            ReferenceCode = $"FT-SQL-LIQ-{Guid.NewGuid():N}"
        });

        var ciResp = await staff.PostAsJsonAsync("/api/v1/staff/check-ins/confirm", new ConfirmCheckInRequest
        {
            ReservationId = rsv.ReservationId,
            IdentityVerification = new IdentityVerificationInput
            {
                Method = "government_id",
                DocumentNumber = "079200008888",
                FullNameMatched = true,
                Notes = "CCCD OK"
            },
            Inspection = new CheckInInspectionInput
            {
                OverallCondition = "good",
                Summary = "Clean",
                Items = [new CheckInInspectionItemInput { ItemName = "Door", Condition = "good" }]
            },
            CustomerSignatureRef = "sig-c",
            StaffSignatureRef = "sig-s",
            Notes = "OK"
        });
        ciResp.EnsureSuccessStatusCode();
        var checkedIn = (await ciResp.Content.ReadFromJsonAsync<ApiResponse<ConfirmCheckInResponse>>(JsonOpts))!.Data!;

        // Age EndDate >30 days in past + unpaid overdue invoice -> run StorageLifecycleService -> defaulted -> Manager liquidate
        using (var ageScope = factory.Services.CreateScope())
        {
            var db = ageScope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
            var agr = await db.RentalAgreements.FirstAsync(a => a.Id == checkedIn.AgreementId);
            agr.StartDate = today.AddDays(-65);
            agr.EndDate = today.AddDays(-35);
            await db.SaveChangesAsync();

            var inv = new Invoice
            {
                InvoiceNo = $"INV-LIQ-{agr.Id}",
                AgreementId = agr.Id,
                CustomerId = agr.CustomerId,
                BillingPeriod = "2026-08",
                IssueDate = today.AddDays(-40),
                DueDate = today.AddDays(-35),
                Currency = "VND",
                SubtotalAmount = 1_500_000m,
                DiscountAmount = 0m,
                TaxAmount = 0m,
                TotalAmount = 1_500_000m,
                PaidAmount = 0m,
                Status = InvoiceStatusConstants.Draft,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-40),
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-40)
            };
            db.Invoices.Add(inv);
            await db.SaveChangesAsync();

            db.InvoiceLines.Add(new InvoiceLine
            {
                InvoiceId = inv.Id,
                LineType = InvoiceLineTypeConstants.Rent,
                Description = "Past due rent",
                Quantity = 1,
                UnitPrice = 1_500_000m,
                LineAmount = 1_500_000m,
                Metadata = "{}"
            });
            await db.SaveChangesAsync();

            inv.Status = InvoiceStatusConstants.Open;
            await db.SaveChangesAsync();

            await ageScope.ServiceProvider.GetRequiredService<IStorageLifecycleService>().RunCycleAsync();
        }

        var liqResp = await mgr.PostAsJsonAsync($"/api/v1/manager/agreements/{checkedIn.AgreementId}/liquidate", new ManagerLiquidateAgreementRequest
        {
            Reason = "Defaulted >30 days on SQL Server",
            AuctionProceedsAmount = 500_000m,
            DeductDeposit = true
        });
        Assert.Equal(HttpStatusCode.OK, liqResp.StatusCode);
        var liq = (await liqResp.Content.ReadFromJsonAsync<ApiResponse<ManagerLiquidateAgreementResultDto>>(JsonOpts))!.Data!;
        Assert.Equal(RentalAgreementStatusConstants.Terminated, liq.AgreementStatus);
        Assert.Equal(StorageUnitStatusConstants.PendingInspection, liq.UnitStatus);
        Assert.True(liq.RevokedCredentialsCount >= 1);
    }
}
