using Microsoft.EntityFrameworkCore;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;
using SelfStorageSystem.Infrastructure.Common;
using SelfStorageSystem.Infrastructure.Services;

namespace SelfStorageSystem.Infrastructure.Persistence;

/// <summary>
/// Idempotent database seeder covering all 3 canonical Roles (Customer, Staff, Manager)
/// and all 7 Core Business Flows (Reservation -> Check-in -> Rental Mgmt -> Pricing/Rules ->
/// Facility/Staff -> Renewal/Overdue -> Support Tickets/Refunds).
/// </summary>
public static class DbSeeder
{
    public const string DefaultDemoPasswordPlain = "Demo@123456";
    private const string SentinelManagerEmail = "manager.hcm@selfstorage.local";

    public static async Task SeedAsync(SelfStorageDbContext db, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(DefaultDemoPasswordPlain);

        // 1. Canonical 4 Roles
        var roleCustomer = await EnsureRoleAsync(db, UserRoleCode.Customer.ToDbValue(), "Customer", "Self-service storage customer", cancellationToken);
        var roleStaff = await EnsureRoleAsync(db, UserRoleCode.Staff.ToDbValue(), "Staff", "On-site facility counter & operations staff", cancellationToken);
        var roleManager = await EnsureRoleAsync(db, UserRoleCode.Manager.ToDbValue(), "Manager", "Global facility & business operations manager", cancellationToken);
        var roleAdmin = await EnsureRoleAsync(db, UserRoleCode.Admin.ToDbValue(), "Admin", "System administrator", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        // Sentinel guard for idempotent re-runs
        if (await db.Users.AnyAsync(u => u.Email == SentinelManagerEmail, cancellationToken))
        {
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            return;
        }

        // 2. Users (4 Roles: 2 Customers, 2 Staff, 2 Managers, 1 Admin)
        var customer1 = CreateUser("customer.one@selfstorage.local", "0901000001", passwordHash, now);
        var customer2 = CreateUser("customer.two@selfstorage.local", "0901000002", passwordHash, now);
        var staffHcm = CreateUser("staff.hcm@selfstorage.local", "0902000001", passwordHash, now);
        var staffHn = CreateUser("staff.hn@selfstorage.local", "0902000002", passwordHash, now);
        var managerHcm = CreateUser(SentinelManagerEmail, "0903000001", passwordHash, now);
        var managerHn = CreateUser("manager.hn@selfstorage.local", "0903000002", passwordHash, now);
        var adminUser = CreateUser("admin@selfstorage.local", "0900000001", passwordHash, now);

        db.Users.AddRange(customer1, customer2, staffHcm, staffHn, managerHcm, managerHn, adminUser);
        await db.SaveChangesAsync(cancellationToken);

        db.UserRoles.AddRange(
            new UserRole { UserId = customer1.Id, RoleId = roleCustomer.Id, GrantedAt = now },
            new UserRole { UserId = customer2.Id, RoleId = roleCustomer.Id, GrantedAt = now },
            new UserRole { UserId = staffHcm.Id, RoleId = roleStaff.Id, GrantedBy = managerHcm.Id, GrantedAt = now },
            new UserRole { UserId = staffHn.Id, RoleId = roleStaff.Id, GrantedBy = managerHn.Id, GrantedAt = now },
            new UserRole { UserId = managerHcm.Id, RoleId = roleManager.Id, GrantedAt = now },
            new UserRole { UserId = managerHn.Id, RoleId = roleManager.Id, GrantedAt = now },
            new UserRole { UserId = adminUser.Id, RoleId = roleAdmin.Id, GrantedAt = now }
        );

        db.CustomerProfiles.AddRange(
            new CustomerProfile
            {
                UserId = customer1.Id,
                FullName = "Nguyen Minh Anh",
                IdentityNumber = "079200001001",
                DateOfBirth = new DateOnly(1998, 4, 12),
                Address = "Thu Duc City, Ho Chi Minh City",
                EmergencyContactName = "Nguyen Minh Tam",
                EmergencyContactPhone = "0909111222",
                CreatedAt = now,
                UpdatedAt = now
            },
            new CustomerProfile
            {
                UserId = customer2.Id,
                FullName = "Tran Gia Binh",
                IdentityNumber = "079200001002",
                DateOfBirth = new DateOnly(1995, 9, 20),
                Address = "District 7, Ho Chi Minh City",
                EmergencyContactName = "Tran Gia Bao",
                EmergencyContactPhone = "0909333444",
                CreatedAt = now,
                UpdatedAt = now
            }
        );

        db.EmployeeProfiles.AddRange(
            new EmployeeProfile { UserId = staffHcm.Id, EmployeeCode = "SEED-EMP-HCM-01", FullName = "Le Hoang Staff", HireDate = new DateOnly(2025, 1, 10), EmploymentStatus = EmploymentStatus.Active.ToDbValue(), CreatedAt = now, UpdatedAt = now },
            new EmployeeProfile { UserId = staffHn.Id, EmployeeCode = "SEED-EMP-HN-01", FullName = "Vu Minh Staff", HireDate = new DateOnly(2025, 2, 15), EmploymentStatus = EmploymentStatus.Active.ToDbValue(), CreatedAt = now, UpdatedAt = now },
            new EmployeeProfile { UserId = managerHcm.Id, EmployeeCode = "SEED-MGR-HCM-01", FullName = "Pham Thu Manager", HireDate = new DateOnly(2024, 3, 1), EmploymentStatus = EmploymentStatus.Active.ToDbValue(), CreatedAt = now, UpdatedAt = now },
            new EmployeeProfile { UserId = managerHn.Id, EmployeeCode = "SEED-MGR-HN-01", FullName = "Do Quang Manager", HireDate = new DateOnly(2024, 4, 1), EmploymentStatus = EmploymentStatus.Active.ToDbValue(), CreatedAt = now, UpdatedAt = now },
            new EmployeeProfile { UserId = adminUser.Id, EmployeeCode = "SEED-ADM-01", FullName = "System Administrator", HireDate = new DateOnly(2024, 1, 1), EmploymentStatus = EmploymentStatus.Active.ToDbValue(), CreatedAt = now, UpdatedAt = now }
        );
        await db.SaveChangesAsync(cancellationToken);

        // 3. Flow 5: Facilities, Assignments, Areas, UnitTypes, Units & Map Positions
        var facHcm = await EnsureFacilityAsync(db, "SEED-HCM-01", "Kho Tu Phuc Vu Quan 1", "123 Le Loi", "Ben Thanh", "District 1", "Ho Chi Minh", 10.776900m, 106.700900m, now, cancellationToken);
        var facHn = await EnsureFacilityAsync(db, "SEED-HN-01", "Kho Tu Phuc Vu Cau Giay", "10 Tran Thai Tong", "Dich Vong", "Cau Giay", "Ha Noi", 21.033300m, 105.787500m, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var assignStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        db.StaffFacilityAssignments.AddRange(
            new StaffFacilityAssignment { EmployeeId = staffHcm.Id, FacilityId = facHcm.Id, AssignmentRole = UserRoleCode.Staff.ToDbValue(), StartsAt = assignStart, AssignedBy = managerHcm.Id, CreatedAt = now },
            new StaffFacilityAssignment { EmployeeId = staffHn.Id, FacilityId = facHn.Id, AssignmentRole = UserRoleCode.Staff.ToDbValue(), StartsAt = assignStart, AssignedBy = managerHn.Id, CreatedAt = now },
            new StaffFacilityAssignment { EmployeeId = managerHcm.Id, FacilityId = facHcm.Id, AssignmentRole = UserRoleCode.Manager.ToDbValue(), StartsAt = assignStart, AssignedBy = managerHcm.Id, CreatedAt = now },
            new StaffFacilityAssignment { EmployeeId = managerHn.Id, FacilityId = facHn.Id, AssignmentRole = UserRoleCode.Manager.ToDbValue(), StartsAt = assignStart, AssignedBy = managerHn.Id, CreatedAt = now }
        );
        await db.SaveChangesAsync(cancellationToken);

        var hcmBuilding = new FacilityArea { FacilityId = facHcm.Id, Code = "BLDG-A", Name = "Building A", AreaType = AreaType.Building.ToDbValue(), DisplayOrder = 1, MapMetadata = "{}", IsActive = true };
        var hnBuilding = new FacilityArea { FacilityId = facHn.Id, Code = "BLDG-HN", Name = "Ha Noi Building", AreaType = AreaType.Building.ToDbValue(), DisplayOrder = 1, MapMetadata = "{}", IsActive = true };
        db.FacilityAreas.AddRange(hcmBuilding, hnBuilding);
        await db.SaveChangesAsync(cancellationToken);

        var hcmFloor1 = new FacilityArea { FacilityId = facHcm.Id, ParentAreaId = hcmBuilding.Id, Code = "F1", Name = "Floor 1", AreaType = AreaType.Floor.ToDbValue(), DisplayOrder = 1, MapMetadata = "{}", IsActive = true };
        var hnFloor1 = new FacilityArea { FacilityId = facHn.Id, ParentAreaId = hnBuilding.Id, Code = "HN-F1", Name = "Floor 1", AreaType = AreaType.Floor.ToDbValue(), DisplayOrder = 1, MapMetadata = "{}", IsActive = true };
        db.FacilityAreas.AddRange(hcmFloor1, hnFloor1);
        await db.SaveChangesAsync(cancellationToken);

        var hcmZoneA = new FacilityArea { FacilityId = facHcm.Id, ParentAreaId = hcmFloor1.Id, Code = "F1-ZA", Name = "Floor 1 - Zone A", AreaType = AreaType.Zone.ToDbValue(), DisplayOrder = 1, MapMetadata = "{}", IsActive = true };
        db.FacilityAreas.Add(hcmZoneA);
        await db.SaveChangesAsync(cancellationToken);

        var utSmall = await EnsureUnitTypeAsync(db, "SEED-S-DRY", "Small Dry Unit 3m2", 1.50m, 2.00m, 2.50m, false, 600m, "Dry unit for personal boxes and archives", now, cancellationToken);
        var utMedium = await EnsureUnitTypeAsync(db, "SEED-M-DRY", "Medium Dry Unit 6m2", 2.00m, 3.00m, 2.50m, false, 1200m, "Spacious dry storage for household furniture", now, cancellationToken);
        var utLarge = await EnsureUnitTypeAsync(db, "SEED-L-CLIMATE", "Large Climate Unit 10m2", 2.50m, 4.00m, 2.80m, true, 2000m, "Climate-controlled unit for sensitive inventory", now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        // Storage Units covering canonical UnitStatus values
        var unitA101 = CreateUnit(facHcm.Id, utSmall.Id, hcmZoneA.Id, "A-101", "1", "A", UnitStatus.Available, now);
        var unitA102 = CreateUnit(facHcm.Id, utMedium.Id, hcmZoneA.Id, "A-102", "1", "A", UnitStatus.Available, now);
        var unitA103 = CreateUnit(facHcm.Id, utSmall.Id, hcmZoneA.Id, "A-103", "1", "A", UnitStatus.Reserved, now);
        var unitA104 = CreateUnit(facHcm.Id, utSmall.Id, hcmZoneA.Id, "A-104", "1", "A", UnitStatus.Reserved, now);
        var unitA105 = CreateUnit(facHcm.Id, utMedium.Id, hcmZoneA.Id, "A-105", "1", "A", UnitStatus.Occupied, now);
        var unitA106 = CreateUnit(facHcm.Id, utSmall.Id, hcmZoneA.Id, "A-106", "1", "A", UnitStatus.Occupied, now);
        var unitA107 = CreateUnit(facHcm.Id, utSmall.Id, hcmZoneA.Id, "A-107", "1", "A", UnitStatus.Cleaning, now);
        var unitA108 = CreateUnit(facHcm.Id, utLarge.Id, hcmZoneA.Id, "A-108", "1", "A", UnitStatus.Maintenance, now);
        var unitA109 = CreateUnit(facHcm.Id, utSmall.Id, hcmZoneA.Id, "A-109", "1", "A", UnitStatus.Damaged, now);
        var unitA110 = CreateUnit(facHcm.Id, utMedium.Id, hcmZoneA.Id, "A-110", "1", "A", UnitStatus.PendingInspection, now);
        var unitHn101 = CreateUnit(facHn.Id, utSmall.Id, hnFloor1.Id, "HN-101", "1", "A", UnitStatus.Available, now);
        var unitHn102 = CreateUnit(facHn.Id, utMedium.Id, hnFloor1.Id, "HN-102", "1", "A", UnitStatus.Occupied, now);

        var allUnits = new[] { unitA101, unitA102, unitA103, unitA104, unitA105, unitA106, unitA107, unitA108, unitA109, unitA110, unitHn101, unitHn102 };
        db.StorageUnits.AddRange(allUnits);
        await db.SaveChangesAsync(cancellationToken);

        for (var i = 0; i < allUnits.Length; i++)
        {
            var u = allUnits[i];
            db.UnitMapPositions.Add(new UnitMapPosition
            {
                UnitId = u.Id,
                AreaId = u.AreaId!.Value,
                X = 20m + (i % 5) * 90m,
                Y = 20m + (i / 5) * 90m,
                Width = 70m,
                Height = 70m,
                RotationDegrees = 0m,
                Metadata = "{}"
            });
        }

        db.UnitStatusHistories.AddRange(
            new UnitStatusHistory { StorageUnitId = unitA107.Id, OldStatus = UnitStatus.Occupied.ToDbValue(), NewStatus = UnitStatus.Cleaning.ToDbValue(), Reason = "Post-moveout sanitation", ChangedBy = staffHcm.Id, ChangedAt = now.AddHours(-2) },
            new UnitStatusHistory { StorageUnitId = unitA108.Id, OldStatus = UnitStatus.Available.ToDbValue(), NewStatus = UnitStatus.Maintenance.ToDbValue(), Reason = "Roller shutter motor repair", ChangedBy = managerHcm.Id, ChangedAt = now.AddDays(-1) }
        );

        var apHcmGate = new AccessPoint { FacilityId = facHcm.Id, Code = "SEED-GATE-01", Name = "HCM Main Gate", AccessPointType = AccessPointType.Gate.ToDbValue(), ExternalDeviceCode = "DEV-HCM-GATE-01", Status = AccessPointStatus.Active.ToDbValue() };
        var apHcmDoor = new AccessPoint { FacilityId = facHcm.Id, Code = "SEED-DOOR-A", Name = "Zone A Smart Door", AccessPointType = AccessPointType.UnitDoor.ToDbValue(), ExternalDeviceCode = "DEV-HCM-DOOR-A", Status = AccessPointStatus.Active.ToDbValue() };
        var apHnGate = new AccessPoint { FacilityId = facHn.Id, Code = "SEED-HN-GATE-01", Name = "Ha Noi Main Gate", AccessPointType = AccessPointType.Gate.ToDbValue(), ExternalDeviceCode = "DEV-HN-GATE-01", Status = AccessPointStatus.Active.ToDbValue() };
        db.AccessPoints.AddRange(apHcmGate, apHcmDoor, apHnGate);

        // Shifts & Tasks (Flow 5)
        var shiftToday = new StaffShift
        {
            FacilityId = facHcm.Id,
            ShiftName = "HCM Morning Shift",
            StartsAt = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero),
            EndsAt = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero).AddDays(1),
            Status = StaffShiftStatus.Open.ToDbValue(),
            CreatedBy = managerHcm.Id,
            CreatedAt = now
        };
        db.StaffShifts.Add(shiftToday);
        await db.SaveChangesAsync(cancellationToken);

        db.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftId = shiftToday.Id,
            EmployeeId = staffHcm.Id,
            DutyRole = DutyRole.Staff.ToDbValue(),
            CheckInAt = now.AddHours(-1),
            Status = ShiftAssignmentStatus.CheckedIn.ToDbValue()
        });

