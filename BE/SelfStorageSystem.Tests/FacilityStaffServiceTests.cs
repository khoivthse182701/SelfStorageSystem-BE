using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SelfStorageSystem.Contracts.Staff;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;
using Xunit;

namespace SelfStorageSystem.Tests;

public class FacilityStaffServiceTests
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
    public async Task CreateHandover_Success_ConvertsReservationAndActivatesAgreement()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();

        var employeeUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var employee = new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff Nguyen", HireDate = new DateOnly(2025, 1, 1), EmploymentStatus = "active" };
        var customerUser = new User { Id = 10, Email = "customer@system.local", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Tran Customer", IdentityNumber = "123456789" };

        var facility = new Facility { Id = 1, Code = "FAC-01", Name = "District 7 Facility", AddressLine = "123 Street", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var unitType = new UnitType { Id = 1, Code = "SM", Name = "Small Locker", WidthM = 1, LengthM = 1, HeightM = 1, IsActive = true };
        var unit = new StorageUnit { Id = 101, FacilityId = 1, UnitTypeId = 1, UnitCode = "U-101", PhysicalStatus = StorageUnitStatusConstants.Reserved, IsListed = true };

        var reservation = new Reservation
        {
            Id = 50,
            ReservationCode = "RSV-5050",
            CustomerId = 10,
            FacilityId = 1,
            UnitTypeId = 1,
            FacilityRateId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            MonthlyRateSnapshot = 1000000m,
            DepositSnapshot = 1000000m,
            BookingFeeSnapshot = 50000m,
            DiscountSnapshot = 0m,
            QuotedTotal = 1050000m,
            HoldUntil = DateTimeOffset.UtcNow.AddHours(2),
            Status = ReservationStatusConstants.Confirmed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var allocation = new UnitAllocation
        {
            Id = 1,
            StorageUnitId = 101,
            ReservationId = 50,
            AllocationKind = AllocationKindConstants.ReservationHold,
            AllocationStartDate = reservation.StartDate,
            AllocationEndDate = reservation.EndDate,
            Status = AllocationStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Users.AddRange(employeeUser, customerUser);
        db.EmployeeProfiles.Add(employee);
        db.CustomerProfiles.Add(customer);
        db.Facilities.Add(facility);
        db.UnitTypes.Add(unitType);
        db.StorageUnits.Add(unit);
        db.Reservations.Add(reservation);
        db.UnitAllocations.Add(allocation);
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);
        var request = new CreateStaffHandoverRequest(
            ReservationId: 50,
            HandoverType: HandoverTypeConstants.CheckIn,
            CustomerSignatureRef: "sig-cust-123",
            StaffSignatureRef: "sig-staff-456",
            Notes: "Smooth checkin handover",
            OverallCondition: "Clean and ready",
            InspectionSummary: "Passed inspection",
            InspectionItems: new List<CreateHandoverInspectionItemRequest>
            {
                new("Smart door lock", "Good", null, null, 0m)
            }
        );

        // Act
        var result = await service.CreateHandoverAsync(employeeUserId: 3, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HandoverTypeConstants.CheckIn, result.HandoverType);
        Assert.Equal(RentalAgreementStatusConstants.Active, result.AgreementStatus);

        var updatedReservation = await db.Reservations.FindAsync(50L);
        Assert.Equal(ReservationStatusConstants.Converted, updatedReservation!.Status);

        var updatedUnit = await db.StorageUnits.FindAsync(101L);
        Assert.Equal(StorageUnitStatusConstants.Occupied, updatedUnit!.PhysicalStatus);

        var handover = await db.HandoverRecords.FindAsync(result.HandoverId);
        Assert.NotNull(handover);

        var inspection = await db.Inspections.FindAsync(handover.InspectionId);
        Assert.NotNull(inspection);
        Assert.Equal(InspectionStatusConstants.Completed, inspection.Status);
        Assert.Equal(OverallConditionConstants.Good, inspection.OverallCondition);
    }

    [Fact]
    public async Task InspectMoveOut_Success_TerminatesAgreementAndFreesUnit()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();

        var employee = new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff Nguyen", HireDate = new DateOnly(2025, 1, 1), EmploymentStatus = "active" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Tran Customer", IdentityNumber = "123456789" };

        var agreement = new RentalAgreement
        {
            Id = 200,
            AgreementNo = "AGR-200",
            CustomerId = 10,
            FacilityId = 1,
            ReservationId = 1,
            PolicyVersionId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MonthlyRateSnapshot = 1000000m,
            DepositSnapshot = 1000000m,
            DepositBalance = 1000000m,
            Status = RentalAgreementStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var unit = new StorageUnit { Id = 202, FacilityId = 1, UnitTypeId = 1, UnitCode = "U-202", PhysicalStatus = StorageUnitStatusConstants.Occupied, IsListed = true };

        var allocation = new UnitAllocation
        {
            Id = 2,
            AgreementId = 200,
            StorageUnitId = 202,
            AllocationKind = AllocationKindConstants.Rental,
            AllocationStartDate = agreement.StartDate,
            AllocationEndDate = agreement.EndDate,
            Status = AllocationStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var moveOut = new MoveOutRequest
        {
            Id = 10,
            AgreementId = 200,
            RequestedBy = 10,
            RequestedMoveOutDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = MoveOutStatusConstants.Requested,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.EmployeeProfiles.Add(employee);
        db.CustomerProfiles.Add(customer);
        db.RentalAgreements.Add(agreement);
        db.StorageUnits.Add(unit);
        db.UnitAllocations.Add(allocation);
        db.MoveOutRequests.Add(moveOut);
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);
        var request = new InspectMoveOutRequest(
            OverallCondition: "Minor wall scratch",
            Summary: "Deduct 100k repair fee",
            InspectionItems: new List<CreateHandoverInspectionItemRequest>
            {
                new("Wall", "Scratched", "Cleaning fee", null, 100000m)
            },
            NextUnitStatus: StorageUnitStatusConstants.Available
        );

        // Act
        var result = await service.InspectMoveOutAsync(employeeUserId: 3, moveOutId: 10, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(100000m, result.TotalDamageCharges);
        Assert.Equal(RentalAgreementStatusConstants.Terminated, result.AgreementStatus);
        Assert.Equal(StorageUnitStatusConstants.Available, result.UnitStatus);

        var updatedAgreement = await db.RentalAgreements.FindAsync(200L);
        Assert.Equal(900000m, updatedAgreement!.DepositBalance);

        var updatedMoveOut = await db.MoveOutRequests.FindAsync(10L);
        Assert.Equal(MoveOutStatusConstants.Completed, updatedMoveOut!.Status);
    }

    [Fact]
    public async Task ResolveTicket_Success_MarksTicketAsResolved()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();

        var employee = new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff Nguyen", HireDate = new DateOnly(2025, 1, 1), EmploymentStatus = "active" };
        var ticket = new SupportTicket
        {
            Id = 77,
            TicketNo = "TCK-20261004-TEST",
            CustomerId = 10,
            FacilityId = 1,
            Category = TicketCategoryConstants.Access,
            Priority = TicketPriorityConstants.High,
            Subject = "PIN code not working",
            Description = "Door locked",
            Status = TicketStatusConstants.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.EmployeeProfiles.Add(employee);
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);
        var request = new ResolveTicketRequest("Resynced PIN on keypad controller.");

        // Act
        await service.ResolveTicketAsync(employeeUserId: 3, ticketId: 77, request);

        // Assert
        var updated = await db.SupportTickets.FindAsync(77L);
        Assert.Equal(TicketStatusConstants.Resolved, updated!.Status);
        Assert.Equal("Resynced PIN on keypad controller.", updated.Resolution);
        Assert.NotNull(updated.ResolvedAt);
    }
}
