using Microsoft.EntityFrameworkCore;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;
using Xunit;

namespace SelfStorageSystem.Tests;

public class CapabilitiesServiceTests
{
    private static SelfStorageDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<SelfStorageDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SelfStorageDbContext(options);
    }

    [Fact]
    public async Task GetUserCapabilities_FacilityManager_ReturnsMergedCapabilities()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var managerRole = new Role { Id = 3, Code = RoleConstants.FacilityManager, DisplayName = "Facility Manager" };
        var user = new User
        {
            Id = 10,
            Email = "manager@storage.local",
            PasswordHash = "hash",
            Status = UserStatusConstants.Active
        };
        db.Roles.Add(managerRole);
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = 10, RoleId = 3, Role = managerRole, User = user });
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment
        {
            EmployeeId = 10,
            FacilityId = 1,
            AssignmentRole = StaffAssignmentRoles.FacilityManager,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-10)
        });
        await db.SaveChangesAsync();

        var service = new CapabilitiesService(db);

        // Act
        var result = await service.GetUserCapabilitiesAsync(10);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.UserId);
        Assert.Contains(RoleConstants.FacilityManager, result.Roles);
        Assert.Contains("pricing:rates_manage", result.Permissions);
        Assert.Contains("promotions:manage", result.Permissions);
        Assert.Contains("pricing:fees_view", result.Permissions);
        Assert.Contains("facility:dashboard", result.Permissions);
        Assert.Contains("shift:view", result.Permissions);
        Assert.NotNull(result.AssignedFacilityIds);
        Assert.Contains(1L, result.AssignedFacilityIds);
    }

    [Fact]
    public async Task GetUserCapabilities_FacilityStaff_ReturnsStaffCapabilitiesIncludingShiftView()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffRole = new Role { Id = 2, Code = RoleConstants.FacilityStaff, DisplayName = "Facility Staff" };
        var user = new User
        {
            Id = 15,
            Email = "staff@storage.local",
            PasswordHash = "hash",
            Status = UserStatusConstants.Active
        };
        db.Roles.Add(staffRole);
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = 15, RoleId = 2, Role = staffRole, User = user });
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment
        {
            EmployeeId = 15,
            FacilityId = 1,
            AssignmentRole = StaffAssignmentRoles.FacilityStaff,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-5)
        });
        await db.SaveChangesAsync();

        var service = new CapabilitiesService(db);

        // Act
        var result = await service.GetUserCapabilitiesAsync(15);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(15, result.UserId);
        Assert.Contains(RoleConstants.FacilityStaff, result.Roles);
        Assert.Contains("shift:view", result.Permissions);
        Assert.Contains("handover:check", result.Permissions);
        Assert.Contains("qr:verify", result.Permissions);
        Assert.DoesNotContain("pricing:rates_manage", result.Permissions);
    }

    [Fact]
    public async Task GetUserCapabilities_Customer_ReturnsCustomerCapabilities()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var customerRole = new Role { Id = 1, Code = RoleConstants.Customer, DisplayName = "Customer" };
        var user = new User
        {
            Id = 20,
            Email = "customer@storage.local",
            PasswordHash = "hash",
            Status = UserStatusConstants.Active
        };
        db.Roles.Add(customerRole);
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = 20, RoleId = 1, Role = customerRole, User = user });
        await db.SaveChangesAsync();

        var service = new CapabilitiesService(db);

        // Act
        var result = await service.GetUserCapabilitiesAsync(20);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(RoleConstants.Customer, result.Roles);
        Assert.Contains("catalog:view", result.Permissions);
        Assert.Contains("reservation:create", result.Permissions);
        Assert.Contains("unit:transfer", result.Permissions);
        Assert.DoesNotContain("pricing:rates_manage", result.Permissions);
    }

    [Fact]
    public async Task GetUserCapabilities_UserNotFound_ThrowsNotFound()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var service = new CapabilitiesService(db);

        // Act & Assert
        await Assert.ThrowsAsync<AppNotFoundException>(() => service.GetUserCapabilitiesAsync(999));
    }
}