        // 4. Flow 4: Price Ranges, Facility Rates, Policies, Fee Rules, Promotions
        var rateStart = new DateOnly(2026, 1, 1);
        db.PriceRanges.AddRange(
            new PriceRange { UnitTypeId = utSmall.Id, MinMonthlyRate = 600_000m, MaxMonthlyRate = 1_400_000m, ValidFrom = rateStart, CreatedBy = managerHcm.Id, CreatedAt = now },
            new PriceRange { UnitTypeId = utMedium.Id, MinMonthlyRate = 1_100_000m, MaxMonthlyRate = 2_300_000m, ValidFrom = rateStart, CreatedBy = managerHcm.Id, CreatedAt = now },
            new PriceRange { UnitTypeId = utLarge.Id, MinMonthlyRate = 1_800_000m, MaxMonthlyRate = 3_500_000m, ValidFrom = rateStart, CreatedBy = managerHcm.Id, CreatedAt = now }
        );
        await db.SaveChangesAsync(cancellationToken);

        var rateHcmSmall = new FacilityRate { FacilityId = facHcm.Id, UnitTypeId = utSmall.Id, MonthlyRate = 900_000m, DepositAmount = 900_000m, BookingFee = 50_000m, ValidFrom = rateStart, CreatedBy = managerHcm.Id, CreatedAt = now };
        var rateHcmMed = new FacilityRate { FacilityId = facHcm.Id, UnitTypeId = utMedium.Id, MonthlyRate = 1_500_000m, DepositAmount = 1_500_000m, BookingFee = 50_000m, ValidFrom = rateStart, CreatedBy = managerHcm.Id, CreatedAt = now };
        var rateHcmLarge = new FacilityRate { FacilityId = facHcm.Id, UnitTypeId = utLarge.Id, MonthlyRate = 2_400_000m, DepositAmount = 2_400_000m, BookingFee = 100_000m, ValidFrom = rateStart, CreatedBy = managerHcm.Id, CreatedAt = now };
        var rateHnSmall = new FacilityRate { FacilityId = facHn.Id, UnitTypeId = utSmall.Id, MonthlyRate = 850_000m, DepositAmount = 850_000m, BookingFee = 50_000m, ValidFrom = rateStart, CreatedBy = managerHn.Id, CreatedAt = now };
        var rateHnMed = new FacilityRate { FacilityId = facHn.Id, UnitTypeId = utMedium.Id, MonthlyRate = 1_400_000m, DepositAmount = 1_400_000m, BookingFee = 50_000m, ValidFrom = rateStart, CreatedBy = managerHn.Id, CreatedAt = now };
        db.FacilityRates.AddRange(rateHcmSmall, rateHcmMed, rateHcmLarge, rateHnSmall, rateHnMed);
        await db.SaveChangesAsync(cancellationToken);

