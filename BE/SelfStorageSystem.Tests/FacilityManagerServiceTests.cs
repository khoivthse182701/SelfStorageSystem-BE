using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using SelfStorageSystem.Contracts.Manager;
using SelfStorageSystem.Domain.Common;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;
using Xunit;

namespace SelfStorageSystem.Tests;

public class FacilityManagerServiceTests
{
    private SelfStorageDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<SelfStorageDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new SelfStorageDbContext(options);
    }

    private async Task SeedFacilityAndManagerAsync(SelfStorageDbContext db, long facilityId, long managerUserId)
    {
        var facility = new Facility
        {
            Id = facilityId,
            Code = $"FAC-{facilityId}",
            Name = $"Facility {facilityId}",
            AddressLine = "123 Street",
            City = "City",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Facilities.Add(facility);

        var managerUser = new User
        {
            Id = managerUserId,
            Email = $"manager{managerUserId}@storage.local",
            Status = UserStatusConstants.Active,
            PasswordHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(managerUser);

        var managerEmployee = new EmployeeProfile
        {
            UserId = managerUserId,
            EmployeeCode = $"EMP-MGR-{managerUserId}",
            FullName = $"Manager {managerUserId}",
            HireDate = new DateOnly(2024, 1, 1),
            EmploymentStatus = "active"
        };
        db.EmployeeProfiles.Add(managerEmployee);

        var assignment = new StaffFacilityAssignment
        {
            Id = managerUserId,
            FacilityId = facilityId,
            EmployeeId = managerUserId,
            AssignmentRole = RoleConstants.FacilityManager,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-10),
            EndsAt = null
        };
        db.StaffFacilityAssignments.Add(assignment);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task EnsureAccess_NonAssignedManager_ThrowsDomainException()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);

        await SeedFacilityAndManagerAsync(db, 1, 10);

        // Act & Assert - Manager 99 has no assignment to facility 1
        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            service.GetUnitsAsync(1, null, null, null, 1, 10, currentUserId: 99, isSystemAdmin: false));

        var hasError = Assert.IsAssignableFrom<IHasAppError>(ex);
        Assert.Equal(ManagerErrors.NotAssignedToFacility.Code, hasError.Error.Code);
    }

    [Fact]
    public async Task EnsureAccess_SystemAdmin_BypassesAssignmentCheck()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);

        await SeedFacilityAndManagerAsync(db, 1, 10);

        // Act - Admin user 999 with isSystemAdmin: true
        var result = await service.GetUnitsAsync(1, null, null, null, 1, 10, currentUserId: 999, isSystemAdmin: true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task CreateUnitAsync_WithDuplicateUnitCode_ThrowsConflictException()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);
        await SeedFacilityAndManagerAsync(db, 1, 10);

        var unitType = new UnitType
        {
            Id = 1,
            Code = "SMALL",
            Name = "Small Unit",
            WidthM = 2,
            LengthM = 2,
            HeightM = 2,
            AreaM2 = 4,
            VolumeM3 = 8,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.UnitTypes.Add(unitType);

        var existingUnit = new StorageUnit
        {
            Id = 1,
            FacilityId = 1,
            UnitTypeId = 1,
            UnitCode = "U-101",
            PhysicalStatus = StorageUnitStatusConstants.Available,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.StorageUnits.Add(existingUnit);
        await db.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            service.CreateUnitAsync(1, new ManagerCreateStorageUnitRequest
            {
                UnitCode = "U-101",
                UnitTypeId = 1
            }, 10, false));

        var hasError = Assert.IsAssignableFrom<IHasAppError>(ex);
        Assert.Equal(ManagerErrors.UnitCodeAlreadyExists.Code, hasError.Error.Code);
    }

    [Fact]
    public async Task CreateUnitAsync_Success_ReturnsNewUnit()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);
        await SeedFacilityAndManagerAsync(db, 1, 10);

        var unitType = new UnitType
        {
            Id = 1,
            Code = "MED",
            Name = "Medium Unit",
            WidthM = 3,
            LengthM = 3,
            HeightM = 2.5m,
            AreaM2 = 9,
            VolumeM3 = 22.5m,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.UnitTypes.Add(unitType);
        await db.SaveChangesAsync();

        // Act
        var result = await service.CreateUnitAsync(1, new ManagerCreateStorageUnitRequest
        {
            UnitCode = "U-201",
            UnitTypeId = 1,
            FloorLabel = "Floor 2",
            ZoneLabel = "Zone B",
            PhysicalStatus = StorageUnitStatusConstants.Available,
            IsListed = true
        }, 10, false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("U-201", result.UnitCode);
        Assert.Equal("MED", result.UnitTypeCode);
        Assert.Equal(9m, result.AreaM2);
        Assert.Equal(22.5m, result.VolumeM3);

        var saved = await db.StorageUnits.FirstOrDefaultAsync(u => u.UnitCode == "U-201");
        Assert.NotNull(saved);
        Assert.Equal("Floor 2", saved.FloorLabel);
    }

    [Fact]
    public async Task UpdateUnitAsync_OccupiedUnitChangePhysicalStatus_ThrowsException()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);
        await SeedFacilityAndManagerAsync(db, 1, 10);

        var unitType = new UnitType
        {
            Id = 1,
            Code = "MED",
            Name = "Medium Unit",
            WidthM = 3,
            LengthM = 3,
            HeightM = 2.5m,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.UnitTypes.Add(unitType);

        var occupiedUnit = new StorageUnit
        {
            Id = 5,
            FacilityId = 1,
            UnitTypeId = 1,
            UnitCode = "U-500",
            PhysicalStatus = StorageUnitStatusConstants.Occupied,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.StorageUnits.Add(occupiedUnit);
        await db.SaveChangesAsync();

        // Act & Assert - Attempting to change physical status of occupied unit to available
        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            service.UpdateUnitAsync(5, new ManagerUpdateStorageUnitRequest
            {
                UnitCode = "U-500",
                UnitTypeId = 1,
                PhysicalStatus = StorageUnitStatusConstants.Available
            }, 10, false));

        var hasError = Assert.IsAssignableFrom<IHasAppError>(ex);
        Assert.Equal(ManagerErrors.UnitOccupiedCannotModify.Code, hasError.Error.Code);
    }

    [Fact]
    public async Task MapLayout_GetAndUpdate_SavesPositions()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);
        await SeedFacilityAndManagerAsync(db, 1, 10);

        var area = new FacilityArea
        {
            Id = 1,
            FacilityId = 1,
            Code = "A1",
            Name = "Zone A",
            AreaType = "zone",
            DisplayOrder = 1,
            MapMetadata = "{}"
        };
        db.FacilityAreas.Add(area);

        var unitType = new UnitType
        {
            Id = 1,
            Code = "U-T",
            Name = "Type 1",
            WidthM = 2,
            LengthM = 2,
            HeightM = 2,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.UnitTypes.Add(unitType);

        var unit = new StorageUnit
        {
            Id = 10,
            FacilityId = 1,
            UnitTypeId = 1,
            UnitCode = "U-10",
            PhysicalStatus = StorageUnitStatusConstants.Available,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.StorageUnits.Add(unit);
        await db.SaveChangesAsync();

        // Act - Update layout
        var count = await service.UpdateMapLayoutAsync(1, new ManagerUpdateMapLayoutRequest
        {
            Positions = new List<ManagerUnitMapPositionDto>
            {
                new ManagerUnitMapPositionDto
                {
                    UnitId = 10,
                    AreaId = 1,
                    X = 15.5m,
                    Y = 20.0m,
                    Width = 50.0m,
                    Height = 60.0m,
                    RotationDegrees = 90
                }
            }
        }, 10, false);

        // Assert
        Assert.Equal(1, count);
        var layout = await service.GetMapLayoutAsync(1, 10, false);
        Assert.NotNull(layout);
        Assert.Single(layout.UnitPositions);
        Assert.Equal(15.5m, layout.UnitPositions[0].X);
        Assert.Equal(90m, layout.UnitPositions[0].RotationDegrees);
    }

    [Fact]
    public async Task DecideTransferRequest_Approved_UpdatesTransferStatus()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);
        await SeedFacilityAndManagerAsync(db, 1, 10);

        var unitType = new UnitType
        {
            Id = 1,
            Code = "STD",
            Name = "Standard",
            WidthM = 2,
            LengthM = 2,
            HeightM = 2,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.UnitTypes.Add(unitType);

        var oldUnit = new StorageUnit
        {
            Id = 100,
            FacilityId = 1,
            UnitTypeId = 1,
            UnitCode = "U-OLD",
            PhysicalStatus = StorageUnitStatusConstants.Occupied,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var targetUnit = new StorageUnit
        {
            Id = 101,
            FacilityId = 1,
            UnitTypeId = 1,
            UnitCode = "U-NEW",
            PhysicalStatus = StorageUnitStatusConstants.Available,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.StorageUnits.AddRange(oldUnit, targetUnit);

        var customer = new User
        {
            Id = 20,
            Email = "cust@storage.local",
            Status = UserStatusConstants.Active,
            PasswordHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
            CustomerProfile = new CustomerProfile { UserId = 20, FullName = "Customer Test" }
        };
        db.Users.Add(customer);

        var agreement = new RentalAgreement
        {
            Id = 50,
            FacilityId = 1,
            CustomerId = 20,
            AgreementNo = "AGR-050",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(2)),
            MonthlyRateSnapshot = 500000,
            DepositSnapshot = 1000000,
            DepositBalance = 1000000,
            Status = RentalAgreementStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.RentalAgreements.Add(agreement);

        var transferRequest = new UnitTransferRequest
        {
            Id = 1,
            AgreementId = 50,
            FromUnitId = 100,
            RequestedUnitTypeId = 1,
            RequestedEffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Reason = "Want cleaner corner",
            Status = TransferRequestStatusConstants.Pending,
            RequestedBy = 20,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.UnitTransferRequests.Add(transferRequest);
        await db.SaveChangesAsync();

        // Act - Approve transfer
        var result = await service.DecideTransferRequestAsync(1, new ManagerDecideTransferRequest
        {
            Decision = ApprovalDecisionConstants.Approved,
            TargetUnitId = 101,
            ReviewNotes = "Approved by manager"
        }, 10, false);

        // Assert
        Assert.Equal(TransferRequestStatusConstants.Approved, result.Status);
        Assert.Equal(101, result.ToUnitId);
    }

    [Fact]
    public async Task ShiftsAndAssignments_FullLifecycle_WorksSuccessfully()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);
        await SeedFacilityAndManagerAsync(db, 1, 10);

        var staffUser = new User
        {
            Id = 30,
            Email = "staff30@storage.local",
            Status = UserStatusConstants.Active,
            PasswordHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
            EmployeeProfile = new EmployeeProfile
            {
                UserId = 30,
                EmployeeCode = "EMP-030",
                FullName = "Staff Member",
                HireDate = new DateOnly(2025, 1, 1),
                EmploymentStatus = "active"
            }
        };
        db.Users.Add(staffUser);

        var staffAssignment = new StaffFacilityAssignment
        {
            Id = 30,
            FacilityId = 1,
            EmployeeId = 30,
            AssignmentRole = RoleConstants.FacilityStaff,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-10)
        };
        db.StaffFacilityAssignments.Add(staffAssignment);
        await db.SaveChangesAsync();

        // Act 1: Create shift
        var shift = await service.CreateShiftAsync(1, new ManagerCreateShiftRequest
        {
            ShiftName = "Morning Shift",
            StartsAt = DateTimeOffset.UtcNow.AddDays(1),
            EndsAt = DateTimeOffset.UtcNow.AddDays(1).AddHours(8)
        }, 10, false);

        Assert.NotNull(shift);
        Assert.Equal("Morning Shift", shift.ShiftName);

        // Act 2: Assign staff to shift
        var assignment = await service.AssignShiftAsync(1, new ManagerCreateShiftAssignmentRequest
        {
            ShiftId = shift.Id,
            EmployeeId = 30,
            DutyRole = DutyRoleConstants.ShiftLead
        }, 10, false);

        Assert.NotNull(assignment);
        Assert.Equal("EMP-030", assignment.EmployeeCode);
        Assert.Equal(DutyRoleConstants.ShiftLead, assignment.DutyRole);

        // Act 3: Duplicate assignment should throw Conflict
        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            service.AssignShiftAsync(1, new ManagerCreateShiftAssignmentRequest
            {
                ShiftId = shift.Id,
                EmployeeId = 30,
                DutyRole = DutyRoleConstants.Staff
            }, 10, false));

        var hasError = Assert.IsAssignableFrom<IHasAppError>(ex);
        Assert.Equal(ManagerErrors.ShiftAssignmentAlreadyExists.Code, hasError.Error.Code);

        // Act 4: Roster retrieval
        var roster = await service.GetShiftRosterAsync(1, null, null, null, 10, false);
        Assert.Single(roster);
        Assert.Equal("Morning Shift", roster[0].ShiftName);
        Assert.Equal(30, roster[0].EmployeeId);
    }

    [Fact]
    public async Task DecideChargeProposal_Approved_GeneratesInvoiceAndApprovesProposal()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);
        await SeedFacilityAndManagerAsync(db, 1, 10);

        var customer = new User
        {
            Id = 50,
            Email = "customer50@storage.local",
            Status = UserStatusConstants.Active,
            PasswordHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
            CustomerProfile = new CustomerProfile { UserId = 50, FullName = "Customer Damage" }
        };
        db.Users.Add(customer);

        var staff = new User
        {
            Id = 60,
            Email = "staff60@storage.local",
            Status = UserStatusConstants.Active,
            PasswordHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
            EmployeeProfile = new EmployeeProfile { UserId = 60, EmployeeCode = "EMP-060", FullName = "Staff Reporter", HireDate = new DateOnly(2025, 1, 1), EmploymentStatus = "active" }
        };
        db.Users.Add(staff);

        var ticket = new SupportTicket
        {
            Id = 70,
            FacilityId = 1,
            CustomerId = 50,
            TicketNo = "TCK-70",
            Subject = "Broken latch",
            Description = "Door latch broken by user",
            Category = "damage",
            Priority = "medium",
            Status = TicketStatusConstants.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.SupportTickets.Add(ticket);

        var proposal = new TicketChargeProposal
        {
            Id = 1,
            TicketId = 70,
            ProposedBy = 60,
            Description = "Door handle replacement fee",
            Amount = 350000,
            Status = ProposalStatusConstants.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.TicketChargeProposals.Add(proposal);
        await db.SaveChangesAsync();

        // Act - Approve charge proposal
        var decision = await service.DecideChargeProposalAsync(1, new ManagerDecideChargeProposalRequest
        {
            Decision = ApprovalDecisionConstants.Approved,
            Reason = "Damage verified"
        }, 10, false);

        // Assert
        Assert.NotNull(decision);
        Assert.Equal(ApprovalDecisionConstants.Approved, decision.Decision);
        Assert.NotNull(decision.GeneratedInvoiceNo);
        Assert.NotNull(decision.GeneratedInvoiceId);
        Assert.Equal(350000, decision.Amount);

        // Verify generated invoice in database
        var invoice = await db.Invoices.Include(i => i.InvoiceLines).FirstOrDefaultAsync(i => i.Id == decision.GeneratedInvoiceId);
        Assert.NotNull(invoice);
        Assert.Equal(350000, invoice.TotalAmount);
        Assert.Single(invoice.InvoiceLines);
        Assert.Equal(InvoiceLineTypeConstants.Damage, invoice.InvoiceLines.First().LineType);
    }

    [Fact]
    public async Task RefundApprovals_CalculationFormula_AndDecisionWorkflow()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);
        await SeedFacilityAndManagerAsync(db, 1, 10);

        var customer = new User
        {
            Id = 75,
            Email = "customer75@storage.local",
            Status = UserStatusConstants.Active,
            PasswordHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
            CustomerProfile = new CustomerProfile { UserId = 75, FullName = "Customer Refund" }
        };
        db.Users.Add(customer);

        var unitType = new UnitType { Id = 1, Code = "U1", Name = "U1", WidthM = 2, LengthM = 2, HeightM = 2, IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.UnitTypes.Add(unitType);

        var unit = new StorageUnit { Id = 10, FacilityId = 1, UnitTypeId = 1, UnitCode = "U-10", PhysicalStatus = StorageUnitStatusConstants.Available, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.StorageUnits.Add(unit);

        var agreement = new RentalAgreement
        {
            Id = 88,
            FacilityId = 1,
            CustomerId = 75,
            AgreementNo = "AGR-088",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-2)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            MonthlyRateSnapshot = 500000,
            DepositSnapshot = 1000000, // Cọc: 1,000,000
            DepositBalance = 1000000,
            Status = RentalAgreementStatusConstants.Terminated,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.RentalAgreements.Add(agreement);

        var allocation = new UnitAllocation
        {
            Id = 1,
            AgreementId = 88,
            StorageUnitId = 10,
            AllocationKind = "primary",
            AllocationStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-2)),
            AllocationEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            Status = AllocationStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.UnitAllocations.Add(allocation);

        // Hóa đơn quá hạn chưa thanh toán (Phạt/nợ: 200,000)
        var unpaidInvoice = new Invoice
        {
            Id = 99,
            AgreementId = 88,
            CustomerId = 75,
            InvoiceNo = "INV-OVERDUE",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            TotalAmount = 200000,
            PaidAmount = 0,
            Status = InvoiceStatusConstants.Overdue,
            Currency = "VND",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Invoices.Add(unpaidInvoice);

        var ticket = new SupportTicket
        {
            Id = 1,
            FacilityId = 1,
            CustomerId = 75,
            TicketNo = "TCK-1",
            Subject = "Scratch",
            Description = "Wall scratch",
            Category = "damage",
            Priority = "low",
            Status = TicketStatusConstants.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.SupportTickets.Add(ticket);

        // Phụ phí hư hại đã duyệt (150,000)
        var damageProposal = new TicketChargeProposal
        {
            Id = 2,
            TicketId = 1,
            ProposedBy = 10,
            Description = "Wall scratch",
            Amount = 150000,
            Status = ProposalStatusConstants.Approved,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.TicketChargeProposals.Add(damageProposal);

        var payment = new Payment
        {
            Id = 200,
            CustomerId = 75,
            TargetInvoiceId = 99,
            Method = "vnpay",
            Provider = "vnpay",
            Currency = "VND",
            IdempotencyKey = "key-200",
            Metadata = "{}",
            Amount = 1000000,
            Status = PaymentConstants.StatusSucceeded,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Payments.Add(payment);

        var refund = new Refund
        {
            Id = 1,
            PaymentId = 200,
            AgreementId = 88,
            Amount = 1000000,
            Reason = "Deposit return on check-out",
            Status = RefundStatusConstants.Pending,
            Currency = "VND",
            Provider = "vnpay",
            IdempotencyKey = "ref-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Refunds.Add(refund);
        await db.SaveChangesAsync();

        // Act 1: Get list and check recommended refund calculation
        // Cọc: 1,000,000 - Hư hại (0 vì proposal chưa có invoice liên kết) - Phạt quá hạn (200,000) = 800,000
        var refundApprovals = await service.GetRefundApprovalsAsync(1, 10, false);
        Assert.Single(refundApprovals);
        Assert.Equal(1000000m, refundApprovals[0].OriginalDeposit);
        Assert.Equal(200000m, refundApprovals[0].EstimatedDeductions);
        Assert.Equal(800000m, refundApprovals[0].RecommendedRefundAmount);

        // Act 2: Decide refund approval with recommended amount
        var decision = await service.DecideRefundApprovalAsync(1, new ManagerDecideRefundApprovalRequest
        {
            Decision = ApprovalDecisionConstants.Approved,
            OverrideAmount = 800000m,
            Reason = "Deducted damages and overdue rent"
        }, 10, false);

        // Assert
        Assert.NotNull(decision);
        Assert.Equal(ApprovalDecisionConstants.Approved, decision.Decision);
        Assert.Equal(800000m, decision.ApprovedAmount);

        var updatedRefund = await db.Refunds.FindAsync(1L);
        Assert.Equal(RefundStatusConstants.Approved, updatedRefund!.Status);
        Assert.Equal(800000m, updatedRefund.Amount);
    }

    [Fact]
    public async Task Delinquency_Actions_BR_REN_04_Compliance()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);
        await SeedFacilityAndManagerAsync(db, 1, 10);

        var unitType = new UnitType { Id = 1, Code = "U1", Name = "U1", WidthM = 2, LengthM = 2, HeightM = 2, IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.UnitTypes.Add(unitType);

        var unit = new StorageUnit { Id = 10, FacilityId = 1, UnitTypeId = 1, UnitCode = "U-OVERDUE", PhysicalStatus = StorageUnitStatusConstants.Occupied, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.StorageUnits.Add(unit);

        var customer = new User
        {
            Id = 80,
            Email = "delinquent@storage.local",
            Status = UserStatusConstants.Active,
            PasswordHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
            CustomerProfile = new CustomerProfile { UserId = 80, FullName = "Delinquent User", User = new User { Id = 80, Email = "delinquent@storage.local", Status = "active", PasswordHash = "h" } }
        };
        db.Users.Add(customer);

        var agreement = new RentalAgreement
        {
            Id = 90,
            FacilityId = 1,
            CustomerId = 80,
            AgreementNo = "AGR-090",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-3)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            MonthlyRateSnapshot = 500000,
            DepositSnapshot = 1000000,
            DepositBalance = 1000000,
            Status = RentalAgreementStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.RentalAgreements.Add(agreement);

        var allocation = new UnitAllocation
        {
            Id = 1,
            AgreementId = 90,
            StorageUnitId = 10,
            AllocationKind = "primary",
            AllocationStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-3)),
            AllocationEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            Status = AllocationStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.UnitAllocations.Add(allocation);

        var delinqCase = new DelinquencyCase
        {
            Id = 1,
            AgreementId = 90,
            InvoiceId = 1,
            OutstandingSnapshot = 1500000,
            OpenedAt = DateTimeOffset.UtcNow.AddDays(-35),
            GraceEndsAt = DateTimeOffset.UtcNow.AddDays(-5),
            Status = DelinquencyCaseStatusConstants.Delinquent
        };
        db.DelinquencyCases.Add(delinqCase);
        await db.SaveChangesAsync();

        // Act 1: Get delinquent cases
        var cases = await service.GetDelinquentCasesAsync(1, 10, false);
        Assert.Single(cases);
        Assert.Equal("U-OVERDUE", cases[0].UnitCode);

        // Act 2: Record lock access / seal action (BR-REN-04)
        var actionResult = await service.AddDelinquencyActionAsync(1, new ManagerAddDelinquencyActionRequest
        {
            ActionType = DelinquencyActionTypeConstants.CredentialSuspended,
            Note = "Door access suspended due to 35-day delinquency",
            UpdateCaseStatus = DelinquencyCaseStatusConstants.TerminationStarted
        }, 10, false);

        // Assert
        Assert.NotNull(actionResult);
        Assert.Equal(DelinquencyActionTypeConstants.CredentialSuspended, actionResult.ActionType);
        Assert.Equal(10, actionResult.PerformedBy);

        var updatedCase = await db.DelinquencyCases.Include(c => c.DelinquencyActions).FirstOrDefaultAsync(c => c.Id == 1);
        Assert.NotNull(updatedCase);
        Assert.Equal(DelinquencyCaseStatusConstants.TerminationStarted, updatedCase.Status);
        Assert.Single(updatedCase.DelinquencyActions);
    }

    [Fact]
    public async Task Dashboard_CalculatesOccupancy_AndFinancialsAccurately()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);
        await SeedFacilityAndManagerAsync(db, 1, 10);

        var unitType = new UnitType { Id = 1, Code = "U1", Name = "U1", WidthM = 2, LengthM = 2, HeightM = 2, IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.UnitTypes.Add(unitType);

        // 4 units: 2 occupied, 1 reserved, 1 available -> Total 4, Occupied 2, Reserved 1 -> Occupancy rate = (2+1)/4 * 100 = 75%
        db.StorageUnits.AddRange(
            new StorageUnit { Id = 1, FacilityId = 1, UnitTypeId = 1, UnitCode = "U1", PhysicalStatus = StorageUnitStatusConstants.Occupied, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new StorageUnit { Id = 2, FacilityId = 1, UnitTypeId = 1, UnitCode = "U2", PhysicalStatus = StorageUnitStatusConstants.Occupied, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new StorageUnit { Id = 3, FacilityId = 1, UnitTypeId = 1, UnitCode = "U3", PhysicalStatus = StorageUnitStatusConstants.Reserved, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new StorageUnit { Id = 4, FacilityId = 1, UnitTypeId = 1, UnitCode = "U4", PhysicalStatus = StorageUnitStatusConstants.Available, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        );

        var customer = new User { Id = 100, Email = "c100@storage.local", Status = UserStatusConstants.Active, PasswordHash = "hash", CreatedAt = DateTimeOffset.UtcNow };
        db.Users.Add(customer);

        var agreement = new RentalAgreement
        {
            Id = 1,
            FacilityId = 1,
            CustomerId = 100,
            AgreementNo = "AGR-100",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            MonthlyRateSnapshot = 500000,
            DepositSnapshot = 1000000,
            DepositBalance = 1000000,
            Status = RentalAgreementStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.RentalAgreements.Add(agreement);

        // Invoice: Total 1,000,000, Paid 400,000 => Outstanding 600,000
        var invoice = new Invoice
        {
            Id = 1,
            AgreementId = 1,
            CustomerId = 100,
            InvoiceNo = "INV-100",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            TotalAmount = 1000000,
            PaidAmount = 400000,
            Status = InvoiceStatusConstants.Open,
            Currency = "VND",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Invoices.Add(invoice);

        // Completed Payment this month: 400,000
        var payment = new Payment
        {
            Id = 1,
            CustomerId = 100,
            TargetInvoiceId = 1,
            Method = "vnpay",
            Provider = "vnpay",
            Currency = "VND",
            IdempotencyKey = "key-1",
            Metadata = "{}",
            Amount = 400000,
            Status = PaymentConstants.StatusSucceeded,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Payments.Add(payment);

        var ticket = new SupportTicket
        {
            Id = 1,
            FacilityId = 1,
            CustomerId = 100,
            TicketNo = "TCK-1",
            Subject = "Help",
            Description = "Urgent help needed",
            Priority = TicketPriorityConstants.Urgent,
            Category = "support",
            Status = TicketStatusConstants.Open,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync();

        // Act
        var dashboard = await service.GetFacilityDashboardAsync(1, 10, false);

        // Assert
        Assert.NotNull(dashboard);
        Assert.Equal(4, dashboard.TotalUnits);
        Assert.Equal(2, dashboard.OccupiedUnits);
        Assert.Equal(1, dashboard.ReservedUnits);
        Assert.Equal(1, dashboard.AvailableUnits);
        Assert.Equal(75.0, dashboard.OccupancyRate);
        Assert.Equal(400000m, dashboard.ThisMonthRevenue);
        Assert.Equal(600000m, dashboard.TotalOutstandingDebt);
        Assert.Equal(1, dashboard.OpenTicketsCount);
        Assert.Equal(1, dashboard.UrgentTicketsCount);
    }
}
