using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SelfStorageSystem.Infrastructure.BackgroundJobs;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Manager;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;
using Xunit;

namespace SelfStorageSystem.Tests;

public class ManagerRound2Tests
{
    private static SelfStorageDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<SelfStorageDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new SelfStorageDbContext(options);
    }

    private static async Task SeedFacilityAndManagerAsync(SelfStorageDbContext db, long facilityId = 1L, long managerUserId = 10L)
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
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.StaffFacilityAssignments.Add(assignment);
        await db.SaveChangesAsync();
    }

    private static void SeedCustomerAndStaff(SelfStorageDbContext db, long customerId = 2L, long staffId = 30L)
    {
        var custUser = new User { Id = customerId, Email = $"cust{customerId}@test.com", PasswordHash = "hash", Status = "active" };
        var custProfile = new CustomerProfile { UserId = customerId, FullName = $"Customer {customerId}", User = custUser };
        db.Users.Add(custUser);
        db.CustomerProfiles.Add(custProfile);

        var staffUser = new User { Id = staffId, Email = $"staff{staffId}@test.com", PasswordHash = "hash", Status = "active" };
        var staffProfile = new EmployeeProfile
        {
            UserId = staffId,
            FullName = $"Staff {staffId}",
            EmployeeCode = $"EMP-{staffId}",
            EmploymentStatus = "active",
            HireDate = new DateOnly(2024, 1, 1),
            User = staffUser
        };
        db.Users.Add(staffUser);
        db.EmployeeProfiles.Add(staffProfile);
    }

    [Fact]
    public async Task GetDelinquentCases_CreatesCaseWithValidGraceOrDelinquentStatus_NeverOpen()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        await SeedFacilityAndManagerAsync(db, 1L, 10L);
        SeedCustomerAndStaff(db, 2L, 30L);

        var unit = new StorageUnit
        {
            Id = 6010L,
            FacilityId = 1L,
            UnitTypeId = 1L,
            UnitCode = "U-6010",
            PhysicalStatus = StorageUnitStatusConstants.Occupied,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.StorageUnits.Add(unit);

        var agreement = new RentalAgreement
        {
            Id = 60L,
            FacilityId = 1L,
            CustomerId = 2L,
            AgreementNo = "AGR-060",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-3)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)),
            Status = RentalAgreementStatusConstants.Active,
            MonthlyRateSnapshot = 1000m,
            DepositSnapshot = 1000m
        };
        db.RentalAgreements.Add(agreement);

        var allocation = new UnitAllocation
        {
            Id = 6001L,
            AgreementId = 60L,
            StorageUnitId = 6010L,
            AllocationKind = "primary",
            AllocationStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-3)),
            AllocationEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)),
            Status = AllocationStatusConstants.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.UnitAllocations.Add(allocation);

        var invoice = new Invoice
        {
            Id = 601L,
            AgreementId = 60L,
            CustomerId = 2L,
            InvoiceNo = "INV-601",
            Currency = "VND",
            TotalAmount = 500000m,
            PaidAmount = 0m,
            Status = InvoiceStatusConstants.Open,
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-20),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-20)
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);

        // Act
        var result = await service.GetDelinquentCasesAsync(1L, 10L, false);

        // Assert
        Assert.NotEmpty(result);
        var createdCase = await db.DelinquencyCases.FirstOrDefaultAsync(caseItem => caseItem.AgreementId == 60L);
        Assert.NotNull(createdCase);
        Assert.NotEqual("open", createdCase.Status);
        Assert.True(createdCase.Status == DelinquencyCaseStatusConstants.Grace || createdCase.Status == DelinquencyCaseStatusConstants.Delinquent);
    }

    [Fact]
    public async Task GetExpiringOrMoveOutAgreements_IgnoresCheckInInspection_OnlyConsidersCheckOut()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        await SeedFacilityAndManagerAsync(db, 1L, 10L);
        SeedCustomerAndStaff(db, 2L, 30L);

        var unit = new StorageUnit
        {
            Id = 7010L,
            FacilityId = 1L,
            UnitTypeId = 1L,
            UnitCode = "U-7010",
            PhysicalStatus = StorageUnitStatusConstants.Occupied,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.StorageUnits.Add(unit);

        // Active agreement 1: Far from expiring, has only check_in inspection
        var agr1 = new RentalAgreement
        {
            Id = 71L,
            FacilityId = 1L,
            CustomerId = 2L,
            AgreementNo = "AGR-071",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            Status = RentalAgreementStatusConstants.Active,
            MonthlyRateSnapshot = 1000m,
            DepositSnapshot = 1000m
        };

        // Active agreement 2: Far from expiring, but has check_out inspection
        var agr2 = new RentalAgreement
        {
            Id = 72L,
            FacilityId = 1L,
            CustomerId = 2L,
            AgreementNo = "AGR-072",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            Status = RentalAgreementStatusConstants.Active,
            MonthlyRateSnapshot = 1000m,
            DepositSnapshot = 1000m
        };

        db.RentalAgreements.AddRange(agr1, agr2);

        var insp1 = new Inspection
        {
            Id = 701L,
            AgreementId = 71L,
            FacilityId = 1L,
            InspectionType = "check_in",
            Status = "completed",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-10)
        };

        var insp2 = new Inspection
        {
            Id = 702L,
            AgreementId = 72L,
            FacilityId = 1L,
            InspectionType = "check_out",
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Inspections.AddRange(insp1, insp2);
        await db.SaveChangesAsync();

        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);

        // Act
        var result = await service.GetExpiringOrMoveOutAgreementsAsync(1L, 10L, false);

        // Assert
        Assert.DoesNotContain(result, r => r.Id == 71L);
        Assert.Contains(result, r => r.Id == 72L);
    }

    [Fact]
    public async Task GetFacilityTickets_FiltersInProgressAndPendingReviewCorrectly()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();
        await SeedFacilityAndManagerAsync(db, 1L, 10L);
        SeedCustomerAndStaff(db, 2L, 30L);

        var ticketAssigned = new SupportTicket
        {
            Id = 801L,
            FacilityId = 1L,
            CustomerId = 2L,
            TicketNo = "TCK-801",
            Subject = "Issue 1",
            Description = "Assigned to staff",
            Category = TicketCategoryConstants.Unit,
            Priority = TicketPriorityConstants.Normal,
            Status = TicketStatusConstants.Assigned,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var ticketInProgress = new SupportTicket
        {
            Id = 802L,
            FacilityId = 1L,
            CustomerId = 2L,
            TicketNo = "TCK-802",
            Subject = "Issue 2",
            Description = "Staff working on it",
            Category = TicketCategoryConstants.Unit,
            Priority = TicketPriorityConstants.Normal,
            Status = TicketStatusConstants.InProgress,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var ticketOpenUnassigned = new SupportTicket
        {
            Id = 803L,
            FacilityId = 1L,
            CustomerId = 2L,
            TicketNo = "TCK-803",
            Subject = "Issue 3",
            Description = "Waiting for manager review",
            Category = TicketCategoryConstants.Other,
            Priority = TicketPriorityConstants.Low,
            Status = TicketStatusConstants.Open,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var ticketOpenAssigned = new SupportTicket
        {
            Id = 804L,
            FacilityId = 1L,
            CustomerId = 2L,
            TicketNo = "TCK-804",
            Subject = "Issue 4",
            Description = "Has assignment",
            Category = TicketCategoryConstants.Other,
            Priority = TicketPriorityConstants.Low,
            Status = TicketStatusConstants.Open,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.SupportTickets.AddRange(ticketAssigned, ticketInProgress, ticketOpenUnassigned, ticketOpenAssigned);

        var assignment = new TicketAssignment
        {
            Id = 8040L,
            TicketId = 804L,
            EmployeeId = 30L,
            AssignedAt = DateTimeOffset.UtcNow
        };
        db.TicketAssignments.Add(assignment);

        await db.SaveChangesAsync();

        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);

        // Act & Assert 1: in_progress includes both assigned and in_progress
        var inProgressResult = await service.GetFacilityTicketsAsync(1L, "in_progress", null, 1, 50, 10L, false);

        Assert.Contains(inProgressResult.Items, t => t.Id == 801L);
        Assert.Contains(inProgressResult.Items, t => t.Id == 802L);
        Assert.DoesNotContain(inProgressResult.Items, t => t.Id == 803L);
        Assert.DoesNotContain(inProgressResult.Items, t => t.Id == 804L);

        // Act & Assert 2: pending_manager_review only includes open tickets with no assignment
        var pendingReviewResult = await service.GetFacilityTicketsAsync(1L, "pending_manager_review", null, 1, 50, 10L, false);

        Assert.Contains(pendingReviewResult.Items, t => t.Id == 803L);
        Assert.DoesNotContain(pendingReviewResult.Items, t => t.Id == 804L);
        Assert.DoesNotContain(pendingReviewResult.Items, t => t.Id == 801L);
        Assert.DoesNotContain(pendingReviewResult.Items, t => t.Id == 802L);
    }
    [Fact]
    public async Task AssignMoveOutInspection_IncludesMoveOutRequest_UpdatesStatusToInspectionPending()
    {
        using var db = CreateInMemoryDbContext();
        await SeedFacilityAndManagerAsync(db, 1L, 10L);
        SeedCustomerAndStaff(db, 2L, 30L);

        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment
        {
            Id = 300L,
            FacilityId = 1L,
            EmployeeId = 30L,
            AssignmentRole = RoleConstants.FacilityStaff,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-10),
            CreatedAt = DateTimeOffset.UtcNow
        });

        var unit = new StorageUnit
        {
            Id = 501L,
            FacilityId = 1L,
            UnitCode = "U-501",
            FloorLabel = "1",
            ZoneLabel = "A",
            PhysicalStatus = "rented",
            IsListed = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.StorageUnits.Add(unit);

        var agreement = new RentalAgreement
        {
            Id = 701L,
            AgreementNo = "AGR-701",
            FacilityId = 1L,
            CustomerId = 2L,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            Status = RentalAgreementStatusConstants.Active,
            MonthlyRateSnapshot = 1_000_000m,
            DepositSnapshot = 1_000_000m,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.RentalAgreements.Add(agreement);

        var moveOut = new MoveOutRequest
        {
            Id = 901L,
            AgreementId = 701L,
            RequestedBy = 2L,
            RequestedMoveOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            Status = MoveOutStatusConstants.Requested,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.MoveOutRequests.Add(moveOut);

        await db.SaveChangesAsync();

        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);

        var request = new ManagerAssignInspectionRequest
        {
            AgreementId = 701L,
            EmployeeId = 30L,
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(1),
            Notes = "Check inventory condition"
        };

        var result = await service.AssignMoveOutInspectionAsync(request, 10L, false);

        Assert.NotNull(result);
        var updatedMoveOut = await db.MoveOutRequests.FindAsync(901L);
        Assert.NotNull(updatedMoveOut);
        Assert.Equal(MoveOutStatusConstants.InspectionPending, updatedMoveOut.Status);
    }

    [Fact]
    public async Task CreateTicket_WithoutCustomerId_ResolvesValidCustomerProfileSafely()
    {
        using var db = CreateInMemoryDbContext();
        await SeedFacilityAndManagerAsync(db, 1L, 10L);
        SeedCustomerAndStaff(db, 2L, 30L);
        await db.SaveChangesAsync();

        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);

        var request = new ManagerCreateTicketRequest
        {
            FacilityId = 1L,
            CustomerId = null,
            Subject = "General Facility Maintenance",
            Description = "Repair broken lighting in Zone B",
            Category = "incident",
            Priority = "normal"
        };

        var ticket = await service.CreateTicketAsync(request, 10L, false);

        Assert.NotNull(ticket);
        Assert.True(ticket.CustomerId > 0);
        var customerExists = await db.CustomerProfiles.AnyAsync(c => c.UserId == ticket.CustomerId);
        Assert.True(customerExists);
    }

    [Fact]
    public async Task DecideRefundApproval_WhenApprovedAmountExceedsPayment_ThrowsAppException()
    {
        using var db = CreateInMemoryDbContext();
        await SeedFacilityAndManagerAsync(db, 1L, 10L);
        SeedCustomerAndStaff(db, 2L, 30L);

        var agreement = new RentalAgreement
        {
            Id = 702L,
            AgreementNo = "AGR-702",
            FacilityId = 1L,
            CustomerId = 2L,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            Status = RentalAgreementStatusConstants.Active,
            MonthlyRateSnapshot = 1_000_000m,
            DepositSnapshot = 1_000_000m,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.RentalAgreements.Add(agreement);

        var invoice = new Invoice
        {
            Id = 801L,
            InvoiceNo = "INV-801",
            CustomerId = 2L,
            AgreementId = 702L,
            Agreement = agreement,
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            Currency = "VND",
            SubtotalAmount = 1_000_000m,
            TaxAmount = 0m,
            TotalAmount = 1_000_000m,
            PaidAmount = 1_000_000m,
            Status = InvoiceStatusConstants.Paid,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Invoices.Add(invoice);

        var payment = new Payment
        {
            Id = 401L,
            CustomerId = 2L,
            TargetInvoiceId = 801L,
            TargetInvoice = invoice,
            Amount = 1_000_000m,
            Currency = "VND",
            Method = PaymentConstants.MethodBankTransfer,
            Provider = PaymentConstants.ProviderManual,
            IdempotencyKey = Guid.NewGuid().ToString(),
            Metadata = "{}",
            Status = PaymentConstants.StatusSucceeded,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Payments.Add(payment);

        var refund = new Refund
        {
            Id = 601L,
            PaymentId = 401L,
            AgreementId = 702L,
            Amount = 1_000_000m,
            Currency = "VND",
            Reason = "Return deposit",
            Provider = "bank_transfer",
            IdempotencyKey = Guid.NewGuid().ToString(),
            Status = RefundStatusConstants.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Refunds.Add(refund);

        await db.SaveChangesAsync();

        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);

        var request = new ManagerDecideRefundApprovalRequest
        {
            Decision = ApprovalDecisionConstants.Approved,
            OverrideAmount = 2_500_000m
        };

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.DecideRefundApprovalAsync(601L, request, 10L, false));

        Assert.Equal(ManagerErrors.RefundAmountExceedsPayment.Code, ex.Error.Code);
    }

    [Fact]
    public async Task GetExpiringOrMoveOutAgreements_IncludesEarlyMoveOutRequested_ExcludesCancelledMoveOut()
    {
        await using var db = CreateInMemoryDbContext();
        await SeedFacilityAndManagerAsync(db, facilityId: 1L, managerUserId: 10L);
        SeedCustomerAndStaff(db, 2L, 30L);

        var unitType = new UnitType
        {
            Id = 1L,
            Code = "UT-1",
            Name = "Small Box",
            WidthM = 2,
            LengthM = 2,
            HeightM = 2,
            AreaM2 = 4,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var unit1 = new StorageUnit
        {
            Id = 101L,
            FacilityId = 1L,
            UnitTypeId = 1L,
            UnitCode = "A-701",
            PhysicalStatus = StorageUnitStatusConstants.Occupied,
            IsListed = true,
            UnitType = unitType,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var unit2 = new StorageUnit
        {
            Id = 102L,
            FacilityId = 1L,
            UnitTypeId = 1L,
            UnitCode = "A-702",
            PhysicalStatus = StorageUnitStatusConstants.Occupied,
            IsListed = true,
            UnitType = unitType,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.UnitTypes.Add(unitType);
        db.StorageUnits.AddRange(unit1, unit2);

        var farFutureEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60));
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

        var earlyMoveOutAgr = new RentalAgreement
        {
            Id = 701L,
            FacilityId = 1L,
            CustomerId = 2L,
            AgreementNo = "AGR-701",
            Status = RentalAgreementStatusConstants.Active,
            MonthlyRateSnapshot = 1000000m,
            DepositSnapshot = 500000m,
            StartDate = startDate,
            EndDate = farFutureEnd,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var cancelledMoveOutAgr = new RentalAgreement
        {
            Id = 702L,
            FacilityId = 1L,
            CustomerId = 2L,
            AgreementNo = "AGR-702",
            Status = RentalAgreementStatusConstants.Active,
            MonthlyRateSnapshot = 1000000m,
            DepositSnapshot = 500000m,
            StartDate = startDate,
            EndDate = farFutureEnd,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.RentalAgreements.AddRange(earlyMoveOutAgr, cancelledMoveOutAgr);

        db.MoveOutRequests.AddRange(
            new MoveOutRequest
            {
                Id = 711L,
                AgreementId = 701L,
                RequestedBy = 2L,
                RequestedMoveOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
                Status = MoveOutStatusConstants.Requested,
                Agreement = earlyMoveOutAgr,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new MoveOutRequest
            {
                Id = 712L,
                AgreementId = 702L,
                RequestedBy = 2L,
                RequestedMoveOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
                Status = MoveOutStatusConstants.Cancelled,
                Agreement = cancelledMoveOutAgr,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        await db.SaveChangesAsync();

        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);
        var result = await service.GetExpiringOrMoveOutAgreementsAsync(1L, 10L, false);

        Assert.Single(result);
        Assert.Equal(701L, result[0].Id);
    }

    [Fact]
    public async Task DecideTransferRequest_ValidatesStatusAndDecision_AndRecordsUnitStatusHistories()
    {
        await using var db = CreateInMemoryDbContext();
        await SeedFacilityAndManagerAsync(db, facilityId: 1L, managerUserId: 10L);
        SeedCustomerAndStaff(db, 2L, 30L);

        var unitType = new UnitType
        {
            Id = 1L,
            Code = "UT-1",
            Name = "Small Box",
            WidthM = 2,
            LengthM = 2,
            HeightM = 2,
            AreaM2 = 4,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var fromUnit = new StorageUnit
        {
            Id = 801L,
            FacilityId = 1L,
            UnitTypeId = 1L,
            UnitCode = "U-801",
            PhysicalStatus = StorageUnitStatusConstants.Occupied,
            IsListed = true,
            UnitType = unitType,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var targetUnit = new StorageUnit
        {
            Id = 802L,
            FacilityId = 1L,
            UnitTypeId = 1L,
            UnitCode = "U-802",
            PhysicalStatus = StorageUnitStatusConstants.Available,
            IsListed = true,
            UnitType = unitType,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.UnitTypes.Add(unitType);
        db.StorageUnits.AddRange(fromUnit, targetUnit);

        var agreement = new RentalAgreement
        {
            Id = 810L,
            FacilityId = 1L,
            CustomerId = 2L,
            AgreementNo = "AGR-810",
            Status = RentalAgreementStatusConstants.Active,
            MonthlyRateSnapshot = 1000000m,
            DepositSnapshot = 500000m,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(50)),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.RentalAgreements.Add(agreement);

        db.UnitAllocations.Add(new UnitAllocation
        {
            Id = 820L,
            AgreementId = 810L,
            StorageUnitId = 801L,
            AllocationKind = "agreement",
            AllocationStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            AllocationEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(50)),
            Status = "active",
            Agreement = agreement,
            StorageUnit = fromUnit,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var transferReq = new UnitTransferRequest
        {
            Id = 830L,
            AgreementId = 810L,
            RequestedUnitTypeId = 1L,
            RequestedFacilityId = 1L,
            FromUnitId = 801L,
            ToUnitId = 802L,
            RequestedEffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MoveOutDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MoveInDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Reason = "Need closer unit",
            Status = TransferRequestStatusConstants.Pending,
            RequestedBy = 2L,
            Agreement = agreement,
            FromUnit = fromUnit,
            ToUnit = targetUnit,
            RequestedUnitType = unitType,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.UnitTransferRequests.Add(transferReq);
        await db.SaveChangesAsync();

        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);

        var invalidDecisionEx = await Assert.ThrowsAsync<AppValidationException>(() =>
            service.DecideTransferRequestAsync(
                830L,
                new ManagerDecideTransferRequest { Decision = "invalid_decision" },
                10L,
                false));
        Assert.Equal(ManagerErrors.InvalidDecision.Code, invalidDecisionEx.Error.Code);

        var approveResult = await service.DecideTransferRequestAsync(
            830L,
            new ManagerDecideTransferRequest
            {
                Decision = ApprovalDecisionConstants.Approved,
                TargetUnitId = 802L
            },
            10L,
            false);

        Assert.Equal(TransferRequestStatusConstants.Completed, approveResult.Status);
        Assert.Equal(StorageUnitStatusConstants.Maintenance, fromUnit.PhysicalStatus);
        Assert.Equal(StorageUnitStatusConstants.Occupied, targetUnit.PhysicalStatus);

        var histories = await db.UnitStatusHistories.OrderBy(h => h.StorageUnitId).ToListAsync();
        Assert.Equal(2, histories.Count);
        Assert.Equal(801L, histories[0].StorageUnitId);
        Assert.Equal(StorageUnitStatusConstants.Occupied, histories[0].OldStatus);
        Assert.Equal(StorageUnitStatusConstants.Maintenance, histories[0].NewStatus);
        Assert.Equal(802L, histories[1].StorageUnitId);
        Assert.Equal(StorageUnitStatusConstants.Available, histories[1].OldStatus);
        Assert.Equal(StorageUnitStatusConstants.Occupied, histories[1].NewStatus);

        var alreadyCompletedEx = await Assert.ThrowsAsync<AppConflictException>(() =>
            service.DecideTransferRequestAsync(
                830L,
                new ManagerDecideTransferRequest { Decision = ApprovalDecisionConstants.Rejected },
                10L,
                false));
        Assert.Equal(ManagerErrors.TransferRequestNotPending.Code, alreadyCompletedEx.Error.Code);
    }

    [Fact]
    public async Task DecideTransferRequest_CrossFacility_UpdatesAgreementFacilityId_WithoutKeyException()
    {
        await using var db = CreateInMemoryDbContext();
        await SeedFacilityAndManagerAsync(db, facilityId: 1L, managerUserId: 10L);

        db.Facilities.Add(new Facility
        {
            Id = 2L,
            Code = "FAC-2",
            Name = "Facility 2",
            Status = "active",
            AddressLine = "456 St",
            District = "D2",
            City = "HCM",
            Timezone = "Asia/Ho_Chi_Minh",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        db.StaffFacilityAssignments.Add(new StaffFacilityAssignment
        {
            Id = 2L,
            EmployeeId = 10L,
            FacilityId = 2L,
            AssignmentRole = RoleConstants.FacilityManager,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-10),
            EndsAt = null,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var custUser = new User
        {
            Id = 2L,
            Email = "cust2@test.com",
            PasswordHash = "h",
            PhoneNumber = "0909123456",
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var custProfile = new CustomerProfile
        {
            UserId = 2L,
            FullName = "Customer Cross",
            User = custUser,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(custUser);
        db.CustomerProfiles.Add(custProfile);

        var unitType = new UnitType
        {
            Id = 1L,
            Code = "UT-1",
            Name = "Small Box",
            WidthM = 2,
            LengthM = 2,
            HeightM = 2,
            AreaM2 = 4,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var fromUnit = new StorageUnit
        {
            Id = 901L,
            FacilityId = 1L,
            UnitTypeId = 1L,
            UnitCode = "F1-901",
            PhysicalStatus = StorageUnitStatusConstants.Occupied,
            IsListed = true,
            UnitType = unitType,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var targetUnit = new StorageUnit
        {
            Id = 902L,
            FacilityId = 2L,
            UnitTypeId = 1L,
            UnitCode = "F2-902",
            PhysicalStatus = StorageUnitStatusConstants.Available,
            IsListed = true,
            UnitType = unitType,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.UnitTypes.Add(unitType);
        db.StorageUnits.AddRange(fromUnit, targetUnit);

        var agreement = new RentalAgreement
        {
            Id = 910L,
            FacilityId = 1L,
            CustomerId = 2L,
            AgreementNo = "AGR-910",
            Status = RentalAgreementStatusConstants.Active,
            MonthlyRateSnapshot = 1000000m,
            DepositSnapshot = 500000m,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(50)),
            Customer = custProfile,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.RentalAgreements.Add(agreement);

        var ticket = new SupportTicket
        {
            Id = 950L,
            TicketNo = "TCK-950",
            CustomerId = 2L,
            FacilityId = 1L,
            AgreementId = 910L,
            StorageUnitId = 901L,
            Category = "unit",
            Priority = "normal",
            Subject = "Cross facility agreement ticket",
            Description = "Test composite FK sync",
            Status = TicketStatusConstants.Open,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.SupportTickets.Add(ticket);

        db.UnitAllocations.Add(new UnitAllocation
        {
            Id = 920L,
            AgreementId = 910L,
            StorageUnitId = 901L,
            AllocationKind = "rental",
            AllocationStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            AllocationEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(50)),
            Status = "active",
            Agreement = agreement,
            StorageUnit = fromUnit,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var transferReq = new UnitTransferRequest
        {
            Id = 930L,
            AgreementId = 910L,
            RequestedUnitTypeId = 1L,
            RequestedFacilityId = 2L,
            FromUnitId = 901L,
            ToUnitId = 902L,
            RequestedEffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MoveOutDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MoveInDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Reason = "Move to Facility 2",
            Status = TransferRequestStatusConstants.Pending,
            RequestedBy = 2L,
            Agreement = agreement,
            FromUnit = fromUnit,
            ToUnit = targetUnit,
            RequestedUnitType = unitType,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.UnitTransferRequests.Add(transferReq);
        await db.SaveChangesAsync();

        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);

        var approveResult = await service.DecideTransferRequestAsync(
            930L,
            new ManagerDecideTransferRequest
            {
                Decision = ApprovalDecisionConstants.Approved,
                TargetUnitId = 902L
            },
            10L,
            false);

        Assert.Equal(TransferRequestStatusConstants.Completed, approveResult.Status);
        Assert.Equal(2L, agreement.FacilityId);
        Assert.Equal(2L, ticket.FacilityId);
        Assert.Equal(902L, ticket.StorageUnitId);
    }

    [Fact]
    public async Task GetUnitsAsync_SearchIgnoresEmbeddedImageUrl_MatchesActualCleanNotes()
    {
        await using var db = CreateInMemoryDbContext();
        await SeedFacilityAndManagerAsync(db, facilityId: 1L, managerUserId: 10L);

        var unitType = new UnitType
        {
            Id = 1L,
            Code = "UT-1",
            Name = "Small Box",
            WidthM = 2,
            LengthM = 2,
            HeightM = 2,
            AreaM2 = 4,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.UnitTypes.Add(unitType);

        db.StorageUnits.AddRange(
            new StorageUnit
            {
                Id = 961L,
                FacilityId = 1L,
                UnitTypeId = 1L,
                UnitCode = "A-101",
                FloorLabel = "Tầng 1",
                ZoneLabel = "Khu A",
                PhysicalStatus = StorageUnitStatusConstants.Available,
                IsListed = true,
                Notes = "[IMG:https://cdn.example.com/units/box1.png] Kệ gỗ chống ẩm",
                UnitType = unitType,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new StorageUnit
            {
                Id = 962L,
                FacilityId = 1L,
                UnitTypeId = 1L,
                UnitCode = "A-102",
                FloorLabel = "Tầng 1",
                ZoneLabel = "Khu A",
                PhysicalStatus = StorageUnitStatusConstants.Available,
                IsListed = true,
                Notes = "[IMG:https://cdn.example.com/units/box2.png]",
                UnitType = unitType,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        await db.SaveChangesAsync();

        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);

        var byHttp = await service.GetUnitsAsync(1L, null, null, "http", 1, 20, 10L, false);
        Assert.Empty(byHttp.Items);
        Assert.Equal(0, byHttp.TotalCount);

        var byImg = await service.GetUnitsAsync(1L, null, null, "img", 1, 20, 10L, false);
        Assert.Empty(byImg.Items);
        Assert.Equal(0, byImg.TotalCount);

        var byPng = await service.GetUnitsAsync(1L, null, null, "png", 1, 20, 10L, false);
        Assert.Empty(byPng.Items);
        Assert.Equal(0, byPng.TotalCount);

        var byRealNote = await service.GetUnitsAsync(1L, null, null, "chống ẩm", 1, 20, 10L, false);
        Assert.Single(byRealNote.Items);
        Assert.Equal(1, byRealNote.TotalCount);
        Assert.Equal("A-101", byRealNote.Items.First().UnitCode);
        Assert.Equal("Kệ gỗ chống ẩm", byRealNote.Items.First().Notes);
    }

    [Fact]
    public async Task GetDelinquentCases_WhenInvoiceOverdue30Days_SetsGraceEndsAtGreaterOrEqualOpenedAt()
    {
        await using var db = CreateInMemoryDbContext();
        await SeedFacilityAndManagerAsync(db, facilityId: 1L, managerUserId: 10L);
        SeedCustomerAndStaff(db, 2L, 30L);

        var agreement = new RentalAgreement
        {
            Id = 970L,
            FacilityId = 1L,
            CustomerId = 2L,
            AgreementNo = "AGR-970",
            Status = RentalAgreementStatusConstants.Overdue,
            MonthlyRateSnapshot = 1200000m,
            DepositSnapshot = 600000m,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-60),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.RentalAgreements.Add(agreement);

        var overdueInvoice = new Invoice
        {
            Id = 971L,
            InvoiceNo = "INV-971",
            AgreementId = 970L,
            CustomerId = 2L,
            Currency = "VND",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-35)),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            SubtotalAmount = 1200000m,
            TotalAmount = 1200000m,
            PaidAmount = 200000m,
            Status = InvoiceStatusConstants.Overdue,
            Agreement = agreement,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-35),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Invoices.Add(overdueInvoice);
        await db.SaveChangesAsync();

        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);
        var cases = await service.GetDelinquentCasesAsync(1L, 10L, false);

        Assert.Single(cases);
        var createdCase = await db.DelinquencyCases.SingleAsync(dc => dc.AgreementId == 970L);
        Assert.True(createdCase.GraceEndsAt >= createdCase.OpenedAt, "GraceEndsAt must be >= OpenedAt to satisfy SQL CHECK constraint.");
        Assert.Equal(1000000m, createdCase.OutstandingSnapshot);
        Assert.Equal(DelinquencyCaseStatusConstants.Delinquent, createdCase.Status);
    }

    [Fact]
    public async Task UpdateUnitAsync_WhenClearingNotesAndImageUrl_RemovesBothCleanly()
    {
        await using var db = CreateInMemoryDbContext();
        await SeedFacilityAndManagerAsync(db, facilityId: 1L, managerUserId: 10L);

        var unitType = new UnitType
        {
            Id = 1L,
            Code = "UT-1",
            Name = "Small Box",
            WidthM = 2,
            LengthM = 2,
            HeightM = 2,
            AreaM2 = 4,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var unit = new StorageUnit
        {
            Id = 980L,
            FacilityId = 1L,
            UnitTypeId = 1L,
            UnitCode = "A-980",
            FloorLabel = "1",
            ZoneLabel = "A",
            PhysicalStatus = StorageUnitStatusConstants.Available,
            IsListed = true,
            Notes = "[IMG:https://cdn.example.com/units/box980.png] Ghi chú ban đầu",
            UnitType = unitType,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.UnitTypes.Add(unitType);
        db.StorageUnits.Add(unit);
        await db.SaveChangesAsync();

        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);

        // 1. Quick toggle simulation: sending both existing notes and imageUrl preserves both
        var preservedDto = await service.UpdateUnitAsync(
            980L,
            new ManagerUpdateStorageUnitRequest
            {
                UnitCode = "A-980",
                UnitTypeId = 1L,
                IsListed = false,
                Notes = "Ghi chú ban đầu",
                ImageUrl = "https://cdn.example.com/units/box980.png"
            },
            10L,
            false);
        Assert.Equal("Ghi chú ban đầu", preservedDto.Notes);
        Assert.Equal("https://cdn.example.com/units/box980.png", preservedDto.ImageUrl);

        // 2. Clearing both notes and imageUrl with empty strings removes both
        var clearedDto = await service.UpdateUnitAsync(
            980L,
            new ManagerUpdateStorageUnitRequest
            {
                UnitCode = "A-980",
                UnitTypeId = 1L,
                IsListed = true,
                Notes = "",
                ImageUrl = ""
            },
            10L,
            false);
        Assert.Null(clearedDto.Notes);
        Assert.Null(clearedDto.ImageUrl);
        Assert.Null(unit.Notes);
    }

    [Fact]
    public async Task StorageLifecycleBackgroundService_SuspendExpiredGrace_WritesValidJsonAndNetBalance()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<SelfStorageDbContext>(opts =>
            opts.UseInMemoryDatabase(dbName)
                .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StorageLifecycleWorker:Enabled"] = "true",
                ["StorageLifecycleWorker:IntervalSeconds"] = "60"
            })
            .Build();

        await using var provider = services.BuildServiceProvider();
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
            await SeedFacilityAndManagerAsync(db, facilityId: 1L, managerUserId: 10L);
            SeedCustomerAndStaff(db, 2L, 30L);

            var agr1 = new RentalAgreement
            {
                Id = 991L,
                FacilityId = 1L,
                CustomerId = 2L,
                AgreementNo = "AGR-991",
                Status = RentalAgreementStatusConstants.Active,
                MonthlyRateSnapshot = 1000000m,
                DepositSnapshot = 500000m,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)),
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-20),
                UpdatedAt = DateTimeOffset.UtcNow
            };
            var inv1 = new Invoice
            {
                Id = 992L,
                InvoiceNo = "INV-992",
                AgreementId = 991L,
                CustomerId = 2L,
                Currency = "VND",
                IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
                SubtotalAmount = 1000000m,
                TotalAmount = 1000000m,
                PaidAmount = 400000m,
                Status = InvoiceStatusConstants.Open,
                Agreement = agr1,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var agr2 = new RentalAgreement
            {
                Id = 993L,
                FacilityId = 1L,
                CustomerId = 2L,
                AgreementNo = "AGR-993",
                Status = RentalAgreementStatusConstants.Overdue,
                MonthlyRateSnapshot = 1000000m,
                DepositSnapshot = 500000m,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-40)),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)),
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-40),
                UpdatedAt = DateTimeOffset.UtcNow
            };
            var inv2 = new Invoice
            {
                Id = 994L,
                InvoiceNo = "INV-994",
                AgreementId = 993L,
                CustomerId = 2L,
                Currency = "VND",
                IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)),
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
                SubtotalAmount = 1000000m,
                TotalAmount = 1000000m,
                PaidAmount = 0m,
                Status = InvoiceStatusConstants.Overdue,
                Agreement = agr2,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-20),
                UpdatedAt = DateTimeOffset.UtcNow
            };
            var dCase2 = new DelinquencyCase
            {
                Id = 995L,
                AgreementId = 993L,
                InvoiceId = 994L,
                OutstandingSnapshot = 1000000m,
                OpenedAt = DateTimeOffset.UtcNow.AddDays(-9),
                GraceEndsAt = DateTimeOffset.UtcNow.AddDays(-1),
                Status = DelinquencyCaseStatusConstants.Grace,
                Agreement = agr2,
                Invoice = inv2
            };
            var cred2 = new AccessCredential
            {
                Id = 996L,
                AgreementId = 993L,
                CredentialType = CredentialTypeConstants.Pin,
                SecretDigest = "hash",
                Status = CredentialStatusConstants.Active,
                IssuedAt = DateTimeOffset.UtcNow.AddDays(-40),
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-40)
            };

            db.RentalAgreements.AddRange(agr1, agr2);
            db.Invoices.AddRange(inv1, inv2);
            db.DelinquencyCases.Add(dCase2);
            db.AccessCredentials.Add(cred2);
            await db.SaveChangesAsync();
        }

        var worker = new StorageLifecycleBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            config,
            NullLogger<StorageLifecycleBackgroundService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(150);
        await worker.StopAsync(CancellationToken.None);

        await using var verifyScope = provider.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();

        var newCaseForAgr1 = await verifyDb.DelinquencyCases.SingleAsync(dc => dc.AgreementId == 991L);
        Assert.Equal(600000m, newCaseForAgr1.OutstandingSnapshot);

        var updatedCase2 = await verifyDb.DelinquencyCases.SingleAsync(dc => dc.Id == 995L);
        Assert.Equal(DelinquencyCaseStatusConstants.TerminationStarted, updatedCase2.Status);

        var updatedCred2 = await verifyDb.AccessCredentials.SingleAsync(ac => ac.Id == 996L);
        Assert.Equal(CredentialStatusConstants.Suspended, updatedCred2.Status);

        var action = await verifyDb.DelinquencyActions.SingleAsync(da => da.DelinquencyCaseId == 995L);
        Assert.NotNull(action.Details);
        using var jsonDoc = JsonDocument.Parse(action.Details!);
        Assert.True(jsonDoc.RootElement.GetProperty("system").GetBoolean());
        Assert.Contains("Grace period expired", jsonDoc.RootElement.GetProperty("note").GetString());
    }

}