        var policyRental = await EnsurePolicyAsync(db, PolicyType.RentalTerms, "2026.1-SEED", "{\"minimum_months\":1,\"billing_cycle\":\"monthly\"}", rateStart, managerHcm.Id, now, cancellationToken);
        await EnsurePolicyAsync(db, PolicyType.Deposit, "2026.1-SEED", "{\"refund_after_checkout\":true,\"early_moveout_penalty_percent\":10}", rateStart, managerHcm.Id, now, cancellationToken);
        await EnsurePolicyAsync(db, PolicyType.Cancellation, "2026.1-SEED", "{\"free_before_start_days\":3}", rateStart, managerHcm.Id, now, cancellationToken);
        await EnsurePolicyAsync(db, PolicyType.Renewal, "2026.1-SEED", "{\"reminder_days\":[7,3,1]}", rateStart, managerHcm.Id, now, cancellationToken);
        await EnsurePolicyAsync(db, PolicyType.MoveOut, "2026.1-SEED", "{\"inspection_required\":true}", rateStart, managerHcm.Id, now, cancellationToken);
        await EnsurePolicyAsync(db, PolicyType.Overdue, "2026.1-SEED", "{\"grace_days\":0,\"suspend_pin_from_day\":1,\"default_after_days\":30}", rateStart, managerHcm.Id, now, cancellationToken);

