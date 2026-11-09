using SelfStorageSystem.Domain.Common;
using SelfStorageSystem.Domain.Errors;
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
    public async Task GetUnits_Success_ReturnsUnitsWithCurrentRate()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var employee = new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff Nguyen", EmploymentStatus = "active" };
        var facility = new Facility { Id = 1, Code = "FAC-01", Name = "Thu Duc Facility", AddressLine = "123 Street", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var unitType = new UnitType { Id = 1, Code = "SM", Name = "Small Locker", WidthM = 1, LengthM = 1, HeightM = 1, AreaM2 = 1, IsActive = true };
        var unit = new StorageUnit { Id = 101, FacilityId = 1, UnitTypeId = 1, UnitCode = "U-101", PhysicalStatus = StorageUnitStatusConstants.Available, IsListed = true };

        var rate = new FacilityRate
        {
            Id = 1,
            FacilityId = 1,
            UnitTypeId = 1,
            MonthlyRate = 1200000m,
            DepositAmount = 1200000m,
            BookingFee = 0,
            ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1))
        };

        db.Users.Add(staffUser);
        db.EmployeeProfiles.Add(employee);
        db.Facilities.Add(facility);
        db.UnitTypes.Add(unitType);
        db.StorageUnits.Add(unit);
        db.FacilityRates.Add(rate);
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment { Id = 101, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-10) });
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);

        // Act
        var result = await service.GetUnitsAsync(employeeUserId: 3, facilityId: 1);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("U-101", result[0].UnitCode);
        Assert.Equal(1200000m, result[0].CurrentRate);
        Assert.Equal(StorageUnitStatusConstants.Available, result[0].PhysicalStatus);
    }

    [Fact]
    public async Task WalkInBooking_Disabled_ThrowsNotSupportedException()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var service = new FacilityStaffService(db);
        var request = new StaffWalkInBookingRequest(
            CustomerPhone: "0901234567",
            CustomerName: "Nguyen Van A",
            CustomerEmail: "nguyenvana@gmail.com",
            CustomerIdentityNumber: "079123456789",
            UnitId: 101,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow),
            DurationMonths: 3
        );

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() => service.CreateWalkInBookingAsync(employeeUserId: 3, request));
    }

    [Fact]
    public async Task QrVerify_CheckInReservation_ReturnsCorrectActionAndTitle()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var customerUser = new User { Id = 10, Email = "cust@system.local", PasswordHash = "hash", Status = "active", PhoneNumber = "0911223344" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Le Thi B" };
        var facility = new Facility { Id = 1, Code = "FAC-HN", Name = "Ha Noi Facility", AddressLine = "Ha Noi", City = "HN", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var unitType = new UnitType { Id = 1, Code = "SM", Name = "Small Locker", WidthM = 1, LengthM = 1, HeightM = 1, IsActive = true };
        var unit = new StorageUnit { Id = 101, FacilityId = 1, UnitTypeId = 1, UnitCode = "HN-101", PhysicalStatus = StorageUnitStatusConstants.Reserved, IsListed = true };

        var reservation = new Reservation
        {
            Id = 50,
            ReservationCode = "RSV-9999",
            CustomerId = 10,
            FacilityId = 1,
            UnitTypeId = 1,
            FacilityRateId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            MonthlyRateSnapshot = 1000000m,
            DepositSnapshot = 1000000m,
            QuotedTotal = 2000000m,
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
            AllocationKind = AllocationKindConstants.Reservation,
            AllocationStartDate = reservation.StartDate,
            AllocationEndDate = reservation.EndDate,
            Status = AllocationStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var employee = new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff Nguyen", EmploymentStatus = "active" };
        db.Users.AddRange(staffUser, customerUser);
        db.EmployeeProfiles.Add(employee);
        db.CustomerProfiles.Add(customer);
        db.Facilities.Add(facility);
        db.UnitTypes.Add(unitType);
        db.StorageUnits.Add(unit);
        db.Reservations.Add(reservation);
        db.UnitAllocations.Add(allocation);
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment { Id = 102, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-10) });
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);

        // Act
        var result = await service.VerifyQrAsync(employeeUserId: 3, new StaffQrVerifyRequest("CHECKIN:RSV-9999:10:1"));

        // Assert
        Assert.NotNull(result);
        Assert.Equal("check_in", result.ActionType);
        Assert.Contains("Le Thi B", result.Title);
        Assert.Contains("Ha Noi Facility", result.Title);
        Assert.Contains("confirm_checkin", result.AllowedActions);
    }

    [Fact]
    public async Task QrConfirmAction_CheckIn_ActivatesAgreementAndOccupiesUnit()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var customerUser = new User { Id = 10, Email = "cust@system.local", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Le Thi B" };
        var facility = new Facility { Id = 1, Code = "FAC-HN", Name = "Ha Noi Facility", AddressLine = "Ha Noi", City = "HN", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var unitType = new UnitType { Id = 1, Code = "SM", Name = "Small Locker", WidthM = 1, LengthM = 1, HeightM = 1, IsActive = true };
        var unit = new StorageUnit { Id = 101, FacilityId = 1, UnitTypeId = 1, UnitCode = "HN-101", PhysicalStatus = StorageUnitStatusConstants.Reserved, IsListed = true };

        var reservation = new Reservation
        {
            Id = 50,
            ReservationCode = "RSV-9999",
            CustomerId = 10,
            FacilityId = 1,
            UnitTypeId = 1,
            FacilityRateId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            MonthlyRateSnapshot = 1000000m,
            DepositSnapshot = 1000000m,
            QuotedTotal = 2000000m,
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
            AllocationKind = AllocationKindConstants.Reservation,
            AllocationStartDate = reservation.StartDate,
            AllocationEndDate = reservation.EndDate,
            Status = AllocationStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var employee = new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff Nguyen", EmploymentStatus = "active" };
        db.Users.AddRange(staffUser, customerUser);
        db.EmployeeProfiles.Add(employee);
        db.CustomerProfiles.Add(customer);
        db.Facilities.Add(facility);
        db.UnitTypes.Add(unitType);
        db.StorageUnits.Add(unit);
        db.Reservations.Add(reservation);
        db.UnitAllocations.Add(allocation);
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment { Id = 102, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-10) });
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);

        // Act
        var result = await service.ConfirmQrActionAsync(employeeUserId: 3, new StaffConfirmQrActionRequest("check_in", ReservationId: 50));

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("check_in", result.ActionType);
        Assert.Equal(RentalAgreementStatusConstants.Active, result.AgreementStatus);
        Assert.Equal(StorageUnitStatusConstants.Occupied, result.UnitStatus);

        var updatedUnit = await db.StorageUnits.FindAsync(101L);
        Assert.Equal(StorageUnitStatusConstants.Occupied, updatedUnit!.PhysicalStatus);

        var updatedReservation = await db.Reservations.FindAsync(50L);
        Assert.Equal(ReservationStatusConstants.Converted, updatedReservation!.Status);
    }

    [Fact]
    public async Task QrConfirmAction_TransferMoveOut_SetsMovedOutInTransitAndCreatesCleaningTicket()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var customerUser = new User { Id = 10, Email = "cust@system.local", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Tran C" };
        var fromFacility = new Facility { Id = 1, Code = "FAC-HCM", Name = "Ho Chi Minh Facility", AddressLine = "HCM", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var toFacility = new Facility { Id = 2, Code = "FAC-HN", Name = "Ha Noi Facility", AddressLine = "HN", City = "HN", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };

        var fromUnit = new StorageUnit { Id = 101, FacilityId = 1, UnitTypeId = 1, UnitCode = "HCM-101", PhysicalStatus = StorageUnitStatusConstants.Occupied, IsListed = true };
        var agreement = new RentalAgreement
        {
            Id = 80,
            AgreementNo = "AGR-HCM-80",
            CustomerId = 10,
            FacilityId = 1,
            PolicyVersionId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(2)),
            MonthlyRateSnapshot = 1000000m,
            DepositSnapshot = 1000000m,
            DepositBalance = 1000000m,
            Status = RentalAgreementStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var transfer = new UnitTransferRequest
        {
            Id = 99,
            AgreementId = 80,
            FromUnitId = 101,
            RequestedUnitTypeId = 1,
            RequestedFacilityId = 2,
            RequestedEffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MoveOutDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MoveInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            Reason = "Relocating to HN",
            Status = TransferRequestStatusConstants.Scheduled,
            RequestedBy = 10,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var employee = new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff Nguyen", EmploymentStatus = "active" };
        db.Users.AddRange(staffUser, customerUser);
        db.EmployeeProfiles.Add(employee);
        db.CustomerProfiles.Add(customer);
        db.Facilities.AddRange(fromFacility, toFacility);
        db.StorageUnits.Add(fromUnit);
        db.RentalAgreements.Add(agreement);
        db.UnitTransferRequests.Add(transfer);
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment { Id = 103, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-10) });
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);

        // Act
        var result = await service.ConfirmQrActionAsync(employeeUserId: 3, new StaffConfirmQrActionRequest("transfer_move_out", TransferRequestId: 99));

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("transfer_move_out", result.ActionType);

        var updatedTransfer = await db.UnitTransferRequests.FindAsync(99L);
        Assert.Equal(TransferRequestStatusConstants.MovedOutInTransit, updatedTransfer!.Status);

        // HCM unit set to Maintenance while cleaning ticket is open
        var updatedUnit = await db.StorageUnits.FindAsync(101L);
        Assert.Equal(StorageUnitStatusConstants.Maintenance, updatedUnit!.PhysicalStatus);

        // Cleaning ticket automatically created for HCM unit
        var cleaningTicket = await db.SupportTickets.FirstOrDefaultAsync(t => t.StorageUnitId == 101 && t.Category == "cleaning");
        Assert.NotNull(cleaningTicket);
        Assert.Equal(TicketStatusConstants.Open, cleaningTicket.Status);
    }

    [Fact]
    public async Task CompleteTicket_CleaningOrMaintenance_UnlocksUnitToAvailable()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var customerUser = new User { Id = 10, Email = "cust@system.local", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Tran C" };
        var facility = new Facility { Id = 1, Code = "FAC-HCM", Name = "Ho Chi Minh Facility", AddressLine = "HCM", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };

        var dirtyUnit = new StorageUnit
        {
            Id = 101,
            FacilityId = 1,
            UnitTypeId = 1,
            UnitCode = "HCM-101",
            PhysicalStatus = StorageUnitStatusConstants.Maintenance,
            IsListed = false
        };

        var ticket = new SupportTicket
        {
            Id = 555,
            TicketNo = "TCK-555",
            CustomerId = 10,
            FacilityId = 1,
            StorageUnitId = 101,
            Category = "cleaning",
            Priority = TicketPriorityConstants.Normal,
            Subject = "Dọn vệ sinh ô kho HCM-101",
            Description = "Cần dọn dẹp và khử trùng kho.",
            Status = TicketStatusConstants.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Users.AddRange(staffUser, customerUser);
        db.CustomerProfiles.Add(customer);
        db.Facilities.Add(facility);
        db.StorageUnits.Add(dirtyUnit);
        db.SupportTickets.Add(ticket);
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment { Id = 107, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-10) });
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment { Id = 105, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-10) });
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment { Id = 104, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-10) });
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);

        // Act
        var result = await service.CompleteTicketAsync(employeeUserId: 3, ticketId: 555, new StaffCompleteTicketRequest("Đã dọn dẹp và khử trùng xong"));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TicketStatusConstants.Resolved, result.Status);
        Assert.Equal(101L, result.UnlockedUnitId);
        Assert.Equal(StorageUnitStatusConstants.Available, result.UnlockedUnitStatus);

        // StorageUnit must be unlocked to Available and IsListed = true
        var updatedUnit = await db.StorageUnits.FindAsync(101L);
        Assert.Equal(StorageUnitStatusConstants.Available, updatedUnit!.PhysicalStatus);
        Assert.True(updatedUnit.IsListed);

        // Status history recorded
        var history = await db.UnitStatusHistories.FirstOrDefaultAsync(h => h.StorageUnitId == 101);
        Assert.NotNull(history);
        Assert.Equal(StorageUnitStatusConstants.Available, history.NewStatus);
    }

    [Fact]
    public async Task AssignTicketToStaff_Success_AssignsStaffAndSetsInProgress()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var employee = new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff Nguyen", EmploymentStatus = "active" };
        var customerUser = new User { Id = 10, Email = "cust@system.local", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Customer Test" };
        var facility = new Facility { Id = 1, Code = "FAC-01", Name = "Thu Duc Facility", AddressLine = "123 Street", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };

        var ticket = new SupportTicket
        {
            Id = 777,
            TicketNo = "TCK-777",
            CustomerId = 10,
            FacilityId = 1,
            Category = "maintenance",
            Priority = TicketPriorityConstants.High,
            Subject = "Đèn kho bị chập",
            Description = "Bóng đèn nhấp nháy liên tục",
            Status = TicketStatusConstants.Open,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Users.AddRange(staffUser, customerUser);
        db.EmployeeProfiles.Add(employee);
        db.CustomerProfiles.Add(customer);
        db.Facilities.Add(facility);
        db.SupportTickets.Add(ticket);
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment { Id = 107, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-10) });
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment { Id = 105, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-10) });
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);

        // Act
        await service.AssignTicketToStaffAsync(employeeUserId: 3, ticketId: 777);

        // Assert
        var updatedTicket = await db.SupportTickets.Include(t => t.TicketAssignment).FirstOrDefaultAsync(t => t.Id == 777);
        Assert.NotNull(updatedTicket);
        Assert.Equal(TicketStatusConstants.InProgress, updatedTicket.Status);
        Assert.NotNull(updatedTicket.TicketAssignment);
        Assert.Equal(3L, updatedTicket.TicketAssignment.EmployeeId);
    }

    [Fact]
    public async Task QrConfirmAction_CheckIn_WithZeroDefaultIds_HandlesCorrectly_ByAgreementId()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var customerUser = new User { Id = 10, Email = "cust@system.local", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Customer Test" };
        var facility = new Facility { Id = 1, Code = "FAC-01", Name = "Thu Duc Facility", AddressLine = "123 Street", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var unit = new StorageUnit { Id = 101, FacilityId = 1, UnitTypeId = 1, UnitCode = "TD-101", PhysicalStatus = StorageUnitStatusConstants.Available, IsListed = true };
        var agreement = new RentalAgreement
        {
            Id = 80,
            AgreementNo = "AGR-HCM-80",
            CustomerId = 10,
            FacilityId = 1,
            PolicyVersionId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(2)),
            MonthlyRateSnapshot = 1000000m,
            DepositSnapshot = 1000000m,
            DepositBalance = 1000000m,
            Status = RentalAgreementStatusConstants.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var allocation = new UnitAllocation
        {
            Id = 1,
            AgreementId = 80,
            StorageUnitId = 101,
            AllocationKind = AllocationKindConstants.Rental,
            AllocationStartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            AllocationEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            Status = AllocationStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var employee = new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff Nguyen", EmploymentStatus = "active" };
        db.Users.AddRange(staffUser, customerUser);
        db.EmployeeProfiles.Add(employee);
        db.CustomerProfiles.Add(customer);
        db.Facilities.Add(facility);
        db.StorageUnits.Add(unit);
        db.RentalAgreements.Add(agreement);
        db.UnitAllocations.Add(allocation);
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment { Id = 106, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-10) });
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);

        // FE sends 0 instead of null for omitted IDs
        var request = new StaffConfirmQrActionRequest(
            ActionType: "check_in",
            ReservationId: 0,
            AgreementId: 80,
            TransferRequestId: 0,
            MoveOutId: 0,
            UnitId: 0,
            Notes: "Checked in with QR"
        );

        // Act
        var result = await service.ConfirmQrActionAsync(employeeUserId: 3, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("check_in", result.ActionType);
        Assert.Equal(80L, result.AgreementId);
        Assert.Equal(RentalAgreementStatusConstants.Active, result.AgreementStatus);
        Assert.Equal(StorageUnitStatusConstants.Occupied, result.UnitStatus);

        var updatedUnit = await db.StorageUnits.FindAsync(101L);
        Assert.Equal(StorageUnitStatusConstants.Occupied, updatedUnit!.PhysicalStatus);

        var updatedAgreement = await db.RentalAgreements.FindAsync(80L);
        Assert.Equal(RentalAgreementStatusConstants.Active, updatedAgreement!.Status);
        Assert.NotNull(updatedAgreement.CheckedInAt);
    }

    [Fact]
    public async Task AddStaffTicketMessageAsync_WithValidTicket_AddsMessageSuccessfully()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var employee = new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff Nguyen", EmploymentStatus = "active" };
        var customerUser = new User { Id = 10, Email = "cust@system.local", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Customer Test" };
        var facility = new Facility { Id = 1, Code = "FAC-01", Name = "Thu Duc Facility", AddressLine = "123 Street", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };

        var ticket = new SupportTicket
        {
            Id = 888,
            TicketNo = "TCK-888",
            CustomerId = 10,
            FacilityId = 1,
            Category = "general",
            Priority = TicketPriorityConstants.Normal,
            Subject = "Test Ticket",
            Description = "Need assistance",
            Status = TicketStatusConstants.Open,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Users.AddRange(staffUser, customerUser);
        db.EmployeeProfiles.Add(employee);
        db.CustomerProfiles.Add(customer);
        db.Facilities.Add(facility);
        db.SupportTickets.Add(ticket);
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment { Id = 107, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-10) });
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);
        var request = new AddStaffTicketMessageRequest("Staff is investigating the issue", IsInternal: false);

        // Act
        var result = await service.AddStaffTicketMessageAsync(employeeUserId: 3, ticketId: 888, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(888L, result.TicketId);
        Assert.Equal("Staff is investigating the issue", result.Body);
        Assert.False(result.IsInternal);
        Assert.Equal("Staff Nguyen", result.AuthorName);

        var msg = await db.TicketMessages.FirstOrDefaultAsync(m => m.TicketId == 888);
        Assert.NotNull(msg);
        Assert.Equal("Staff is investigating the issue", msg.Body);
    }

    [Fact]
    public async Task UpdateUnitStatusAsync_WithValidStaffAndUnit_UpdatesPhysicalStatus()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var employee = new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff Nguyen", EmploymentStatus = "active" };
        var facility = new Facility { Id = 1, Code = "FAC-01", Name = "Thu Duc Facility", AddressLine = "123 Street", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var assignment = new StaffFacilityAssignment
        {
            Id = 1,
            EmployeeId = 3,
            FacilityId = 1,
            AssignmentRole = "staff",
            StartsAt = DateTimeOffset.UtcNow.AddDays(-10)
        };
        var unit = new StorageUnit { Id = 101, FacilityId = 1, UnitTypeId = 1, UnitCode = "TD-101", PhysicalStatus = StorageUnitStatusConstants.Available, IsListed = true };

        db.Users.Add(staffUser);
        db.EmployeeProfiles.Add(employee);
        db.Facilities.Add(facility);
        db.StaffFacilityAssignments.Add(assignment);
        db.StorageUnits.Add(unit);
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);
        var request = new UpdateUnitStatusRequest(PhysicalStatus: "under_maintenance", Reason: "Kẹt cửa cuốn");

        // Act
        await service.UpdateUnitStatusAsync(employeeUserId: 3, unitId: 101, request);

        // Assert
        var updatedUnit = await db.StorageUnits.FindAsync(101L);
        Assert.NotNull(updatedUnit);
        Assert.Equal("under_maintenance", updatedUnit.PhysicalStatus);
    }

    [Fact]
    public async Task UpdateUnitStatusAsync_OccupiedUnit_ThrowsError()
    {
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var employee = new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff Nguyen", EmploymentStatus = "active" };
        var facility = new Facility { Id = 1, Code = "FAC-01", Name = "Thu Duc Facility", AddressLine = "123 Street", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var assignment = new StaffFacilityAssignment { Id = 2, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-10) };
        var unit = new StorageUnit { Id = 102, FacilityId = 1, UnitTypeId = 1, UnitCode = "TD-102", PhysicalStatus = StorageUnitStatusConstants.Occupied, IsListed = true };

        db.Users.Add(staffUser);
        db.EmployeeProfiles.Add(employee);
        db.Facilities.Add(facility);
        db.StaffFacilityAssignments.Add(assignment);
        db.StorageUnits.Add(unit);
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);
        var request = new UpdateUnitStatusRequest(PhysicalStatus: "cleaning", Reason: "Dọn dẹp");

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.UpdateUnitStatusAsync(employeeUserId: 3, unitId: 102, request));
        Assert.Contains("thuê", ex.Message);
    }

    [Fact]
    public async Task UpdateUnitStatusAsync_LockedByOtherStaff_ThrowsForbidden()
    {
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var employee = new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff Nguyen", EmploymentStatus = "active" };
        var facility = new Facility { Id = 1, Code = "FAC-01", Name = "Thu Duc Facility", AddressLine = "123 Street", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var assignment = new StaffFacilityAssignment { Id = 3, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-10) };
        var unit = new StorageUnit
        {
            Id = 103,
            FacilityId = 1,
            UnitTypeId = 1,
            UnitCode = "TD-103",
            PhysicalStatus = StorageUnitStatusConstants.Cleaning,
            Notes = "[LOCKED_BY:99:OtherStaff] Dang don dep",
            IsListed = true
        };

        db.Users.Add(staffUser);
        db.EmployeeProfiles.Add(employee);
        db.Facilities.Add(facility);
        db.StaffFacilityAssignments.Add(assignment);
        db.StorageUnits.Add(unit);
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);
        var request = new UpdateUnitStatusRequest(PhysicalStatus: "available");

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.UpdateUnitStatusAsync(employeeUserId: 3, unitId: 103, request));
        Assert.Contains("OtherStaff", ex.Message);
    }

    [Fact]
    public async Task UpdateUnitStatusAsync_StaffNotAssignedToFacility_ThrowsForbidden()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var employee = new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff Nguyen", EmploymentStatus = "active" };
        var facility1 = new Facility { Id = 1, Code = "FAC-01", Name = "Thu Duc Facility", AddressLine = "123 Street", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var facility2 = new Facility { Id = 2, Code = "FAC-02", Name = "District 1 Facility", AddressLine = "456 Street", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };

        // Staff assigned to facility 1 only
        var assignment = new StaffFacilityAssignment { Id = 201, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-10) };
        // Unit is at facility 2
        var unitAtFacility2 = new StorageUnit { Id = 202, FacilityId = 2, UnitTypeId = 1, UnitCode = "D1-202", PhysicalStatus = StorageUnitStatusConstants.Available, IsListed = true };

        db.Users.Add(staffUser);
        db.EmployeeProfiles.Add(employee);
        db.Facilities.AddRange(facility1, facility2);
        db.StaffFacilityAssignments.Add(assignment);
        db.StorageUnits.Add(unitAtFacility2);
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);
        var request = new UpdateUnitStatusRequest(PhysicalStatus: "cleaning");

        // Act & Assert
        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            service.UpdateUnitStatusAsync(employeeUserId: 3, unitId: 202, request));
        Assert.True(ex is IHasAppError hasErr && hasErr.Error.Code == StaffErrors.StaffNotAssignedToFacility.Code);
    }

    [Fact]
    public async Task QrConfirmAction_CheckIn_UnconfirmedReservation_ThrowsReservationNotConfirmed()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var customerUser = new User { Id = 10, Email = "cust@system.local", PasswordHash = "hash", Status = "active" };
        var employee = new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff Nguyen", EmploymentStatus = "active" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Customer Test" };
        var facility = new Facility { Id = 1, Code = "FAC-01", Name = "Thu Duc Facility", AddressLine = "123 Street", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var assignment = new StaffFacilityAssignment { Id = 202, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-10) };
        var unit = new StorageUnit { Id = 101, FacilityId = 1, UnitTypeId = 1, UnitCode = "TD-101", PhysicalStatus = StorageUnitStatusConstants.Reserved, IsListed = true };

        var reservation = new Reservation
        {
            Id = 60,
            ReservationCode = "RSV-UNPAID",
            CustomerId = 10,
            FacilityId = 1,
            UnitTypeId = 1,
            FacilityRateId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            MonthlyRateSnapshot = 1000000m,
            DepositSnapshot = 1000000m,
            QuotedTotal = 2000000m,
            HoldUntil = DateTimeOffset.UtcNow.AddHours(2),
            Status = ReservationStatusConstants.Pending, // Unpaid / pending
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Users.AddRange(staffUser, customerUser);
        db.EmployeeProfiles.Add(employee);
        db.CustomerProfiles.Add(customer);
        db.Facilities.Add(facility);
        db.StaffFacilityAssignments.Add(assignment);
        db.StorageUnits.Add(unit);
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);

        // Act & Assert
        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            service.ConfirmQrActionAsync(employeeUserId: 3, new StaffConfirmQrActionRequest("check_in", ReservationId: 60)));
        Assert.True(ex is IHasAppError hasErr && hasErr.Error.Code == StaffErrors.ReservationNotConfirmed.Code);
    }

    [Fact]
    public async Task CompleteTicket_CleaningWithActiveRental_KeepsUnitOccupied()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var customerUser = new User { Id = 10, Email = "cust@system.local", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Customer Test" };
        var facility = new Facility { Id = 1, Code = "FAC-01", Name = "Thu Duc Facility", AddressLine = "123 Street", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var assignment = new StaffFacilityAssignment { Id = 203, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-10) };

        var unit = new StorageUnit
        {
            Id = 301,
            FacilityId = 1,
            UnitTypeId = 1,
            UnitCode = "TD-301",
            PhysicalStatus = StorageUnitStatusConstants.Maintenance,
            IsListed = false
        };

        var agreement = new RentalAgreement
        {
            Id = 300,
            AgreementNo = "AGR-ACTIVE-300",
            CustomerId = 10,
            FacilityId = 1,
            PolicyVersionId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            MonthlyRateSnapshot = 1500000m,
            DepositSnapshot = 1500000m,
            DepositBalance = 1500000m,
            Status = RentalAgreementStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var activeRentalAllocation = new UnitAllocation
        {
            Id = 301,
            AgreementId = 300,
            StorageUnitId = 301,
            AllocationKind = AllocationKindConstants.Rental,
            AllocationStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            AllocationEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            Status = AllocationStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var ticket = new SupportTicket
        {
            Id = 999,
            TicketNo = "TCK-999",
            CustomerId = 10,
            FacilityId = 1,
            StorageUnitId = 301,
            Category = "repair",
            Priority = TicketPriorityConstants.High,
            Subject = "Sửa khóa cửa cho khách đang thuê",
            Description = "Khóa bị kẹt",
            Status = TicketStatusConstants.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Users.AddRange(staffUser, customerUser);
        db.EmployeeProfiles.Add(new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff Nguyen", EmploymentStatus = "active" });
        db.CustomerProfiles.Add(customer);
        db.Facilities.Add(facility);
        db.StaffFacilityAssignments.Add(assignment);
        db.StorageUnits.Add(unit);
        db.RentalAgreements.Add(agreement);
        db.UnitAllocations.Add(activeRentalAllocation);
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);

        // Act
        var result = await service.CompleteTicketAsync(employeeUserId: 3, ticketId: 999, new StaffCompleteTicketRequest("Đã sửa khóa xong"));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TicketStatusConstants.Resolved, result.Status);
        Assert.Equal(StorageUnitStatusConstants.Occupied, result.UnlockedUnitStatus);

        var updatedUnit = await db.StorageUnits.FindAsync(301L);
        Assert.Equal(StorageUnitStatusConstants.Occupied, updatedUnit!.PhysicalStatus);
        Assert.False(updatedUnit.IsListed);
    }

    [Fact]
    public async Task AssignUnitToReservation_MismatchedUnitType_ThrowsUnitTypeMismatch()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var customerUser = new User { Id = 10, Email = "cust@system.local", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Customer Test" };
        var facility = new Facility { Id = 1, Code = "FAC-01", Name = "Thu Duc Facility", AddressLine = "123 Street", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var assignment = new StaffFacilityAssignment { Id = 204, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-10) };

        var unitTypeSmall = new UnitType { Id = 1, Code = "SM", Name = "Small", WidthM = 1, LengthM = 1, HeightM = 1, IsActive = true };
        var unitTypeLarge = new UnitType { Id = 2, Code = "LG", Name = "Large", WidthM = 3, LengthM = 3, HeightM = 3, IsActive = true };

        var reservation = new Reservation
        {
            Id = 70,
            ReservationCode = "RSV-SM",
            CustomerId = 10,
            FacilityId = 1,
            UnitTypeId = 1, // Small
            FacilityRateId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            MonthlyRateSnapshot = 1000000m,
            DepositSnapshot = 1000000m,
            QuotedTotal = 2000000m,
            HoldUntil = DateTimeOffset.UtcNow.AddHours(2),
            Status = ReservationStatusConstants.Confirmed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Storage unit has Large type (UnitTypeId = 2)
        var largeUnit = new StorageUnit { Id = 402, FacilityId = 1, UnitTypeId = 2, UnitCode = "TD-LG-402", PhysicalStatus = StorageUnitStatusConstants.Available, IsListed = true };

        db.Users.AddRange(staffUser, customerUser);
        db.EmployeeProfiles.Add(new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff Nguyen", EmploymentStatus = "active" });
        db.CustomerProfiles.Add(customer);
        db.Facilities.Add(facility);
        db.StaffFacilityAssignments.Add(assignment);
        db.UnitTypes.AddRange(unitTypeSmall, unitTypeLarge);
        db.StorageUnits.Add(largeUnit);
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);

        // Act & Assert
        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            service.AssignUnitToReservationAsync(employeeUserId: 3, reservationId: 70, new AssignUnitToReservationRequest(StorageUnitId: 402)));
        Assert.True(ex is IHasAppError hasErr && hasErr.Error.Code == StaffErrors.UnitTypeMismatch.Code);
    }

    [Fact]
    public async Task VerifyQrAsync_CrossFacility_ThrowsStaffNotAssignedToFacility()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var customerUser = new User { Id = 10, Email = "cust@system.local", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Customer B" };

        var fac1 = new Facility { Id = 1, Code = "FAC-01", Name = "Facility 1", AddressLine = "Addr 1", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var fac2 = new Facility { Id = 2, Code = "FAC-02", Name = "Facility 2", AddressLine = "Addr 2", City = "HN", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var unitType = new UnitType { Id = 1, Code = "SM", Name = "Small", WidthM = 1, LengthM = 1, HeightM = 1, IsActive = true };

        var reservationFac2 = new Reservation
        {
            Id = 80,
            ReservationCode = "RSV-FAC2",
            CustomerId = 10,
            FacilityId = 2,
            UnitTypeId = 1,
            FacilityRateId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            MonthlyRateSnapshot = 1000000m,
            DepositSnapshot = 1000000m,
            QuotedTotal = 2000000m,
            HoldUntil = DateTimeOffset.UtcNow.AddHours(2),
            Status = ReservationStatusConstants.Confirmed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Users.AddRange(staffUser, customerUser);
        db.EmployeeProfiles.Add(new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff A", EmploymentStatus = "active" });
        db.CustomerProfiles.Add(customer);
        db.Facilities.AddRange(fac1, fac2);
        db.UnitTypes.Add(unitType);
        db.Reservations.Add(reservationFac2);
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment { Id = 301, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-5) });
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);

        // Act & Assert
        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            service.VerifyQrAsync(employeeUserId: 3, new StaffQrVerifyRequest("CHECKIN:RSV-FAC2:10:2")));
        Assert.True(ex is IHasAppError err && err.Error.Code == StaffErrors.StaffNotAssignedToFacility.Code);
    }

    [Fact]
    public async Task LookupReservationsAndGetMoveOuts_FiltersByStaffFacilityAndBlocksCrossFacility()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var customerUser = new User { Id = 10, Email = "cust@system.local", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Customer Test" };

        var fac1 = new Facility { Id = 1, Code = "FAC-01", Name = "Facility 1", AddressLine = "Addr 1", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var fac2 = new Facility { Id = 2, Code = "FAC-02", Name = "Facility 2", AddressLine = "Addr 2", City = "HN", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var unitType = new UnitType { Id = 1, Code = "SM", Name = "Small", WidthM = 1, LengthM = 1, HeightM = 1, IsActive = true };

        var rsv1 = new Reservation { Id = 81, ReservationCode = "RSV-F1", CustomerId = 10, FacilityId = 1, UnitTypeId = 1, FacilityRateId = 1, StartDate = DateOnly.FromDateTime(DateTime.UtcNow), EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), Status = ReservationStatusConstants.Confirmed, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var rsv2 = new Reservation { Id = 82, ReservationCode = "RSV-F2", CustomerId = 10, FacilityId = 2, UnitTypeId = 1, FacilityRateId = 1, StartDate = DateOnly.FromDateTime(DateTime.UtcNow), EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), Status = ReservationStatusConstants.Confirmed, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };

        var agr1 = new RentalAgreement { Id = 401, AgreementNo = "AGR-F1", CustomerId = 10, FacilityId = 1, PolicyVersionId = 1, StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)), EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)), Status = RentalAgreementStatusConstants.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var agr2 = new RentalAgreement { Id = 402, AgreementNo = "AGR-F2", CustomerId = 10, FacilityId = 2, PolicyVersionId = 1, StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)), EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)), Status = RentalAgreementStatusConstants.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };

        var mo1 = new MoveOutRequest { Id = 501, AgreementId = 401, RequestedBy = 10, RequestedMoveOutDate = DateOnly.FromDateTime(DateTime.UtcNow), Status = MoveOutStatusConstants.Requested, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var mo2 = new MoveOutRequest { Id = 502, AgreementId = 402, RequestedBy = 10, RequestedMoveOutDate = DateOnly.FromDateTime(DateTime.UtcNow), Status = MoveOutStatusConstants.Requested, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };

        db.Users.AddRange(staffUser, customerUser);
        db.EmployeeProfiles.Add(new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff A", EmploymentStatus = "active" });
        db.CustomerProfiles.Add(customer);
        db.Facilities.AddRange(fac1, fac2);
        db.UnitTypes.Add(unitType);
        db.Reservations.AddRange(rsv1, rsv2);
        db.RentalAgreements.AddRange(agr1, agr2);
        db.MoveOutRequests.AddRange(mo1, mo2);
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment { Id = 302, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-5) });
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);

        // Act: Omit facilityId -> auto-scoped to Facility 1
        var reservations = await service.LookupReservationsAsync(employeeUserId: 3, query: null, facilityId: null);
        var moveOuts = await service.GetMoveOutsAsync(employeeUserId: 3, facilityId: null);

        // Assert
        Assert.Single(reservations);
        Assert.Equal("RSV-F1", reservations[0].ReservationCode);
        Assert.Single(moveOuts);
        Assert.Equal("AGR-F1", moveOuts[0].AgreementNo);

        // Cross-facility access must throw
        await Assert.ThrowsAnyAsync<Exception>(() => service.LookupReservationsAsync(employeeUserId: 3, query: null, facilityId: 2));
        await Assert.ThrowsAnyAsync<Exception>(() => service.GetMoveOutsAsync(employeeUserId: 3, facilityId: 2));
    }

    [Fact]
    public async Task SubmitInspectionReport_WhenDamageExceedsDeposit_ZerosOutAgreementDepositBalance()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var customerUser = new User { Id = 10, Email = "cust@system.local", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Customer Test" };
        var facility = new Facility { Id = 1, Code = "FAC-01", Name = "Facility 1", AddressLine = "Addr 1", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var unit = new StorageUnit { Id = 601, FacilityId = 1, UnitTypeId = 1, UnitCode = "U-601", PhysicalStatus = StorageUnitStatusConstants.Occupied, IsListed = false };

        var agreement = new RentalAgreement
        {
            Id = 600,
            AgreementNo = "AGR-600",
            CustomerId = 10,
            FacilityId = 1,
            PolicyVersionId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MonthlyRateSnapshot = 1000000m,
            DepositSnapshot = 1000000m,
            DepositBalance = 1000000m,
            Status = RentalAgreementStatusConstants.MoveOutScheduled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var alloc = new UnitAllocation
        {
            Id = 601,
            AgreementId = 600,
            StorageUnitId = 601,
            AllocationKind = AllocationKindConstants.Rental,
            AllocationStartDate = agreement.StartDate,
            AllocationEndDate = agreement.EndDate,
            Status = AllocationStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var inspection = new Inspection
        {
            Id = 700,
            AgreementId = 600,
            FacilityId = 1,
            StorageUnitId = 601,
            InspectedBy = 3,
            InspectionType = "check_out",
            OverallCondition = "good",
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Users.AddRange(staffUser, customerUser);
        db.EmployeeProfiles.Add(new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff A", EmploymentStatus = "active" });
        db.CustomerProfiles.Add(customer);
        db.Facilities.Add(facility);
        db.StorageUnits.Add(unit);
        db.RentalAgreements.Add(agreement);
        db.UnitAllocations.Add(alloc);
        db.Inspections.Add(inspection);
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment { Id = 303, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-5) });
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);

        // Act
        var result = await service.SubmitInspectionReportAsync(
            employeeUserId: 3,
            inspectionId: 700,
            new StaffSubmitInspectionReportRequest
            {
                OverallCondition = "damaged",
                Summary = "Hỏng cửa cuốn nặng",
                DamageCost = 1500000m
            });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0m, result.RefundAmount);
        Assert.Equal(500000m, result.ExtraPenalty);
        Assert.NotNull(result.PenaltyInvoiceId);

        var updatedAgreement = await db.RentalAgreements.FindAsync(600L);
        Assert.Equal(0m, updatedAgreement!.DepositBalance);
    }

    [Fact]
    public async Task ConfirmQrAction_CheckInWithFallbackUnitId_ValidatesFacilityTypeAndAvailability()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var customerUser = new User { Id = 10, Email = "cust@system.local", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Customer Test" };
        var fac1 = new Facility { Id = 1, Code = "FAC-01", Name = "Facility 1", AddressLine = "Addr 1", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var fac2 = new Facility { Id = 2, Code = "FAC-02", Name = "Facility 2", AddressLine = "Addr 2", City = "HN", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var type1 = new UnitType { Id = 1, Code = "SM", Name = "Small", WidthM = 1, LengthM = 1, HeightM = 1, IsActive = true };
        var type2 = new UnitType { Id = 2, Code = "LG", Name = "Large", WidthM = 2, LengthM = 2, HeightM = 2, IsActive = true };

        var unitWrongFacility = new StorageUnit { Id = 701, FacilityId = 2, UnitTypeId = 1, UnitCode = "U-F2", PhysicalStatus = StorageUnitStatusConstants.Available, IsListed = true };
        var unitWrongType = new StorageUnit { Id = 702, FacilityId = 1, UnitTypeId = 2, UnitCode = "U-T2", PhysicalStatus = StorageUnitStatusConstants.Available, IsListed = true };
        var unitOccupied = new StorageUnit { Id = 703, FacilityId = 1, UnitTypeId = 1, UnitCode = "U-OCC", PhysicalStatus = StorageUnitStatusConstants.Occupied, IsListed = true };
        var unitValid = new StorageUnit { Id = 704, FacilityId = 1, UnitTypeId = 1, UnitCode = "U-OK", PhysicalStatus = StorageUnitStatusConstants.Available, IsListed = true };

        var reservation = new Reservation
        {
            Id = 710,
            ReservationCode = "RSV-710",
            CustomerId = 10,
            FacilityId = 1,
            UnitTypeId = 1,
            FacilityRateId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            MonthlyRateSnapshot = 1000000m,
            DepositSnapshot = 1000000m,
            QuotedTotal = 2000000m,
            Status = ReservationStatusConstants.Confirmed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Users.AddRange(staffUser, customerUser);
        db.EmployeeProfiles.Add(new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff A", EmploymentStatus = "active" });
        db.CustomerProfiles.Add(customer);
        db.Facilities.AddRange(fac1, fac2);
        db.UnitTypes.AddRange(type1, type2);
        db.StorageUnits.AddRange(unitWrongFacility, unitWrongType, unitOccupied, unitValid);
        db.Reservations.Add(reservation);
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment { Id = 304, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-5) });
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);

        // 1. Wrong facility -> Staff.FacilityMismatch
        var exFac = await Assert.ThrowsAnyAsync<Exception>(() =>
            service.ConfirmQrActionAsync(3, new StaffConfirmQrActionRequest(StaffActionConstants.CheckIn, ReservationId: 710, UnitId: 701)));
        Assert.True(exFac is IHasAppError errFac && errFac.Error.Code == "Staff.FacilityMismatch");

        // Reset reservation status after in-memory tracked change before throw
        reservation.Status = ReservationStatusConstants.Confirmed;

        // 2. Wrong unit type -> StaffErrors.UnitTypeMismatch
        var exType = await Assert.ThrowsAnyAsync<Exception>(() =>
            service.ConfirmQrActionAsync(3, new StaffConfirmQrActionRequest(StaffActionConstants.CheckIn, ReservationId: 710, UnitId: 702)));
        Assert.True(exType is IHasAppError errType && errType.Error.Code == StaffErrors.UnitTypeMismatch.Code);

        reservation.Status = ReservationStatusConstants.Confirmed;

        // 3. Occupied unit -> StaffErrors.UnitNotAvailable
        var exOcc = await Assert.ThrowsAnyAsync<Exception>(() =>
            service.ConfirmQrActionAsync(3, new StaffConfirmQrActionRequest(StaffActionConstants.CheckIn, ReservationId: 710, UnitId: 703)));
        Assert.True(exOcc is IHasAppError errOcc && errOcc.Error.Code == StaffErrors.UnitNotAvailable.Code);

        reservation.Status = ReservationStatusConstants.Confirmed;

        // 4. Valid unit -> succeeds
        var okResult = await service.ConfirmQrActionAsync(3, new StaffConfirmQrActionRequest(StaffActionConstants.CheckIn, ReservationId: 710, UnitId: 704));
        Assert.True(okResult.Success);
        Assert.Equal(704L, okResult.UnitId);
        Assert.Equal(StorageUnitStatusConstants.Occupied, okResult.UnitStatus);
    }

    [Fact]
    public async Task ConfirmQrAction_TransferMoveInWithFallbackUnitId_ValidatesFacilityTypeAndAvailability()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var customerUser = new User { Id = 10, Email = "cust@system.local", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Customer Test" };
        var fac1 = new Facility { Id = 1, Code = "FAC-01", Name = "Facility 1", AddressLine = "Addr 1", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var fac2 = new Facility { Id = 2, Code = "FAC-02", Name = "Facility 2", AddressLine = "Addr 2", City = "HN", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var type1 = new UnitType { Id = 1, Code = "SM", Name = "Small", WidthM = 1, LengthM = 1, HeightM = 1, IsActive = true };
        var type2 = new UnitType { Id = 2, Code = "LG", Name = "Large", WidthM = 2, LengthM = 2, HeightM = 2, IsActive = true };

        var fromUnit = new StorageUnit { Id = 800, FacilityId = 1, UnitTypeId = 1, UnitCode = "U-FROM", PhysicalStatus = StorageUnitStatusConstants.Maintenance, IsListed = true };
        var toUnitWrongFacility = new StorageUnit { Id = 801, FacilityId = 2, UnitTypeId = 1, UnitCode = "U-TF2", PhysicalStatus = StorageUnitStatusConstants.Available, IsListed = true };
        var toUnitWrongType = new StorageUnit { Id = 802, FacilityId = 1, UnitTypeId = 2, UnitCode = "U-TT2", PhysicalStatus = StorageUnitStatusConstants.Available, IsListed = true };
        var toUnitOccupied = new StorageUnit { Id = 803, FacilityId = 1, UnitTypeId = 1, UnitCode = "U-TOCC", PhysicalStatus = StorageUnitStatusConstants.Occupied, IsListed = true };
        var toUnitValid = new StorageUnit { Id = 804, FacilityId = 1, UnitTypeId = 1, UnitCode = "U-TOK", PhysicalStatus = StorageUnitStatusConstants.Available, IsListed = true };

        var agreement = new RentalAgreement
        {
            Id = 810,
            AgreementNo = "AGR-810",
            CustomerId = 10,
            FacilityId = 1,
            PolicyVersionId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)),
            MonthlyRateSnapshot = 1000000m,
            DepositSnapshot = 1000000m,
            DepositBalance = 1000000m,
            Status = RentalAgreementStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var transfer = new UnitTransferRequest
        {
            Id = 820,
            AgreementId = 810,
            FromUnitId = 800,
            ToUnitId = null,
            RequestedFacilityId = 1,
            RequestedUnitTypeId = 1,
            RequestedBy = 10,
            Status = TransferRequestStatusConstants.MovedOutInTransit,
            Reason = "Move to Fac 1",
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Users.AddRange(staffUser, customerUser);
        db.EmployeeProfiles.Add(new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff A", EmploymentStatus = "active" });
        db.CustomerProfiles.Add(customer);
        db.Facilities.AddRange(fac1, fac2);
        db.UnitTypes.AddRange(type1, type2);
        db.StorageUnits.AddRange(fromUnit, toUnitWrongFacility, toUnitWrongType, toUnitOccupied, toUnitValid);
        db.RentalAgreements.Add(agreement);
        db.UnitTransferRequests.Add(transfer);
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment { Id = 305, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-5) });
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);

        // 1. Unit not found -> StaffErrors.UnitNotFound
        var exNotFound = await Assert.ThrowsAnyAsync<Exception>(() =>
            service.ConfirmQrActionAsync(3, new StaffConfirmQrActionRequest(StaffActionConstants.TransferMoveIn, TransferRequestId: 820, UnitId: 9999)));
        Assert.True(exNotFound is IHasAppError errNf && errNf.Error.Code == StaffErrors.UnitNotFound.Code);

        // 2. Wrong facility -> Staff.FacilityMismatch
        var exFac = await Assert.ThrowsAnyAsync<Exception>(() =>
            service.ConfirmQrActionAsync(3, new StaffConfirmQrActionRequest(StaffActionConstants.TransferMoveIn, TransferRequestId: 820, UnitId: 801)));
        Assert.True(exFac is IHasAppError errFac && errFac.Error.Code == "Staff.FacilityMismatch");

        // 3. Wrong unit type -> StaffErrors.UnitTypeMismatch
        var exType = await Assert.ThrowsAnyAsync<Exception>(() =>
            service.ConfirmQrActionAsync(3, new StaffConfirmQrActionRequest(StaffActionConstants.TransferMoveIn, TransferRequestId: 820, UnitId: 802)));
        Assert.True(exType is IHasAppError errType && errType.Error.Code == StaffErrors.UnitTypeMismatch.Code);

        // 4. Occupied unit -> StaffErrors.UnitNotAvailable
        var exOcc = await Assert.ThrowsAnyAsync<Exception>(() =>
            service.ConfirmQrActionAsync(3, new StaffConfirmQrActionRequest(StaffActionConstants.TransferMoveIn, TransferRequestId: 820, UnitId: 803)));
        Assert.True(exOcc is IHasAppError errOcc && errOcc.Error.Code == StaffErrors.UnitNotAvailable.Code);

        // 5. Valid unit -> succeeds
        var okResult = await service.ConfirmQrActionAsync(3, new StaffConfirmQrActionRequest(StaffActionConstants.TransferMoveIn, TransferRequestId: 820, UnitId: 804));
        Assert.True(okResult.Success);
        Assert.Equal(804L, okResult.UnitId);
        Assert.Equal(StorageUnitStatusConstants.Occupied, okResult.UnitStatus);
    }

    [Fact]
    public async Task GetAssignedInspections_WhenDepositBalanceIsZero_ReturnsZeroDepositInsteadOfSnapshot()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var customerUser = new User { Id = 10, Email = "cust@system.local", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Customer Test" };
        var facility = new Facility { Id = 1, Code = "FAC-01", Name = "Facility 1", AddressLine = "Addr 1", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var unit = new StorageUnit { Id = 901, FacilityId = 1, UnitTypeId = 1, UnitCode = "U-901", PhysicalStatus = StorageUnitStatusConstants.Occupied, IsListed = false };

        var agreement = new RentalAgreement
        {
            Id = 900,
            AgreementNo = "AGR-900",
            CustomerId = 10,
            FacilityId = 1,
            PolicyVersionId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MonthlyRateSnapshot = 1000000m,
            DepositSnapshot = 1000000m,
            DepositBalance = 0m, // Deducted to 0
            Status = RentalAgreementStatusConstants.MoveOutScheduled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var moveOut = new MoveOutRequest
        {
            Id = 910,
            AgreementId = 900,
            RequestedBy = 10,
            RequestedMoveOutDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = MoveOutStatusConstants.Requested,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Users.AddRange(staffUser, customerUser);
        db.EmployeeProfiles.Add(new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff A", EmploymentStatus = "active" });
        db.CustomerProfiles.Add(customer);
        db.Facilities.Add(facility);
        db.StorageUnits.Add(unit);
        db.RentalAgreements.Add(agreement);
        db.MoveOutRequests.Add(moveOut);
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment { Id = 306, EmployeeId = 3, FacilityId = 1, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-5) });
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);

        // Act
        var list = await service.GetAssignedInspectionsAsync(employeeUserId: 3, facilityId: 1);

        // Assert
        Assert.Single(list);
        Assert.Equal(0m, list[0].DepositAmount);
        Assert.Equal(0m, list[0].RefundAmount);
    }

    [Fact]
    public async Task ConfirmQrAction_TransferMoveIn_CrossFacility_UpdatesAgreementAndTicketsFacilityId_WithoutKeyException()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff2@system.local", PasswordHash = "hash", Status = "active" };
        var customerUser = new User { Id = 10, Email = "cust@system.local", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Customer Cross" };
        var fac1 = new Facility { Id = 1, Code = "FAC-01", Name = "Facility 1", AddressLine = "Addr 1", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var fac2 = new Facility { Id = 2, Code = "FAC-02", Name = "Facility 2", AddressLine = "Addr 2", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var unitType = new UnitType { Id = 1, Code = "UT-S", Name = "Small", WidthM = 2, LengthM = 2, HeightM = 2, IsActive = true };

        var fromUnit = new StorageUnit { Id = 1001, FacilityId = 1, UnitTypeId = 1, UnitCode = "F1-101", PhysicalStatus = StorageUnitStatusConstants.Maintenance, IsListed = true };
        var toUnit = new StorageUnit { Id = 2001, FacilityId = 2, UnitTypeId = 1, UnitCode = "F2-201", PhysicalStatus = StorageUnitStatusConstants.Available, IsListed = true };

        var agreement = new RentalAgreement
        {
            Id = 1050,
            AgreementNo = "AGR-1050",
            CustomerId = 10,
            FacilityId = 1,
            PolicyVersionId = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)),
            MonthlyRateSnapshot = 1000000m,
            DepositSnapshot = 500000m,
            DepositBalance = 500000m,
            Status = RentalAgreementStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var linkedTicket = new SupportTicket
        {
            Id = 5001,
            TicketNo = "TCK-5001",
            CustomerId = 10,
            FacilityId = 1,
            AgreementId = 1050,
            StorageUnitId = 1001,
            Category = "unit",
            Priority = "normal",
            Subject = "Ticket linked to agreement",
            Description = "Should follow agreement facility on cross-facility transfer",
            Status = TicketStatusConstants.Open,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var transfer = new UnitTransferRequest
        {
            Id = 1060,
            AgreementId = 1050,
            FromUnitId = 1001,
            ToUnitId = null,
            RequestedFacilityId = 2,
            RequestedUnitTypeId = 1,
            RequestedBy = 10,
            RequestedEffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MoveOutDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MoveInDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DiffDepositAmount = 100000m,
            Reason = "Cross-facility transfer to Facility 2",
            Status = TransferRequestStatusConstants.MovedOutInTransit,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Users.AddRange(staffUser, customerUser);
        db.EmployeeProfiles.Add(new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff Fac 2", EmploymentStatus = "active" });
        db.CustomerProfiles.Add(customer);
        db.Facilities.AddRange(fac1, fac2);
        db.UnitTypes.Add(unitType);
        db.StorageUnits.AddRange(fromUnit, toUnit);
        db.RentalAgreements.Add(agreement);
        db.SupportTickets.Add(linkedTicket);
        db.UnitTransferRequests.Add(transfer);
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment { Id = 401, EmployeeId = 3, FacilityId = 2, AssignmentRole = "staff", StartsAt = DateTimeOffset.UtcNow.AddDays(-5) });
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);

        // Act
        var result = await service.ConfirmQrActionAsync(
            3,
            new StaffConfirmQrActionRequest(StaffActionConstants.TransferMoveIn, TransferRequestId: 1060, UnitId: 2001));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2001L, result.UnitId);

        var updatedAgreement = await db.RentalAgreements.FindAsync(1050L);
        Assert.NotNull(updatedAgreement);
        Assert.Equal(2L, updatedAgreement!.FacilityId);
        Assert.Equal(600000m, updatedAgreement.DepositBalance);

        var updatedTicket = await db.SupportTickets.FindAsync(5001L);
        Assert.NotNull(updatedTicket);
        Assert.Equal(2L, updatedTicket!.FacilityId);
        Assert.Equal(2001L, updatedTicket.StorageUnitId);
    }

    [Fact]
    public async Task GetTicketMessagesAsync_StaffAssignedToFacility1_AccessingTicketInFacility2_ThrowsStaffNotAssignedToFacility()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var staffUser = new User { Id = 3, Email = "staff1@system.local", PasswordHash = "hash", Status = "active" };
        var customerUser = new User { Id = 10, Email = "cust@system.local", PasswordHash = "hash", Status = "active" };
        var fac1 = new Facility { Id = 1, Code = "FAC-01", Name = "Facility 1", AddressLine = "Addr 1", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var fac2 = new Facility { Id = 2, Code = "FAC-02", Name = "Facility 2", AddressLine = "Addr 2", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };

        var ticketInFac2 = new SupportTicket
        {
            Id = 6001,
            TicketNo = "TCK-6001",
            CustomerId = 10,
            FacilityId = 2,
            Category = "other",
            Priority = "normal",
            Subject = "Facility 2 Ticket",
            Description = "Confidential to Facility 2",
            Status = TicketStatusConstants.Open,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Users.AddRange(staffUser, customerUser);
        db.EmployeeProfiles.Add(new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff Fac 1", EmploymentStatus = "active" });
        db.CustomerProfiles.Add(new CustomerProfile { UserId = 10, FullName = "Customer" });
        db.Facilities.AddRange(fac1, fac2);
        db.SupportTickets.Add(ticketInFac2);
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment
        {
            Id = 501,
            EmployeeId = 3,
            FacilityId = 1,
            AssignmentRole = "staff",
            StartsAt = DateTimeOffset.UtcNow.AddDays(-10)
        });
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);

        // Act & Assert
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetTicketMessagesAsync(employeeUserId: 3, ticketId: 6001));
        Assert.True(ex is IHasAppError err && err.Error.Code == StaffErrors.StaffNotAssignedToFacility.Code);
    }

    [Fact]
    public async Task ConfirmQrActionAsync_CheckIn_WhenAgreementIsDraftWithoutRentalAllocation_CreatesInspectionAndHandoverOnFirstCall()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var staffUser = new User { Id = 3, Email = "staff1@system.local", PasswordHash = "hash", Status = "active" };
        var customerUser = new User { Id = 10, Email = "cust@system.local", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Customer CheckIn", User = customerUser };
        var fac1 = new Facility { Id = 1, Code = "FAC-01", Name = "Facility 1", AddressLine = "Addr 1", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };
        var unitType = new UnitType { Id = 1, Code = "S-DRY", Name = "Small Dry", WidthM = 1.5m, LengthM = 2m, HeightM = 2.5m, IsActive = true };
        var unit = new StorageUnit { Id = 101, FacilityId = 1, UnitTypeId = 1, UnitCode = "A-101", PhysicalStatus = StorageUnitStatusConstants.Reserved, IsListed = true, UnitType = unitType, Facility = fac1 };

        var reservation = new Reservation
        {
            Id = 700,
            ReservationCode = "RSV-700",
            CustomerId = 10,
            FacilityId = 1,
            UnitTypeId = 1,
            FacilityRateId = 1,
            StartDate = today,
            EndDate = today.AddDays(30),
            MonthlyRateSnapshot = 900000m,
            DepositSnapshot = 900000m,
            BookingFeeSnapshot = 50000m,
            Status = ReservationStatusConstants.Confirmed,
            Customer = customer,
            Facility = fac1,
            UnitType = unitType,
            CreatedAt = now,
            UpdatedAt = now
        };

        var holdAlloc = new UnitAllocation
        {
            Id = 701,
            ReservationId = 700,
            StorageUnitId = 101,
            AllocationKind = AllocationKindConstants.ReservationHold,
            AllocationStartDate = today,
            AllocationEndDate = today.AddDays(30),
            Status = AllocationStatusConstants.Active,
            Reservation = reservation,
            StorageUnit = unit,
            CreatedAt = now
        };
        reservation.UnitAllocation = holdAlloc;

        var draftAgreement = new RentalAgreement
        {
            Id = 702,
            AgreementNo = "AGR-702",
            ReservationId = 700,
            CustomerId = 10,
            FacilityId = 1,
            PolicyVersionId = 1,
            StartDate = today,
            EndDate = today.AddDays(30),
            MonthlyRateSnapshot = 900000m,
            DepositSnapshot = 900000m,
            DepositBalance = 900000m,
            Status = RentalAgreementStatusConstants.Draft,
            Customer = customer,
            Facility = fac1,
            Reservation = reservation,
            CreatedAt = now,
            UpdatedAt = now
        };
        reservation.RentalAgreement = draftAgreement;

        db.Users.AddRange(staffUser, customerUser);
        db.EmployeeProfiles.Add(new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff Fac 1", EmploymentStatus = "active" });
        db.CustomerProfiles.Add(customer);
        db.Facilities.Add(fac1);
        db.UnitTypes.Add(unitType);
        db.StorageUnits.Add(unit);
        db.Reservations.Add(reservation);
        db.UnitAllocations.Add(holdAlloc);
        db.RentalAgreements.Add(draftAgreement);
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment
        {
            Id = 703,
            EmployeeId = 3,
            FacilityId = 1,
            AssignmentRole = RoleConstants.FacilityStaff,
            StartsAt = now.AddDays(-10)
        });
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);

        // Act
        var result = await service.ConfirmQrActionAsync(
            employeeUserId: 3,
            new StaffConfirmQrActionRequest(StaffActionConstants.CheckIn, ReservationId: 700, Notes: "Check-in lần đầu"));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(RentalAgreementStatusConstants.Active, result.AgreementStatus);
        Assert.Equal(StorageUnitStatusConstants.Occupied, result.UnitStatus);

        var updatedHold = await db.UnitAllocations.SingleAsync(a => a.Id == 701);
        Assert.Equal(AllocationStatusConstants.Consumed, updatedHold.Status);

        var rentalAlloc = await db.UnitAllocations.SingleAsync(a => a.AgreementId == 702 && a.Status == AllocationStatusConstants.Active);
        Assert.Equal(AllocationKindConstants.Rental, rentalAlloc.AllocationKind);

        var checkInInspection = await db.Inspections.SingleAsync(i => i.AgreementId == 702 && i.InspectionType == "check_in");
        Assert.Equal(3L, checkInInspection.InspectedBy);
        Assert.Equal(InspectionStatusConstants.Completed, checkInInspection.Status);

        var handover = await db.HandoverRecords.SingleAsync(h => h.AgreementId == 702 && h.HandoverType == HandoverTypeConstants.CheckIn);
        Assert.Equal(rentalAlloc.Id, handover.UnitAllocationId);
        Assert.Equal(checkInInspection.Id, handover.InspectionId);
        Assert.Equal(3L, handover.HandledBy);
    }

    [Fact]
    public async Task ProcessCounterCashPayout_WithMoveOutRequestIdFallback_Succeeds()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var now = DateTimeOffset.UtcNow;
        var staffUser = new User { Id = 3, Email = "staff@system.local", PasswordHash = "hash", Status = "active" };
        var customerUser = new User { Id = 10, Email = "cust@system.local", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 10, FullName = "Customer Test" };
        var fac = new Facility { Id = 1, Code = "FAC-01", Name = "Facility 1", AddressLine = "Addr 1", City = "HCM", Timezone = "Asia/Ho_Chi_Minh", Status = "active" };

        var agr = new RentalAgreement { Id = 801, AgreementNo = "AGR-801", CustomerId = 10, FacilityId = 1, PolicyVersionId = 1, StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)), EndDate = DateOnly.FromDateTime(DateTime.UtcNow), Status = RentalAgreementStatusConstants.Active, CreatedAt = now, UpdatedAt = now };
        var mo = new MoveOutRequest { Id = 901, AgreementId = 801, RequestedBy = 10, RequestedMoveOutDate = DateOnly.FromDateTime(DateTime.UtcNow), Status = MoveOutStatusConstants.Completed, CreatedAt = now, UpdatedAt = now };

        var refund = new Refund
        {
            Id = 555,
            PaymentId = 100,
            AgreementId = 801,
            Amount = 1500000m,
            Currency = "VND",
            Reason = "Refund deposit",
            Provider = RefundPayoutMethodConstants.Cash,
            IdempotencyKey = "REFUND-555",
            Status = RefundStatusConstants.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Users.AddRange(staffUser, customerUser);
        db.EmployeeProfiles.Add(new EmployeeProfile { UserId = 3, EmployeeCode = "EMP-003", FullName = "Staff A", EmploymentStatus = "active" });
        db.CustomerProfiles.Add(customer);
        db.Facilities.Add(fac);
        db.RentalAgreements.Add(agr);
        db.MoveOutRequests.Add(mo);
        db.Refunds.Add(refund);
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment { Id = 301, EmployeeId = 3, FacilityId = 1, AssignmentRole = RoleConstants.FacilityStaff, StartsAt = now.AddDays(-5) });
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db);

        // Act 1: GetMoveOutsAsync should populate refund info
        var moveOuts = await service.GetMoveOutsAsync(employeeUserId: 3, facilityId: 1);
        Assert.Single(moveOuts);
        Assert.Equal(555L, moveOuts[0].RefundId);
        Assert.Equal(1500000m, moveOuts[0].RefundAmount);

        // Act 2: ProcessCounterCashPayoutAsync using moveOut.Id (901) instead of refund.Id (555)
        var payoutResult = await service.ProcessCounterCashPayoutAsync(
            employeeUserId: 3,
            refundId: 901L,
            new StaffCounterCashPayoutRequest { Note = "Chi tiền mặt tại quầy" });

        // Assert
        Assert.Equal(RefundStatusConstants.Completed, payoutResult.Status);
        Assert.Equal(555L, payoutResult.RefundId);
        var updatedRefund = await db.Refunds.FindAsync(555L);
        Assert.NotNull(updatedRefund);
        Assert.Equal(RefundStatusConstants.Completed, updatedRefund.Status);
        Assert.Equal(RefundPayoutMethodConstants.Cash, updatedRefund.Provider);
    }
}
