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
    public async Task WalkInBooking_Success_CreatesUserReservationInvoiceAndReturnsVietQr()
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
            MonthlyRate = 1000000m,
            DepositAmount = 1000000m,
            BookingFee = 0,
            ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1))
        };

        db.Users.Add(staffUser);
        db.EmployeeProfiles.Add(employee);
        db.Facilities.Add(facility);
        db.UnitTypes.Add(unitType);
        db.StorageUnits.Add(unit);
        db.FacilityRates.Add(rate);
        await db.SaveChangesAsync();

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

        // Act
        var response = await service.CreateWalkInBookingAsync(employeeUserId: 3, request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Nguyen Van A", response.CustomerName);
        Assert.Equal("0901234567", response.CustomerPhone);
        Assert.Equal(2000000m, response.TotalAmount); // 1 month rent + 1 month deposit
        Assert.Contains("vietqr", response.QrImageUrl);
        Assert.Equal("awaiting_payment", response.Status);

        // Unit should now be Reserved
        var updatedUnit = await db.StorageUnits.FindAsync(101L);
        Assert.Equal(StorageUnitStatusConstants.Reserved, updatedUnit!.PhysicalStatus);

        // Reservation and invoice exist
        var reservation = await db.Reservations.FindAsync(response.ReservationId);
        Assert.NotNull(reservation);
        Assert.Equal(ReservationStatusConstants.Pending, reservation.Status);
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

        db.Users.AddRange(staffUser, customerUser);
        db.CustomerProfiles.Add(customer);
        db.Facilities.Add(facility);
        db.UnitTypes.Add(unitType);
        db.StorageUnits.Add(unit);
        db.Reservations.Add(reservation);
        db.UnitAllocations.Add(allocation);
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

        db.Users.AddRange(staffUser, customerUser);
        db.CustomerProfiles.Add(customer);
        db.Facilities.Add(facility);
        db.UnitTypes.Add(unitType);
        db.StorageUnits.Add(unit);
        db.Reservations.Add(reservation);
        db.UnitAllocations.Add(allocation);
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

        db.Users.AddRange(staffUser, customerUser);
        db.CustomerProfiles.Add(customer);
        db.Facilities.AddRange(fromFacility, toFacility);
        db.StorageUnits.Add(fromUnit);
        db.RentalAgreements.Add(agreement);
        db.UnitTransferRequests.Add(transfer);
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

        db.Users.AddRange(staffUser, customerUser);
        db.CustomerProfiles.Add(customer);
        db.Facilities.Add(facility);
        db.StorageUnits.Add(unit);
        db.RentalAgreements.Add(agreement);
        db.UnitAllocations.Add(allocation);
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
        var request = new UpdateUnitStatusRequest(PhysicalStatus: "under_maintenance");

        // Act
        await service.UpdateUnitStatusAsync(employeeUserId: 3, unitId: 101, request);

        // Assert
        var updatedUnit = await db.StorageUnits.FindAsync(101L);
        Assert.NotNull(updatedUnit);
        Assert.Equal("under_maintenance", updatedUnit.PhysicalStatus);
    }
}