        db.FeeRules.AddRange(
            new FeeRule { FacilityId = facHcm.Id, Code = "SEED-LATE-DAILY", FeeType = FeeType.LateFee.ToDbValue(), CalculationMethod = FeeCalculationMethod.PerDay.ToDbValue(), Amount = 50_000m, GraceDays = 0, Conditions = "{}", ValidFrom = rateStart, IsActive = true, CreatedBy = managerHcm.Id, CreatedAt = now },
            new FeeRule { FacilityId = facHcm.Id, Code = "SEED-DAMAGE-STD", FeeType = FeeType.DamageFee.ToDbValue(), CalculationMethod = FeeCalculationMethod.Flat.ToDbValue(), Amount = 200_000m, GraceDays = 0, Conditions = "{}", ValidFrom = rateStart, IsActive = true, CreatedBy = managerHcm.Id, CreatedAt = now },
            new FeeRule { FacilityId = facHcm.Id, Code = "SEED-LOST-KEY", FeeType = FeeType.LostKeyFee.ToDbValue(), CalculationMethod = FeeCalculationMethod.Flat.ToDbValue(), Amount = 150_000m, GraceDays = 0, Conditions = "{}", ValidFrom = rateStart, IsActive = true, CreatedBy = managerHcm.Id, CreatedAt = now },
            new FeeRule { FacilityId = null, Code = "SEED-VAT-10", FeeType = FeeType.Tax.ToDbValue(), CalculationMethod = FeeCalculationMethod.Percentage.ToDbValue(), RatePercent = 10.0000m, GraceDays = 0, Conditions = "{}", ValidFrom = rateStart, IsActive = true, CreatedBy = managerHcm.Id, CreatedAt = now },
            new FeeRule { FacilityId = null, Code = "SEED-BOOKING-STD", FeeType = FeeType.Booking.ToDbValue(), CalculationMethod = FeeCalculationMethod.Flat.ToDbValue(), Amount = 50_000m, GraceDays = 0, Conditions = "{}", ValidFrom = rateStart, IsActive = true, CreatedBy = managerHcm.Id, CreatedAt = now }
        );

