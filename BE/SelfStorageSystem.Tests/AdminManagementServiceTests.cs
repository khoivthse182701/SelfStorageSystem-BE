using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using SelfStorageSystem.Contracts.Admin;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;
using Xunit;

namespace SelfStorageSystem.Tests;

public class AdminManagementServiceTests
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
    public async Task GetUsersAsync_WithFiltersAndPaging_ReturnsFilteredPagedResult()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var adminService = new AdminManagementService(db, NullLogger<AdminManagementService>.Instance);

        var staffRole = new Role { Id = 1, Code = RoleConstants.FacilityStaff, DisplayName = "Staff" };
        var customerRole = new Role { Id = 2, Code = RoleConstants.Customer, DisplayName = "Customer" };
        db.Roles.AddRange(staffRole, customerRole);

        var u1 = new User { Id = 1, Email = "staff1@storage.local", Status = UserStatusConstants.Active, PasswordHash = "hash", CreatedAt = DateTimeOffset.UtcNow };
        u1.EmployeeProfile = new EmployeeProfile { UserId = 1, EmployeeCode = "EMP-001", FullName = "Staff One", HireDate = new DateOnly(2025, 1, 1), EmploymentStatus = "active" };
        u1.UserRoleUsers.Add(new UserRole { UserId = 1, RoleId = 1, Role = staffRole });

        var u2 = new User { Id = 2, Email = "customer1@storage.local", Status = UserStatusConstants.Active, PasswordHash = "hash", CreatedAt = DateTimeOffset.UtcNow };
        u2.CustomerProfile = new CustomerProfile { UserId = 2, FullName = "Customer One" };
        u2.UserRoleUsers.Add(new UserRole { UserId = 2, RoleId = 2, Role = customerRole });

        db.Users.AddRange(u1, u2);
        await db.SaveChangesAsync();

        // Act - Search by role
        var result = await adminService.GetUsersAsync(new AdminUserQueryParameters
        {
            Role = RoleConstants.FacilityStaff,
            PageNumber = 1,
            PageSize = 10
        });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("staff1@storage.local", result.Items[0].Email);
        Assert.Equal("EMP-001", result.Items[0].EmployeeCode);
    }

    [Fact]
    public async Task GetUserDetailAsync_WhenUserExists_ReturnsDetailWithRolesAndRecentLogins()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var adminService = new AdminManagementService(db, NullLogger<AdminManagementService>.Instance);

        var role = new Role { Id = 1, Code = RoleConstants.FacilityManager, DisplayName = "Manager" };
        db.Roles.Add(role);

        var user = new User { Id = 10, Email = "manager@storage.local", Status = UserStatusConstants.Active, PasswordHash = "hash", CreatedAt = DateTimeOffset.UtcNow };
        user.EmployeeProfile = new EmployeeProfile { UserId = 10, EmployeeCode = "MGR-001", FullName = "Manager Dan", HireDate = new DateOnly(2025, 1, 1), EmploymentStatus = "active" };
        user.UserRoleUsers.Add(new UserRole { UserId = 10, RoleId = 1, Role = role, GrantedAt = DateTimeOffset.UtcNow });

        db.Users.Add(user);
        db.LoginHistories.Add(new LoginHistory
        {
            Id = 1,
            UserId = 10,
            AttemptedEmail = "manager@storage.local",
            Result = LoginHistoryResultConstants.Succeeded,
            OccurredAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        // Act
        var detail = await adminService.GetUserDetailAsync(10);

        // Assert
        Assert.NotNull(detail);
        Assert.Equal(10, detail.Id);
        Assert.Equal("manager@storage.local", detail.Email);
        Assert.Single(detail.Roles);
        Assert.Equal(RoleConstants.FacilityManager, detail.Roles[0].Code);
        Assert.NotNull(detail.EmployeeProfile);
        Assert.Equal("MGR-001", detail.EmployeeProfile.EmployeeCode);
        Assert.Single(detail.RecentLoginHistories);
    }

    [Fact]
    public async Task CreateInternalUserAsync_DuplicateEmail_ThrowsConflictException()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var adminService = new AdminManagementService(db, NullLogger<AdminManagementService>.Instance);

        db.Users.Add(new User { Id = 1, Email = "existing@storage.local", Status = UserStatusConstants.Active, PasswordHash = "hash" });
        await db.SaveChangesAsync();

        var request = new AdminCreateUserRequest
        {
            Email = "existing@storage.local",
            Password = "Password123!",
            FullName = "New Staff",
            Role = RoleConstants.FacilityStaff
        };

        // Act & Assert
        await Assert.ThrowsAsync<AppConflictException>(() =>
            adminService.CreateInternalUserAsync(request, adminUserId: 99));
    }

    [Fact]
    public async Task CreateInternalUserAsync_ValidRequest_CreatesUserWithEmployeeProfileAndAuditLog()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var adminService = new AdminManagementService(db, NullLogger<AdminManagementService>.Instance);

        var staffRole = new Role { Id = 1, Code = RoleConstants.FacilityStaff, DisplayName = "Staff" };
        db.Roles.Add(staffRole);
        var facility = new Facility { Id = 5, Code = "FAC-05", Name = "Thu Duc Facility", AddressLine = "Road 1", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        db.Facilities.Add(facility);
        await db.SaveChangesAsync();

        var request = new AdminCreateUserRequest
        {
            Email = "newstaff@storage.local",
            Password = "SecurePassword123!",
            FullName = "Nguyen Staff",
            Role = RoleConstants.FacilityStaff,
            InitialFacilityId = 5
        };

        // Act
        var result = await adminService.CreateInternalUserAsync(request, adminUserId: 99);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("newstaff@storage.local", result.Email);
        Assert.NotNull(result.EmployeeProfile);
        Assert.Equal("Nguyen Staff", result.EmployeeProfile.FullName);
        Assert.Single(result.EmployeeProfile.FacilityAssignments);
        Assert.Equal(5, result.EmployeeProfile.FacilityAssignments[0].FacilityId);

        // Verify audit log created
        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityType == AuditEntityTypeConstants.User && a.Action == AuditActionConstants.Create);
        Assert.NotNull(audit);
        Assert.Equal(99, audit.ActorUserId);
    }

    [Fact]
    public async Task UpdateUserStatusAsync_AdminCannotDeactivateSelf_ThrowsValidationException()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var adminService = new AdminManagementService(db, NullLogger<AdminManagementService>.Instance);

        var request = new AdminUpdateUserStatusRequest
        {
            Status = UserStatusConstants.Locked,
            Reason = "Testing"
        };

        // Act & Assert (id == adminUserId)
        await Assert.ThrowsAsync<AppValidationException>(() =>
            adminService.UpdateUserStatusAsync(id: 42, request, adminUserId: 42));
    }

    [Fact]
    public async Task UpdateUserStatusAsync_ValidUser_UpdatesStatusAndRecordsAuditLog()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var adminService = new AdminManagementService(db, NullLogger<AdminManagementService>.Instance);

        var targetUser = new User { Id = 15, Email = "user15@storage.local", Status = UserStatusConstants.Active, PasswordHash = "hash" };
        db.Users.Add(targetUser);
        await db.SaveChangesAsync();

        var request = new AdminUpdateUserStatusRequest
        {
            Status = UserStatusConstants.Locked,
            Reason = "Suspected fraud"
        };

        // Act
        var updated = await adminService.UpdateUserStatusAsync(id: 15, request, adminUserId: 1);

        // Assert
        Assert.Equal(UserStatusConstants.Locked, updated.Status);
        var dbUser = await db.Users.FindAsync(15L);
        Assert.Equal(UserStatusConstants.Locked, dbUser!.Status);

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.Action == AuditActionConstants.UpdateStatus);
        Assert.NotNull(audit);
        Assert.Equal(1, audit.ActorUserId);
    }

    [Fact]
    public async Task UpdateUserRolesAsync_AdminCannotDemoteSelf_ThrowsValidationException()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var adminService = new AdminManagementService(db, NullLogger<AdminManagementService>.Instance);

        var adminUser = new User { Id = 1, Email = "admin@storage.local", Status = UserStatusConstants.Active, PasswordHash = "hash" };
        var adminRole = new Role { Id = 1, Code = RoleConstants.SystemAdministrator, DisplayName = "Admin" };
        var staffRole = new Role { Id = 2, Code = RoleConstants.FacilityStaff, DisplayName = "Staff" };
        db.Roles.AddRange(adminRole, staffRole);
        adminUser.UserRoleUsers.Add(new UserRole { UserId = 1, RoleId = 1, Role = adminRole });
        db.Users.Add(adminUser);
        await db.SaveChangesAsync();

        var request = new AdminUpdateUserRolesRequest
        {
            Roles = new List<string> { RoleConstants.FacilityStaff } // removing SystemAdministrator
        };

        // Act & Assert
        await Assert.ThrowsAsync<AppValidationException>(() =>
            adminService.UpdateUserRolesAsync(id: 1, request, adminUserId: 1));
    }

    [Fact]
    public async Task AssignStaffToFacility_And_RevokeAssignment_WorksSuccessfully()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var adminService = new AdminManagementService(db, NullLogger<AdminManagementService>.Instance);

        var user = new User { Id = 7, Email = "staff7@storage.local", Status = UserStatusConstants.Active, PasswordHash = "hash" };
        var employee = new EmployeeProfile { UserId = 7, EmployeeCode = "EMP-007", FullName = "James Staff", HireDate = new DateOnly(2025, 1, 1), EmploymentStatus = "active" };
        var facility = new Facility { Id = 10, Code = "FAC-10", Name = "Central Facility", AddressLine = "Main St", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };

        db.Users.Add(user);
        db.EmployeeProfiles.Add(employee);
        db.Facilities.Add(facility);
        await db.SaveChangesAsync();

        var assignRequest = new AdminCreateStaffAssignmentRequest
        {
            EmployeeId = 7,
            FacilityId = 10,
            AssignmentRole = StaffAssignmentRoles.FacilityStaff
        };

        // Act - Assign
        var assignmentDto = await adminService.AssignStaffToFacilityAsync(assignRequest, adminUserId: 1);

        // Assert
        Assert.NotNull(assignmentDto);
        Assert.Equal(7, assignmentDto.EmployeeId);
        Assert.Equal(10, assignmentDto.FacilityId);
        Assert.True(assignmentDto.IsActive);

        // Act - Re-assign duplicate should throw Conflict
        await Assert.ThrowsAsync<AppConflictException>(() =>
            adminService.AssignStaffToFacilityAsync(assignRequest, adminUserId: 1));

        // Act - Revoke
        await adminService.RevokeStaffAssignmentAsync(assignmentDto.Id, adminUserId: 1);

        // Assert - Assignment removed
        var dbAssignment = await db.StaffFacilityAssignments.FindAsync(assignmentDto.Id);
        Assert.Null(dbAssignment);
    }

    [Fact]
    public async Task GetLoginHistoriesAsync_DetectsSuspiciousEntries_AndCalculatesSummary()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var adminService = new AdminManagementService(db, NullLogger<AdminManagementService>.Instance);

        var now = DateTimeOffset.UtcNow;
        db.LoginHistories.AddRange(
            new LoginHistory { Id = 1, UserId = 1, AttemptedEmail = "user@test.com", Result = LoginHistoryResultConstants.Succeeded, OccurredAt = now },
            new LoginHistory { Id = 2, UserId = 1, AttemptedEmail = "user@test.com", Result = LoginHistoryResultConstants.Locked, OccurredAt = now.AddMinutes(-5) },
            new LoginHistory { Id = 3, UserId = 2, AttemptedEmail = "hacker@test.com", IpAddress = "192.168.1.100", Result = LoginHistoryResultConstants.Failed, OccurredAt = now.AddMinutes(-10) },
            new LoginHistory { Id = 4, UserId = 2, AttemptedEmail = "hacker@test.com", IpAddress = "192.168.1.100", Result = LoginHistoryResultConstants.Failed, OccurredAt = now.AddMinutes(-9) },
            new LoginHistory { Id = 5, UserId = 2, AttemptedEmail = "hacker@test.com", IpAddress = "192.168.1.100", Result = LoginHistoryResultConstants.Failed, OccurredAt = now.AddMinutes(-8) }
        );
        await db.SaveChangesAsync();

        // Act
        var report = await adminService.GetLoginHistoriesAsync(new AdminLoginHistoryQueryParameters
        {
            PageNumber = 1,
            PageSize = 10
        });

        // Assert
        Assert.NotNull(report);
        Assert.Equal(5, report.Summary.TotalAttempts);
        Assert.Equal(1, report.Summary.SucceededAttempts);
        Assert.Equal(3, report.Summary.FailedAttempts);
        Assert.Equal(1, report.Summary.LockedAttempts);
        Assert.True(report.Summary.SuspiciousAttemptsCount >= 1);

        // Verify locked login is flagged suspicious
        var lockedItem = report.Histories.Items.FirstOrDefault(i => i.Id == 2);
        Assert.NotNull(lockedItem);
        Assert.True(lockedItem.IsSuspicious);
        Assert.Equal(AdminConstants.SuspiciousLockedAttemptReason, lockedItem.SuspiciousReason);
    }

    [Fact]
    public async Task GetDashboardOverviewAsync_CalculatesRevenueAndOccupancyMetrics()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var adminService = new AdminManagementService(db, NullLogger<AdminManagementService>.Instance);

        var facility = new Facility { Id = 1, Code = "FAC-01", Name = "Facility 1", AddressLine = "Address", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var unitType = new UnitType { Id = 1, Code = "UT-1", Name = "Type 1", WidthM = 2, LengthM = 2, HeightM = 2, IsActive = true };
        db.Facilities.Add(facility);
        db.UnitTypes.Add(unitType);

        // 4 units: 2 occupied, 1 reserved, 1 available -> 75% occupancy
        db.StorageUnits.AddRange(
            new StorageUnit { Id = 1, FacilityId = 1, UnitTypeId = 1, UnitCode = "U1", PhysicalStatus = StorageUnitStatusConstants.Occupied, IsListed = true },
            new StorageUnit { Id = 2, FacilityId = 1, UnitTypeId = 1, UnitCode = "U2", PhysicalStatus = StorageUnitStatusConstants.Occupied, IsListed = true },
            new StorageUnit { Id = 3, FacilityId = 1, UnitTypeId = 1, UnitCode = "U3", PhysicalStatus = StorageUnitStatusConstants.Reserved, IsListed = true },
            new StorageUnit { Id = 4, FacilityId = 1, UnitTypeId = 1, UnitCode = "U4", PhysicalStatus = StorageUnitStatusConstants.Available, IsListed = true }
        );

        // Payments
        db.Payments.AddRange(
            new Payment { Id = 1, CustomerId = 1, TargetInvoiceId = 1, Amount = 500000m, Currency = "VND", Method = "bank_transfer", Provider = "SePay", IdempotencyKey = "key1", Metadata = "{}", Status = PaymentConstants.StatusSucceeded, PaidAt = DateTimeOffset.UtcNow },
            new Payment { Id = 2, CustomerId = 1, TargetInvoiceId = 1, Amount = 300000m, Currency = "VND", Method = "bank_transfer", Provider = "SePay", IdempotencyKey = "key2", Metadata = "{}", Status = PaymentConstants.StatusSucceeded, PaidAt = DateTimeOffset.UtcNow },
            new Payment { Id = 3, CustomerId = 1, TargetInvoiceId = 2, Amount = 100000m, Currency = "VND", Method = "bank_transfer", Provider = "SePay", IdempotencyKey = "key3", Metadata = "{}", Status = PaymentConstants.StatusFailed, PaidAt = null }
        );

        // Invoices
        db.Invoices.AddRange(
            new Invoice { Id = 1, CustomerId = 1, InvoiceNo = "INV-01", Currency = "VND", SubtotalAmount = 500000m, TotalAmount = 500000m, Status = InvoiceStatusConstants.Paid, IssueDate = DateOnly.FromDateTime(DateTime.UtcNow), DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)) },
            new Invoice { Id = 2, CustomerId = 1, InvoiceNo = "INV-02", Currency = "VND", SubtotalAmount = 300000m, TotalAmount = 300000m, Status = InvoiceStatusConstants.Overdue, IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)), DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)) }
        );

        await db.SaveChangesAsync();

        // Act
        var overview = await adminService.GetDashboardOverviewAsync();

        // Assert
        Assert.NotNull(overview);
        Assert.Equal(800000m, overview.RevenueKpi.TotalRevenue);
        Assert.Equal(2, overview.RevenueKpi.TotalInvoices);
        Assert.Equal(1, overview.RevenueKpi.PaidInvoices);
        Assert.Equal(1, overview.RevenueKpi.OverdueInvoices);

        Assert.Equal(1, overview.OccupancyKpi.TotalFacilities);
        Assert.Equal(4, overview.OccupancyKpi.TotalStorageUnits);
        Assert.Equal(2, overview.OccupancyKpi.OccupiedUnits);
        Assert.Equal(1, overview.OccupancyKpi.ReservedUnits);
        Assert.Equal(1, overview.OccupancyKpi.AvailableUnits);
        Assert.Equal(50.0, overview.OccupancyKpi.SystemOccupancyRate);

        Assert.Single(overview.FacilityBreakdown);
        Assert.Equal(50.0, overview.FacilityBreakdown[0].OccupancyRate);
    }

    [Fact]
    public async Task CreateInternalUserAsync_DuplicateEmployeeCode_ThrowsConflictException()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var adminService = new AdminManagementService(db, NullLogger<AdminManagementService>.Instance);

        var staffRole = new Role { Id = 1, Code = RoleConstants.FacilityStaff, DisplayName = "Staff" };
        db.Roles.Add(staffRole);
        var existingUser = new User { Id = 1, Email = "staff1@storage.local", Status = UserStatusConstants.Active, PasswordHash = "hash" };
        var existingEmp = new EmployeeProfile { UserId = 1, EmployeeCode = "EMP-999", FullName = "Existing", HireDate = new DateOnly(2025, 1, 1), EmploymentStatus = "active" };
        db.Users.Add(existingUser);
        db.EmployeeProfiles.Add(existingEmp);
        await db.SaveChangesAsync();

        var request = new AdminCreateUserRequest
        {
            Email = "staff2@storage.local",
            Password = "Password123!",
            FullName = "New Staff",
            Role = RoleConstants.FacilityStaff,
            EmployeeCode = "EMP-999" // Colliding code
        };

        // Act & Assert
        await Assert.ThrowsAsync<AppConflictException>(() =>
            adminService.CreateInternalUserAsync(request, adminUserId: 99));
    }

    [Fact]
    public async Task CreateInternalUserAsync_NonStaffRoleWithInitialFacility_ThrowsValidationException()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var adminService = new AdminManagementService(db, NullLogger<AdminManagementService>.Instance);

        var adminRole = new Role { Id = 1, Code = RoleConstants.SystemAdministrator, DisplayName = "Admin" };
        var facility = new Facility { Id = 1, Code = "FAC-01", Name = "Facility", AddressLine = "Address", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        db.Roles.Add(adminRole);
        db.Facilities.Add(facility);
        await db.SaveChangesAsync();

        var request = new AdminCreateUserRequest
        {
            Email = "newadmin@storage.local",
            Password = "Password123!",
            FullName = "New Admin",
            Role = RoleConstants.SystemAdministrator,
            InitialFacilityId = 1 // Not allowed for Admin
        };

        // Act & Assert
        await Assert.ThrowsAsync<AppValidationException>(() =>
            adminService.CreateInternalUserAsync(request, adminUserId: 99));
    }

    [Fact]
    public async Task AssignStaffToFacility_TerminatedEmployee_ThrowsValidationException()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var adminService = new AdminManagementService(db, NullLogger<AdminManagementService>.Instance);

        var user = new User { Id = 5, Email = "terminated@storage.local", Status = UserStatusConstants.Active, PasswordHash = "hash" };
        var employee = new EmployeeProfile { UserId = 5, EmployeeCode = "EMP-005", FullName = "Terminated Staff", HireDate = new DateOnly(2025, 1, 1), EmploymentStatus = EmploymentStatusConstants.Terminated };
        var facility = new Facility { Id = 1, Code = "FAC-01", Name = "Facility", AddressLine = "Address", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        db.Users.Add(user);
        db.EmployeeProfiles.Add(employee);
        db.Facilities.Add(facility);
        await db.SaveChangesAsync();

        var request = new AdminCreateStaffAssignmentRequest
        {
            EmployeeId = 5,
            FacilityId = 1,
            AssignmentRole = StaffAssignmentRoles.FacilityStaff
        };

        // Act & Assert
        await Assert.ThrowsAsync<AppValidationException>(() =>
            adminService.AssignStaffToFacilityAsync(request, adminUserId: 99));
    }

    [Fact]
    public async Task UpdateUserRolesAsync_PromoteToStaff_AutoInitializesEmployeeProfile()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var adminService = new AdminManagementService(db, NullLogger<AdminManagementService>.Instance);

        var customerRole = new Role { Id = 1, Code = RoleConstants.Customer, DisplayName = "Customer" };
        var staffRole = new Role { Id = 2, Code = RoleConstants.FacilityStaff, DisplayName = "Staff" };
        db.Roles.AddRange(customerRole, staffRole);

        var user = new User { Id = 20, Email = "customer20@storage.local", Status = UserStatusConstants.Active, PasswordHash = "hash" };
        user.CustomerProfile = new CustomerProfile { UserId = 20, FullName = "Customer Promoted" };
        user.UserRoleUsers.Add(new UserRole { UserId = 20, RoleId = 1, Role = customerRole });
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = new AdminUpdateUserRolesRequest
        {
            Roles = new List<string> { RoleConstants.FacilityStaff }
        };

        // Act
        var resultRoles = await adminService.UpdateUserRolesAsync(id: 20, request, adminUserId: 1);

        // Assert
        Assert.Single(resultRoles);
        Assert.Equal(RoleConstants.FacilityStaff, resultRoles[0]);

        var updatedUser = await db.Users.Include(u => u.EmployeeProfile).FirstOrDefaultAsync(u => u.Id == 20);
        Assert.NotNull(updatedUser!.EmployeeProfile);
        Assert.Equal("Customer Promoted", updatedUser.EmployeeProfile.FullName);
        Assert.StartsWith("EMP-", updatedUser.EmployeeProfile.EmployeeCode);
    }

    [Fact]
    public async Task GetFacilitiesAsync_WithFiltersAndPaging_ReturnsFilteredSummaryWithStats()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var adminService = new AdminManagementService(db, NullLogger<AdminManagementService>.Instance);

        var f1 = new Facility { Id = 1, Code = "FAC-HN", Name = "Facility Hanoi", AddressLine = "123 Tran Hung Dao", City = "Ha Noi", Timezone = "Asia/Ho_Chi_Minh", Status = FacilityStatusConstants.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var f2 = new Facility { Id = 2, Code = "FAC-SG", Name = "Facility Saigon", AddressLine = "456 Le Loi", City = "Ho Chi Minh", Timezone = "Asia/Ho_Chi_Minh", Status = FacilityStatusConstants.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.Facilities.AddRange(f1, f2);

        // Add units to f2
        db.StorageUnits.AddRange(
            new StorageUnit { Id = 1, FacilityId = 2, UnitCode = "SG-U1", PhysicalStatus = StorageUnitStatusConstants.Available },
            new StorageUnit { Id = 2, FacilityId = 2, UnitCode = "SG-U2", PhysicalStatus = StorageUnitStatusConstants.Occupied }
        );

        // Add staff assignment to f2
        var user = new User { Id = 101, Email = "staff@storage.local", Status = UserStatusConstants.Active, PasswordHash = "hash" };
        var emp = new EmployeeProfile { UserId = 101, EmployeeCode = "EMP-101", FullName = "Staff SG", HireDate = new DateOnly(2025, 1, 1), EmploymentStatus = "active" };
        db.Users.Add(user);
        db.EmployeeProfiles.Add(emp);
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment
        {
            Id = 1,
            EmployeeId = 101,
            FacilityId = 2,
            AssignmentRole = StaffAssignmentRoles.FacilityStaff,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-5),
            EndsAt = null
        });

        await db.SaveChangesAsync();

        // Act - Search by city
        var result = await adminService.GetFacilitiesAsync(new AdminFacilityQueryParameters
        {
            City = "Ho Chi Minh",
            PageNumber = 1,
            PageSize = 10
        });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal("FAC-SG", item.Code);
        Assert.Equal(2, item.TotalUnits);
        Assert.Equal(1, item.ActiveStaffCount);
    }

    [Fact]
    public async Task GetFacilityDetailAsync_ReturnsDetailWithUnitStatsAndAssignedStaff()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var adminService = new AdminManagementService(db, NullLogger<AdminManagementService>.Instance);

        var facility = new Facility
        {
            Id = 10,
            Code = "FAC-DN",
            Name = "Facility Da Nang",
            AddressLine = "789 Bach Dang",
            City = "Da Nang",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = FacilityStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Facilities.Add(facility);

        db.StorageUnits.AddRange(
            new StorageUnit { Id = 11, FacilityId = 10, UnitCode = "DN-01", PhysicalStatus = StorageUnitStatusConstants.Available },
            new StorageUnit { Id = 12, FacilityId = 10, UnitCode = "DN-02", PhysicalStatus = StorageUnitStatusConstants.Occupied },
            new StorageUnit { Id = 13, FacilityId = 10, UnitCode = "DN-03", PhysicalStatus = StorageUnitStatusConstants.Reserved },
            new StorageUnit { Id = 14, FacilityId = 10, UnitCode = "DN-04", PhysicalStatus = StorageUnitStatusConstants.Maintenance }
        );

        var staffUser = new User { Id = 201, Email = "danang@storage.local", Status = UserStatusConstants.Active, PasswordHash = "hash" };
        var empProfile = new EmployeeProfile { UserId = 201, EmployeeCode = "EMP-DN1", FullName = "DN Staff", HireDate = new DateOnly(2025, 1, 1), EmploymentStatus = "active" };
        db.Users.Add(staffUser);
        db.EmployeeProfiles.Add(empProfile);

        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment
        {
            Id = 5,
            EmployeeId = 201,
            FacilityId = 10,
            AssignmentRole = StaffAssignmentRoles.FacilityStaff,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        await db.SaveChangesAsync();

        // Act
        var detail = await adminService.GetFacilityDetailAsync(10);

        // Assert
        Assert.NotNull(detail);
        Assert.Equal("FAC-DN", detail.Code);
        Assert.Equal(4, detail.UnitStats.TotalUnits);
        Assert.Equal(1, detail.UnitStats.Available);
        Assert.Equal(1, detail.UnitStats.Occupied);
        Assert.Equal(1, detail.UnitStats.Reserved);
        Assert.Equal(1, detail.UnitStats.Maintenance);
        Assert.Equal(25.0, detail.UnitStats.OccupancyRate);
        Assert.Single(detail.AssignedStaff);
        Assert.Equal("DN Staff", detail.AssignedStaff[0].EmployeeName);
    }

    [Fact]
    public async Task CreateFacilityAsync_ValidRequest_CreatesFacilityAndRecordsAuditLog()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var adminService = new AdminManagementService(db, NullLogger<AdminManagementService>.Instance);

        var request = new AdminCreateFacilityRequest
        {
            Code = "FAC-NEW",
            Name = "New Branch",
            AddressLine = "12 New St",
            City = "Can Tho",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = FacilityStatusConstants.Active,
            Latitude = 10.035123m,
            Longitude = 105.778123m,
            OpeningTime = new TimeOnly(8, 0),
            ClosingTime = new TimeOnly(20, 0)
        };

        // Act
        var created = await adminService.CreateFacilityAsync(request, adminUserId: 999);

        // Assert
        Assert.NotNull(created);
        Assert.Equal("FAC-NEW", created.Code);
        Assert.Equal("New Branch", created.Name);

        var auditLog = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityType == AuditEntityTypeConstants.Facility);
        Assert.NotNull(auditLog);
        Assert.Equal(AuditActionConstants.Create, auditLog.Action);
        Assert.Equal(999, auditLog.ActorUserId);
    }

    [Fact]
    public async Task CreateFacilityAsync_DuplicateCode_ThrowsConflictException()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var adminService = new AdminManagementService(db, NullLogger<AdminManagementService>.Instance);

        db.Facilities.Add(new Facility
        {
            Id = 1,
            Code = "FAC-DUP",
            Name = "Original Facility",
            AddressLine = "Street 1",
            City = "HCM",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = FacilityStatusConstants.Active
        });
        await db.SaveChangesAsync();

        var request = new AdminCreateFacilityRequest
        {
            Code = "fac-dup", // Same code, different case
            Name = "Duplicate Facility",
            AddressLine = "Street 2",
            City = "HCM",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = FacilityStatusConstants.Active
        };

        // Act & Assert
        await Assert.ThrowsAsync<AppConflictException>(() =>
            adminService.CreateFacilityAsync(request, adminUserId: 999));
    }

    [Fact]
    public async Task UpdateFacilityStatusAsync_ValidStatus_UpdatesStatusAndRecordsAuditLog()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var adminService = new AdminManagementService(db, NullLogger<AdminManagementService>.Instance);

        db.Facilities.Add(new Facility
        {
            Id = 5,
            Code = "FAC-05",
            Name = "Facility 05",
            AddressLine = "Street 5",
            City = "HCM",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = FacilityStatusConstants.Active
        });
        await db.SaveChangesAsync();

        var request = new AdminUpdateFacilityStatusRequest
        {
            Status = FacilityStatusConstants.Maintenance,
            Reason = "Renovation work"
        };

        // Act
        var updated = await adminService.UpdateFacilityStatusAsync(5, request, adminUserId: 999);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal(FacilityStatusConstants.Maintenance, updated.Status);

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityType == AuditEntityTypeConstants.Facility && a.Action == AuditActionConstants.UpdateStatus);
        Assert.NotNull(audit);
        Assert.Equal(FacilityStatusConstants.Active, audit.OldValues);
        Assert.Contains("maintenance", audit.NewValues);
    }
}