        var promoWelcome = new Promotion
        {
            Code = "SEED-WELCOME10",
            Name = "Welcome 10% Off First Month",
            Description = "10% discount capped at 300,000 VND",
            DiscountType = PromotionDiscountType.Percentage.ToDbValue(),
            DiscountValue = 10m,
            MaxDiscountAmount = 300_000m,
            UsageLimit = 500,
            PerCustomerLimit = 2,
            ValidFrom = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ValidTo = new DateTimeOffset(2027, 12, 31, 23, 59, 59, TimeSpan.Zero),
            IsActive = true,
            CreatedBy = managerHcm.Id,
            CreatedAt = now
        };
        var promoLongTerm = new Promotion
        {
            Code = "SEED-LONGTERM20",
            Name = "Long-Term 20% Off",
            Description = "20% discount for 3+ months",
            DiscountType = PromotionDiscountType.Percentage.ToDbValue(),
            DiscountValue = 20m,
            MaxDiscountAmount = 600_000m,
            UsageLimit = 200,
            PerCustomerLimit = 2,
            ValidFrom = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ValidTo = new DateTimeOffset(2027, 12, 31, 23, 59, 59, TimeSpan.Zero),
            IsActive = true,
            CreatedBy = managerHcm.Id,
            CreatedAt = now
        };
        db.Promotions.AddRange(promoWelcome, promoLongTerm);
        await db.SaveChangesAsync(cancellationToken);

        db.PromotionRules.AddRange(
            new PromotionRule { PromotionId = promoWelcome.Id, RuleType = PromotionRuleType.MinimumMonths.ToDbValue(), Operator = PromotionOperator.Gte.ToDbValue(), RuleValue = "1" },
            new PromotionRule { PromotionId = promoLongTerm.Id, RuleType = PromotionRuleType.MinimumMonths.ToDbValue(), Operator = PromotionOperator.Gte.ToDbValue(), RuleValue = "3" }
        );
        await db.SaveChangesAsync(cancellationToken);


        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }
    private static User CreateUser(string email, string phone, string passwordHash, DateTimeOffset now) => new()
    {
        Email = email.Trim().ToLowerInvariant(),
        PhoneNumber = phone,
        PasswordHash = passwordHash,
        Status = UserStatus.Active.ToDbValue(),
        CreatedAt = now,
        UpdatedAt = now
    };

    private static StorageUnit CreateUnit(long facilityId, long unitTypeId, long areaId, string unitCode, string floorLabel, string zoneLabel, UnitStatus status, DateTimeOffset now) => new()
    {
        FacilityId = facilityId,
        UnitTypeId = unitTypeId,
        AreaId = areaId,
        UnitCode = unitCode,
        FloorLabel = floorLabel,
        ZoneLabel = zoneLabel,
        PhysicalStatus = status.ToDbValue(),
        IsListed = true,
        CreatedAt = now,
        UpdatedAt = now
    };

    private static async Task<Role> EnsureRoleAsync(SelfStorageDbContext db, string code, string displayName, string description, CancellationToken ct)
    {
        var existing = await db.Roles.FirstOrDefaultAsync(r => r.Code == code, ct);
        if (existing is not null) return existing;

        var role = new Role { Code = code, DisplayName = displayName, Description = description };
        db.Roles.Add(role);
        return role;
    }

    private static async Task<Facility> EnsureFacilityAsync(
        SelfStorageDbContext db, string code, string name, string address, string ward, string district, string city,
        decimal lat, decimal lng, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await db.Facilities.FirstOrDefaultAsync(f => f.Code == code, ct);
        if (existing is not null) return existing;

        var facility = new Facility
        {
            Code = code,
            Name = name,
            AddressLine = address,
            Ward = ward,
            District = district,
            City = city,
            Latitude = lat,
            Longitude = lng,
            Timezone = "Asia/Ho_Chi_Minh",
            OpeningTime = new TimeOnly(7, 0),
            ClosingTime = new TimeOnly(22, 0),
            Status = FacilityStatus.Active.ToDbValue(),
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Facilities.Add(facility);
        return facility;
    }

    private static async Task<UnitType> EnsureUnitTypeAsync(
        SelfStorageDbContext db, string code, string name, decimal width, decimal length, decimal height,
        bool climate, decimal maxWeight, string description, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await db.UnitTypes.FirstOrDefaultAsync(ut => ut.Code == code, ct);
        if (existing is not null) return existing;

        var ut = new UnitType
        {
            Code = code,
            Name = name,
            WidthM = width,
            LengthM = length,
            HeightM = height,
            ClimateControlled = climate,
            MaxWeightKg = maxWeight,
            Description = description,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.UnitTypes.Add(ut);
        return ut;
    }

    private static async Task<PolicyVersion> EnsurePolicyAsync(
        SelfStorageDbContext db, PolicyType type, string version, string jsonContent, DateOnly validFrom,
        long createdBy, DateTimeOffset now, CancellationToken ct)
    {
        var typeValue = type.ToDbValue();
        var existing = await db.PolicyVersions
            .Where(p => p.PolicyType == typeValue && (p.Version == version || p.ValidTo == null || p.ValidTo >= validFrom))
            .OrderByDescending(p => p.ValidFrom)
            .FirstOrDefaultAsync(ct);
        if (existing is not null) return existing;

        var policy = new PolicyVersion
        {
            PolicyType = typeValue,
            Version = version,
            Content = jsonContent,
            ValidFrom = validFrom,
            CreatedBy = createdBy,
            CreatedAt = now
        };
        db.PolicyVersions.Add(policy);
        await db.SaveChangesAsync(ct);
        return policy;
    }

    private static async Task<Invoice> CreateAndOpenInvoiceAsync(
        SelfStorageDbContext db,
        string invoiceNo,
        long customerId,
        long? reservationId,
        long? agreementId,
        DateOnly issueDate,
        DateOnly dueDate,
        IEnumerable<(InvoiceLineType LineType, string Description, decimal UnitPrice)> lines,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var lineList = lines.ToList();
        var subtotal = lineList.Where(l => l.LineType != InvoiceLineType.Discount).Sum(l => l.UnitPrice);
        var discount = lineList.Where(l => l.LineType == InvoiceLineType.Discount).Sum(l => Math.Abs(l.UnitPrice));
        var total = Math.Max(0m, subtotal - discount);

        // Insert as draft first so trg_invoice_lines_guard permits line inserts on SQL Server
        var invoice = new Invoice
        {
            InvoiceNo = invoiceNo,
            CustomerId = customerId,
            ReservationId = reservationId,
            AgreementId = agreementId,
            IssueDate = issueDate,
            DueDate = dueDate,
            Currency = "VND",
            SubtotalAmount = subtotal,
            DiscountAmount = discount,
            TaxAmount = 0m,
            TotalAmount = total,
            PaidAmount = 0m,
            Status = InvoiceStatus.Draft.ToDbValue(),
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);

        foreach (var (lineType, description, unitPrice) in lineList)
        {
            db.InvoiceLines.Add(new InvoiceLine
            {
                InvoiceId = invoice.Id,
                LineType = lineType.ToDbValue(),
                Description = description,
                Quantity = 1m,
                UnitPrice = unitPrice,
                Metadata = "{}"
            });
        }
        await db.SaveChangesAsync(ct);

        invoice.Status = InvoiceStatus.Open.ToDbValue();
        invoice.OpenedAt = now;
        invoice.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return invoice;
    }

    private static async Task<Payment> SettleInvoiceViaSePayAsync(
        SelfStorageDbContext db,
        long customerId,
        Invoice invoice,
        string providerTxId,
        string externalEventId,
        DateTimeOffset paidAt,
        CancellationToken ct)
    {
        db.IntegrationEvents.Add(new IntegrationEvent
        {
            Source = "sepay",
            ExternalEventId = externalEventId,
            EventType = "payment_received",
            Payload = $"{{\"invoiceId\":{invoice.Id},\"amount\":{invoice.TotalAmount}}}",
            Status = IntegrationEventStatus.Processed.ToDbValue(),
            ReceivedAt = paidAt,
            ProcessedAt = paidAt
        });

        var payment = new Payment
        {
            CustomerId = customerId,
            TargetInvoiceId = invoice.Id,
            Amount = invoice.TotalAmount,
            Currency = "VND",
            Method = PaymentMethod.BankTransfer.ToDbValue(),
            Provider = "SePay",
            ProviderTransactionId = providerTxId,
            IdempotencyKey = providerTxId,
            Status = PaymentStatus.Succeeded.ToDbValue(),
            PaidAt = paidAt,
            Metadata = "{}",
            CreatedAt = paidAt,
            UpdatedAt = paidAt
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);

        db.PaymentAllocations.Add(new PaymentAllocation
        {
            PaymentId = payment.Id,
            InvoiceId = invoice.Id,
            AllocatedAmount = invoice.TotalAmount,
            AllocatedAt = paidAt
        });
        await db.SaveChangesAsync(ct);

        // Sync tracked entity with SQL Server trigger update (or set explicitly on non-SQL-Server providers)
        if (!db.Database.IsSqlServer())
        {
            invoice.PaidAmount = invoice.TotalAmount;
            invoice.Status = InvoiceStatus.Paid.ToDbValue();
            invoice.UpdatedAt = paidAt;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            await db.Entry(invoice).ReloadAsync(ct);
        }

        return payment;
    }
}
