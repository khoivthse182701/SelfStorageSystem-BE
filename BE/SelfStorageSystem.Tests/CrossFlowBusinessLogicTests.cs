using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Customer.Payments;
using SelfStorageSystem.Contracts.Customer.Rentals;
using SelfStorageSystem.Contracts.Customer.SupportTickets;
using SelfStorageSystem.Contracts.Manager;
using SelfStorageSystem.Contracts.Staff;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.BackgroundJobs;
using SelfStorageSystem.Infrastructure.Common;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;
using Xunit;

namespace SelfStorageSystem.Tests;

public class CrossFlowBusinessLogicTests
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
    public async Task Task1_ExpiredAgreement_WithoutDebt_DoesNotCreateDelinquencyCase_AndTransitionsToExpiringSoon()
    {
        using var db = CreateInMemoryDbContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTimeOffset.UtcNow;

        var agreement = new RentalAgreement
        {
            Id = 101,
            AgreementNo = "AGR-EXP-101",
            CustomerId = 1,
            FacilityId = 1,
            Status = RentalAgreementStatusConstants.Active,
            StartDate = today.AddMonths(-1),
            EndDate = today.AddDays(-1), // Expired
            DepositBalance = 2000000m,
            DepositSnapshot = 2000000m,
            MonthlyRateSnapshot = 1000000m,
            CreatedAt = now.AddMonths(-1),
            UpdatedAt = now.AddMonths(-1)
        };

        // Fully paid invoice (Total = Paid)
        var paidInvoice = new Invoice
        {
            Id = 201,
            InvoiceNo = "INV-PAID-201",
            Currency = "VND",
            BillingPeriod = "2026-10",
            AgreementId = agreement.Id,
            CustomerId = 1,
            TotalAmount = 1000000m,
            PaidAmount = 1000000m,
            Status = InvoiceStatusConstants.Paid,
            DueDate = today.AddDays(-5),
            CreatedAt = now.AddMonths(-1),
            UpdatedAt = now.AddMonths(-1)
        };

        db.RentalAgreements.Add(agreement);
        db.Invoices.Add(paidInvoice);
        await db.SaveChangesAsync();

        // Simulate StorageLifecycleBackgroundService logic for overdue/expiring
        var overdueAgreements = await db.RentalAgreements
            .Include(a => a.Invoices)
            .Where(a => a.Status == RentalAgreementStatusConstants.Active || a.Status == RentalAgreementStatusConstants.ExpiringSoon)
            .Where(a => a.Invoices.Any(i =>
                i.Status == InvoiceStatusConstants.Overdue &&
                (i.TotalAmount - i.PaidAmount) > 0 &&
                (i.BillingPeriod == null || !i.BillingPeriod.StartsWith(TransferConstants.BillingPeriodPrefix)) &&
                (i.InvoiceNo == null || !i.InvoiceNo.StartsWith(TransferConstants.InvoicePrefix))))
            .Where(a => !a.DelinquencyCases.Any(c => c.Status == DelinquencyCaseStatusConstants.Delinquent || c.Status == DelinquencyCaseStatusConstants.Grace || c.Status == DelinquencyCaseStatusConstants.TerminationStarted))
            .ToListAsync();

        Assert.Empty(overdueAgreements);

        var expiringAgreements = await db.RentalAgreements
            .Include(a => a.Invoices)
            .Where(a => a.Status == RentalAgreementStatusConstants.Active && a.EndDate <= today)
            .Where(a => !a.Invoices.Any(i => i.Status == InvoiceStatusConstants.Overdue && (i.TotalAmount - i.PaidAmount) > 0))
            .ToListAsync();

        Assert.Single(expiringAgreements);
        foreach (var ag in expiringAgreements)
        {
            ag.Status = RentalAgreementStatusConstants.ExpiringSoon;
        }
        await db.SaveChangesAsync();

        // Verify: No delinquency case created, agreement is ExpiringSoon
        var cases = await db.DelinquencyCases.Where(c => c.AgreementId == agreement.Id).ToListAsync();
        Assert.Empty(cases);
        Assert.Equal(RentalAgreementStatusConstants.ExpiringSoon, agreement.Status);
    }

    [Fact]
    public async Task Task2_ManagerDecideRefundApproval_AutoReassignsPayment_WhenAssignedPaymentIsSmallerThanApprovedAmount()
    {
        using var db = CreateInMemoryDbContext();
        var now = DateTimeOffset.UtcNow;

        var facility = new Facility
        {
            Id = 1,
            Code = "FAC-01",
            Name = "Facility 1",
            AddressLine = "123 Street",
            City = "Hanoi",
            Status = "active",
            Timezone = "Asia/Ho_Chi_Minh",
            CreatedAt = now,
            UpdatedAt = now
        };

        var agreement = new RentalAgreement
        {
            Id = 301,
            AgreementNo = "AGR-REF-301",
            CustomerId = 1,
            FacilityId = facility.Id,
            Facility = facility,
            Status = RentalAgreementStatusConstants.Completed,
            DepositSnapshot = 2000000m,
            DepositBalance = 2000000m,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Big deposit payment (2M)
        var depInvoice = new Invoice
        {
            Id = 401,
            InvoiceNo = "INV-DEP-401",
            Currency = "VND",
            BillingPeriod = "2026-10",
            AgreementId = agreement.Id,
            CustomerId = 1,
            TotalAmount = 2000000m,
            PaidAmount = 2000000m,
            Status = InvoiceStatusConstants.Paid,
            CreatedAt = now,
            UpdatedAt = now
        };
        var depositPayment = new Payment
        {
            Id = 501,
            CustomerId = 1,
            TargetInvoiceId = depInvoice.Id,
            TargetInvoice = depInvoice,
            Amount = 2000000m,
            Currency = "VND",
            Method = "bank_transfer",
            Provider = "bank_transfer",
            IdempotencyKey = "key-dep-501",
            Metadata = "{}",
            Status = PaymentConstants.StatusSucceeded,
            CreatedAt = now.AddDays(-10),
            UpdatedAt = now.AddDays(-10)
        };

        // Small 50k fee payment
        var feeInvoice = new Invoice
        {
            Id = 402,
            InvoiceNo = "INV-FEE-402",
            Currency = "VND",
            BillingPeriod = "2026-11",
            AgreementId = agreement.Id,
            CustomerId = 1,
            TotalAmount = 50000m,
            PaidAmount = 50000m,
            Status = InvoiceStatusConstants.Paid,
            CreatedAt = now,
            UpdatedAt = now
        };
        var feePayment = new Payment
        {
            Id = 502,
            CustomerId = 1,
            TargetInvoiceId = feeInvoice.Id,
            TargetInvoice = feeInvoice,
            Amount = 50000m,
            Currency = "VND",
            Method = "bank_transfer",
            Provider = "bank_transfer",
            IdempotencyKey = "key-fee-502",
            Metadata = "{}",
            Status = PaymentConstants.StatusSucceeded,
            CreatedAt = now.AddDays(-1),
            UpdatedAt = now.AddDays(-1)
        };

        // Refund accidentally assigned to feePayment (50k)
        var refund = new Refund
        {
            Id = 601,
            AgreementId = agreement.Id,
            Agreement = agreement,
            PaymentId = feePayment.Id,
            Payment = feePayment,
            Amount = 2000000m,
            Currency = "VND",
            Reason = "Hoàn cọc trả kho",
            Provider = "manual",
            IdempotencyKey = "key-ref-601",
            Status = RefundStatusConstants.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        var managerUser = new User
        {
            Id = 1,
            Email = "manager1@storage.local",
            Status = UserStatusConstants.Active,
            PasswordHash = "hash"
        };
        var managerEmp = new EmployeeProfile
        {
            UserId = 1,
            EmployeeCode = "EMP-001",
            FullName = "Manager One",
            EmploymentStatus = "active"
        };

        db.Users.Add(managerUser);
        db.EmployeeProfiles.Add(managerEmp);
        db.Facilities.Add(facility);
        db.RentalAgreements.Add(agreement);
        db.Invoices.AddRange(depInvoice, feeInvoice);
        db.Payments.AddRange(depositPayment, feePayment);
        db.Refunds.Add(refund);
        await db.SaveChangesAsync();

        var service = new FacilityManagerService(db, NullLogger<FacilityManagerService>.Instance);

        var request = new ManagerDecideRefundApprovalRequest
        {
            Decision = "approved",
            OverrideAmount = 2000000m,
            Reason = "Hoàn đủ tiền cọc"
        };

        // Act: Manager approves 2,000,000 VND
        var result = await service.DecideRefundApprovalAsync(refund.Id, request, 1L, true, CancellationToken.None);

        // Assert: Approved successfully, PaymentId automatically reassigned to deposit payment (501)
        Assert.NotNull(result);
        Assert.Equal("approved", result.Decision);
        var updatedRefund = await db.Refunds.FindAsync(refund.Id);
        Assert.NotNull(updatedRefund);
        Assert.Equal(depositPayment.Id, updatedRefund.PaymentId);
    }

    [Fact]
    public async Task Task3_RenewalPayment_RestoresExpiringSoonToActive_AndSyncsAllocationEndDate()
    {
        using var db = CreateInMemoryDbContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTimeOffset.UtcNow;

        var agreement = new RentalAgreement
        {
            Id = 701,
            AgreementNo = "AGR-RNW-701",
            CustomerId = 10,
            FacilityId = 1,
            Status = RentalAgreementStatusConstants.ExpiringSoon,
            StartDate = today.AddMonths(-1),
            EndDate = today,
            MonthlyRateSnapshot = 1500000m,
            DepositSnapshot = 1500000m,
            CreatedAt = now.AddMonths(-1),
            UpdatedAt = now.AddMonths(-1)
        };

        var unitAlloc = new UnitAllocation
        {
            Id = 801,
            AgreementId = agreement.Id,
            StorageUnitId = 901,
            AllocationKind = "rental",
            Status = AllocationStatusConstants.Active,
            AllocationStartDate = today.AddMonths(-1),
            AllocationEndDate = today,
            CreatedAt = now.AddMonths(-1)
        };

        var newEndDate = today.AddMonths(3);
        var renewal = new RentalRenewal
        {
            Id = 1,
            AgreementId = agreement.Id,
            Agreement = agreement,
            RequestedBy = 10,
            OldEndDate = today,
            RequestedEndDate = newEndDate,
            OldMonthlyRate = 1500000m,
            NewMonthlyRate = 1500000m,
            Status = RenewalStatusConstants.PendingPayment,
            CreatedAt = now
        };

        db.RentalAgreements.Add(agreement);
        db.UnitAllocations.Add(unitAlloc);
        db.RentalRenewals.Add(renewal);
        await db.SaveChangesAsync();

        var paymentService = new CustomerPaymentService(
            db,
            Mock.Of<IEmailService>(),
            Options.Create(new PaymentSettings()),
            NullLogger<CustomerPaymentService>.Instance);

        var payload = new SePayWebhookPayload
        {
            Id = 99999,
            Content = $"RNW1 Thann toan gia han",
            TransferAmount = 4500000m,
            ReferenceCode = "RNW1-REF"
        };

        // Act: Process webhook
        var processed = await paymentService.ProcessSePayWebhookAsync(payload, CancellationToken.None);

        // Assert
        Assert.True(processed);
        Assert.Equal(RentalAgreementStatusConstants.Active, agreement.Status);
        Assert.Equal(newEndDate, agreement.EndDate);
        Assert.Equal(newEndDate, unitAlloc.AllocationEndDate);
    }

    [Fact]
    public async Task Task5_CustomerSupportTickets_ExcludesInternalMaintenanceTickets()
    {
        using var db = CreateInMemoryDbContext();
        var now = DateTimeOffset.UtcNow;

        var facility = new Facility
        {
            Id = 1,
            Code = "FAC-01",
            Name = "Facility 01",
            AddressLine = "123 Storage St",
            City = "City",
            Status = "active",
            Timezone = "Asia/Ho_Chi_Minh",
            CreatedAt = now,
            UpdatedAt = now
        };

        var normalTicket = new SupportTicket
        {
            Id = 1001,
            TicketNo = "TCK-1001",
            CustomerId = 5,
            FacilityId = 1,
            Category = TicketCategoryConstants.Payment,
            Priority = TicketPriorityConstants.Normal,
            Subject = "Hỏi về hóa đơn tháng 11",
            Description = "Tôi có câu hỏi về phí phụ",
            Status = TicketStatusConstants.Open,
            CreatedAt = now,
            UpdatedAt = now
        };

        var internalTicket = new SupportTicket
        {
            Id = 1002,
            TicketNo = "TCK-1002",
            CustomerId = 5,
            FacilityId = 1,
            Category = MaintenanceCategoryConstants.Cleaning,
            Priority = TicketPriorityConstants.Normal,
            Subject = "[INTERNAL] Dọn vệ sinh ô kho A-101 sau khi khách trả kho",
            Description = "Dọn kho",
            Status = TicketStatusConstants.Open,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Facilities.Add(facility);
        db.SupportTickets.AddRange(normalTicket, internalTicket);
        await db.SaveChangesAsync();

        var ticketService = new CustomerSupportTicketService(db);

        // Act: Customer queries their tickets
        var customerTickets = await ticketService.GetCustomerTicketsAsync(5, null, CancellationToken.None);

        // Assert: Customer only sees normal customer ticket, internal ticket is excluded
        Assert.Single(customerTickets);
        Assert.Equal(normalTicket.Id, customerTickets[0].Id);
    }

    [Fact]
    public async Task Task6_CancelReservation_Confirmed_ReleasesUnit_CancelsDraftAgreement_AndCreatesRefund()
    {
        using var db = CreateInMemoryDbContext();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var unit = new StorageUnit
        {
            Id = 10,
            FacilityId = 1,
            UnitTypeId = 1,
            UnitCode = "U-10",
            PhysicalStatus = StorageUnitStatusConstants.Reserved,
            CreatedAt = now,
            UpdatedAt = now
        };

        var reservation = new Reservation
        {
            Id = 20,
            ReservationCode = "RSV-20",
            CustomerId = 5,
            FacilityId = 1,
            UnitTypeId = 1,
            FacilityRateId = 1,
            Status = ReservationStatusConstants.Confirmed,
            StartDate = today.AddDays(5),
            EndDate = today.AddMonths(1),
            CreatedAt = now,
            UpdatedAt = now
        };

        var unitAlloc = new UnitAllocation
        {
            Id = 30,
            ReservationId = reservation.Id,
            StorageUnitId = unit.Id,
            StorageUnit = unit,
            AllocationKind = "rental",
            Status = AllocationStatusConstants.Active,
            CreatedAt = now
        };
        reservation.UnitAllocation = unitAlloc;

        var draftAgreement = new RentalAgreement
        {
            Id = 40,
            AgreementNo = "AGR-RSV-20",
            CustomerId = 5,
            FacilityId = 1,
            ReservationId = reservation.Id,
            Status = RentalAgreementStatusConstants.Draft,
            StartDate = today.AddDays(5),
            EndDate = today.AddMonths(1),
            CreatedAt = now,
            UpdatedAt = now
        };

        var paidInvoice = new Invoice
        {
            Id = 50,
            InvoiceNo = "INV-RSV-50",
            Currency = "VND",
            BillingPeriod = "2026-11",
            ReservationId = reservation.Id,
            CustomerId = 5,
            TotalAmount = 2000000m,
            PaidAmount = 2000000m,
            Status = InvoiceStatusConstants.Paid,
            CreatedAt = now,
            UpdatedAt = now
        };

        var successfulPayment = new Payment
        {
            Id = 60,
            CustomerId = 5,
            TargetInvoiceId = paidInvoice.Id,
            TargetInvoice = paidInvoice,
            Amount = 2000000m,
            Currency = "VND",
            Method = "bank_transfer",
            Provider = "bank_transfer",
            IdempotencyKey = "key-rsv-60",
            Metadata = "{}",
            Status = PaymentConstants.StatusSucceeded,
            CreatedAt = now,
            UpdatedAt = now
        };
        paidInvoice.Payments.Add(successfulPayment);
        reservation.Invoices.Add(paidInvoice);

        db.StorageUnits.Add(unit);
        db.Reservations.Add(reservation);
        db.UnitAllocations.Add(unitAlloc);
        db.RentalAgreements.Add(draftAgreement);
        db.Invoices.Add(paidInvoice);
        db.Payments.Add(successfulPayment);
        await db.SaveChangesAsync();

        var resService = new CustomerReservationService(
            db,
            Options.Create(new ReservationSettings()),
            new NullLogger<CustomerReservationService>());

        // Act: Customer cancels confirmed reservation before check-in
        var cancelResult = await resService.CancelReservationAsync(5, reservation.Id, "Bận việc đột xuất", CancellationToken.None);

        // Assert
        Assert.True(cancelResult);
        Assert.Equal(ReservationStatusConstants.Cancelled, reservation.Status);
        Assert.Equal(StorageUnitStatusConstants.Available, unit.PhysicalStatus);
        Assert.Equal(AllocationStatusConstants.Cancelled, unitAlloc.Status);
        Assert.Equal(RentalAgreementStatusConstants.Cancelled, draftAgreement.Status);

        var pendingRefund = await db.Refunds.FirstOrDefaultAsync(r => r.PaymentId == successfulPayment.Id);
        Assert.NotNull(pendingRefund);
        Assert.Equal(RefundStatusConstants.Pending, pendingRefund.Status);
        Assert.Equal(2000000m, pendingRefund.Amount);
    }

    [Fact]
    public async Task Task8_RenewAgreement_UsesActiveFacilityRate_WhenConfigured()
    {
        using var db = CreateInMemoryDbContext();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var unit = new StorageUnit
        {
            Id = 11,
            FacilityId = 1,
            UnitTypeId = 2,
            UnitCode = "U-11",
            PhysicalStatus = StorageUnitStatusConstants.Occupied,
            CreatedAt = now,
            UpdatedAt = now
        };

        var agreement = new RentalAgreement
        {
            Id = 88,
            AgreementNo = "AGR-RATE-88",
            CustomerId = 7,
            FacilityId = 1,
            Status = RentalAgreementStatusConstants.Active,
            StartDate = today.AddMonths(-1),
            EndDate = today,
            MonthlyRateSnapshot = 1000000m, // Old rate: 1,000,000 VND
            DepositSnapshot = 1000000m,
            CreatedAt = now.AddMonths(-1),
            UpdatedAt = now.AddMonths(-1)
        };

        var alloc = new UnitAllocation
        {
            Id = 99,
            AgreementId = agreement.Id,
            StorageUnitId = unit.Id,
            StorageUnit = unit,
            AllocationKind = "rental",
            Status = AllocationStatusConstants.Active,
            CreatedAt = now.AddMonths(-1)
        };

        // New active rate: 1,400,000 VND
        var newFacilityRate = new FacilityRate
        {
            Id = 1,
            FacilityId = 1,
            UnitTypeId = 2,
            MonthlyRate = 1400000m,
            DepositAmount = 1400000m,
            ValidFrom = today.AddDays(-10),
            ValidTo = null,
            CreatedAt = now
        };

        db.StorageUnits.Add(unit);
        db.RentalAgreements.Add(agreement);
        db.UnitAllocations.Add(alloc);
        db.FacilityRates.Add(newFacilityRate);
        await db.SaveChangesAsync();

        var service = new CustomerRentalService(
            db,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new JwtSettings { Secret = "Secret123456789012345678901234567890" }),
            new NullLogger<CustomerRentalService>(),
            Options.Create(new RentalSettings()));

        var request = new RenewAgreementRequest(2);

        // Act: Request 2 months renewal
        var response = await service.RenewAgreementAsync(7, agreement.Id, request, CancellationToken.None);

        // Assert: New rate (1,400,000 VND) was used instead of old rate (1,000,000 VND)
        Assert.NotNull(response);
        Assert.Equal(1400000m, response.MonthlyRate);
        Assert.Equal(2800000m, response.TotalRenewalAmount);
    }

    [Fact]
    public async Task StaffTasks_GetStaffTasks_DeduplicatesTasksByTicketIdAndTitle()
    {
        using var db = CreateInMemoryDbContext();
        var now = DateTimeOffset.UtcNow;

        var facility = new Facility
        {
            Id = 1,
            Code = "FAC-01",
            Name = "Facility 01",
            AddressLine = "123 Storage St",
            City = "City",
            Status = "active",
            Timezone = "Asia/Ho_Chi_Minh",
            CreatedAt = now,
            UpdatedAt = now
        };

        var staffUser = new User
        {
            Id = 911,
            Email = "staff911@storage.local",
            PasswordHash = "hash",
            Status = "active"
        };
        var staffProfile = new EmployeeProfile
        {
            UserId = 911,
            EmployeeCode = "EMP-911",
            FullName = "Nguyễn Văn Staff",
            EmploymentStatus = "active"
        };

        var ticket = new SupportTicket
        {
            Id = 9101,
            TicketNo = "TCK-2026-9101",
            FacilityId = 1,
            CustomerId = 1,
            Category = "unit",
            Priority = "normal",
            Subject = "Kho A-901 bị kẹt then cài cửa",
            Description = "Then cài cửa kẹt",
            Status = TicketStatusConstants.Assigned,
            CreatedAt = now.AddHours(-3),
            UpdatedAt = now.AddHours(-1)
        };

        // 3 duplicate tasks for the same ticket (e.g. repeated assignments)
        var task1 = new StaffTask
        {
            Id = 9201,
            FacilityId = 1,
            AssignedEmployeeId = 911,
            TicketId = 9101,
            TaskType = "support",
            Title = "[Sự cố #TCK-2026-9101] Kho A-901 bị kẹt then cài cửa",
            Status = "todo",
            ProgressPercent = 0,
            CreatedAt = now.AddHours(-3),
            UpdatedAt = now.AddHours(-3)
        };
        var task2 = new StaffTask
        {
            Id = 9202,
            FacilityId = 1,
            AssignedEmployeeId = 911,
            TicketId = 9101,
            TaskType = "support",
            Title = "[Sự cố #TCK-2026-9101] Kho A-901 bị kẹt then cài cửa",
            Status = "in_progress",
            ProgressPercent = 50,
            CreatedAt = now.AddHours(-2),
            UpdatedAt = now.AddHours(-2)
        };
        var task3 = new StaffTask
        {
            Id = 9203,
            FacilityId = 1,
            AssignedEmployeeId = 911,
            TicketId = 9101,
            TaskType = "support",
            Title = "[Sự cố #TCK-2026-9101] Kho A-901 bị kẹt then cài cửa",
            Status = "in_progress",
            ProgressPercent = 50,
            CreatedAt = now.AddHours(-1),
            UpdatedAt = now.AddHours(-1)
        };

        db.Facilities.Add(facility);
        db.Users.Add(staffUser);
        db.EmployeeProfiles.Add(staffProfile);
        db.SupportTickets.Add(ticket);
        db.StaffTasks.AddRange(task1, task2, task3);
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db, Options.Create(new PaymentSettings()), null);

        // Act: Staff loads tasks
        var staffTasks = await service.GetStaffTasksAsync(911, 1, CancellationToken.None);

        // Assert: Deduplicated to exactly 1 task
        Assert.Single(staffTasks);
        Assert.Equal(task3.Id, staffTasks[0].Id);
        Assert.Equal("[Sự cố #TCK-2026-9101] Kho A-901 bị kẹt then cài cửa", staffTasks[0].Title);
    }

    [Fact]
    public async Task StaffTasks_UpdateStaffTaskStatus_AutoMapsProgressAndSyncsTicketWithReportNote()
    {
        using var db = CreateInMemoryDbContext();
        var now = DateTimeOffset.UtcNow;

        var facility = new Facility
        {
            Id = 1,
            Code = "FAC-01",
            Name = "Facility 01",
            AddressLine = "123 Storage St",
            City = "City",
            Status = "active",
            Timezone = "Asia/Ho_Chi_Minh",
            CreatedAt = now,
            UpdatedAt = now
        };

        var staffUser = new User { Id = 911, Email = "staff911@storage.local", PasswordHash = "hash", Status = "active" };
        var staffProfile = new EmployeeProfile { UserId = 911, EmployeeCode = "EMP-911", FullName = "Staff Kỹ Thuật", EmploymentStatus = "active" };

        var ticket = new SupportTicket
        {
            Id = 9101,
            TicketNo = "TCK-2026-9101",
            FacilityId = 1,
            CustomerId = 1,
            Category = "unit",
            Priority = "normal",
            Subject = "Kho A-901 bị kẹt then cài cửa",
            Description = "Then cài cửa kẹt",
            Status = TicketStatusConstants.Assigned,
            CreatedAt = now,
            UpdatedAt = now
        };

        var task = new StaffTask
        {
            Id = 9201,
            FacilityId = 1,
            AssignedEmployeeId = 911,
            TicketId = 9101,
            TaskType = "support",
            Title = "[Sự cố #TCK-2026-9101] Kho A-901 bị kẹt then cài cửa",
            Status = "todo",
            ProgressPercent = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Facilities.Add(facility);
        db.Users.Add(staffUser);
        db.EmployeeProfiles.Add(staffProfile);
        db.SupportTickets.Add(ticket);
        db.StaffTasks.Add(task);
        await db.SaveChangesAsync();

        var service = new FacilityStaffService(db, Options.Create(new PaymentSettings()), null);

        // Case 1: Staff marks as resolved (done) with report note
        var updateDoneRequest = new UpdateStaffTaskStatusRequest("done", null, "Đã tra dầu bảo dưỡng then cài cửa, hoạt động trơn tru.");
        var resultDone = await service.UpdateStaffTaskStatusAsync(911, task.Id, updateDoneRequest, CancellationToken.None);

        Assert.Equal("done", resultDone.Status);
        Assert.Equal((short)100, resultDone.ProgressPercent);

        var updatedTicket = await db.SupportTickets.FindAsync(ticket.Id);
        Assert.NotNull(updatedTicket);
        Assert.Equal(TicketStatusConstants.Resolved, updatedTicket.Status);
        Assert.Equal("Đã tra dầu bảo dưỡng then cài cửa, hoạt động trơn tru.", updatedTicket.Resolution);

        // Check that ticket message was logged
        var messages = await db.TicketMessages.Where(m => m.TicketId == ticket.Id).ToListAsync();
        Assert.Single(messages);
        Assert.Contains("Đã tra dầu", messages[0].Body);

        // Case 2: If severe / blocked is reported, status is blocked, progress is 50%, ticket is severe_need_relocation
        var updateBlockedRequest = new UpdateStaffTaskStatusRequest("blocked", null, "Then cài gãy chốt hoàn toàn, đề nghị Manager đổi kho mới.");
        var resultBlocked = await service.UpdateStaffTaskStatusAsync(911, task.Id, updateBlockedRequest, CancellationToken.None);

        Assert.Equal("blocked", resultBlocked.Status);
        Assert.Equal((short)50, resultBlocked.ProgressPercent);

        var reUpdatedTicket = await db.SupportTickets.FindAsync(ticket.Id);
        Assert.NotNull(reUpdatedTicket);
        Assert.Equal(TicketStatusConstants.SevereNeedRelocation, reUpdatedTicket.Status);
        Assert.Contains("Hư hỏng nặng - Đề nghị đổi kho", reUpdatedTicket.Resolution);
    }

    [Fact]
    public async Task Task9_StorageLifecycle_SweepsAbandonedCartReservations_ReleasesUnitToAvailable()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<SelfStorageDbContext>(options =>
            options.UseInMemoryDatabase(dbName)
                   .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LifecycleJob:IntervalSeconds"] = "1",
            ["LifecycleJob:FacilityTimezone"] = "Asia/Ho_Chi_Minh"
        }).Build();

        var provider = services.BuildServiceProvider();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.DateTime);

        using (var initScope = provider.CreateScope())
        {
            var db = initScope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
            var unit = new StorageUnit
            {
                Id = 988,
                FacilityId = 1,
                UnitTypeId = 1,
                UnitCode = "A-988",
                PhysicalStatus = StorageUnitStatusConstants.Reserved,
                CreatedAt = now.AddHours(-1),
                UpdatedAt = now.AddHours(-1)
            };
            var reservation = new Reservation
            {
                Id = 988,
                ReservationCode = "RSV-988-ABANDONED",
                CustomerId = 1,
                FacilityId = 1,
                UnitTypeId = 1,
                FacilityRateId = 1,
                StartDate = today.AddDays(1),
                EndDate = today.AddMonths(1),
                Status = ReservationStatusConstants.Pending,
                HoldUntil = now.AddMinutes(-5), // Expired
                CreatedAt = now.AddMinutes(-20),
                UpdatedAt = now.AddMinutes(-20)
            };
            var allocation = new UnitAllocation
            {
                Id = 988,
                ReservationId = 988,
                StorageUnitId = 988,
                AllocationKind = "reservation",
                AllocationStartDate = today.AddDays(1),
                AllocationEndDate = today.AddMonths(1),
                Status = AllocationStatusConstants.Active,
                CreatedAt = now.AddMinutes(-20),
                StorageUnit = unit
            };
            reservation.UnitAllocation = allocation;

            var promoRedemption = new PromotionRedemption
            {
                Id = 988,
                ReservationId = 988,
                PromotionId = 1,
                CustomerId = 1,
                Status = PromotionRedemptionStatusConstants.Reserved,
                RedeemedAt = now.AddMinutes(-20)
            };

            db.StorageUnits.Add(unit);
            db.Reservations.Add(reservation);
            db.UnitAllocations.Add(allocation);
            db.PromotionRedemptions.Add(promoRedemption);
            await db.SaveChangesAsync();
        }

        var worker = new StorageLifecycleBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            config,
            NullLogger<StorageLifecycleBackgroundService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await worker.StopAsync(CancellationToken.None);

        using (var verifyScope = provider.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
            var sweptReservation = await db.Reservations.FindAsync(988L);
            var sweptAllocation = await db.UnitAllocations.FindAsync(988L);
            var sweptUnit = await db.StorageUnits.FindAsync(988L);
            var sweptPromo = await db.PromotionRedemptions.FindAsync(988L);

            Assert.NotNull(sweptReservation);
            Assert.Equal(ReservationStatusConstants.Expired, sweptReservation.Status);

            Assert.NotNull(sweptAllocation);
            Assert.Equal(AllocationStatusConstants.Cancelled, sweptAllocation.Status);

            Assert.NotNull(sweptUnit);
            Assert.Equal(StorageUnitStatusConstants.Available, sweptUnit.PhysicalStatus);

            Assert.NotNull(sweptPromo);
            Assert.Equal(PromotionRedemptionStatusConstants.Released, sweptPromo.Status);
        }
    }

    [Fact]
    public void Task10_TimeZoneHelper_CrossPlatform_ResolvesWindowsAndIanaTimeZonesToUtc7()
    {
        var testCandidates = new[]
        {
            "SE Asia Standard Time",
            "Asia/Ho_Chi_Minh",
            "Asia/Bangkok",
            "Asia/Saigon",
            null,
            "",
            "Invalid/Timezone_That_Does_Not_Exist"
        };

        foreach (var candidate in testCandidates)
        {
            var resolved = TimeZoneHelper.ResolveTimeZone(candidate, "Asia/Ho_Chi_Minh");
            Assert.NotNull(resolved);
            Assert.Equal(TimeSpan.FromHours(7), resolved.BaseUtcOffset);
        }
    }

    [Fact]
    public void Task11_StorageUnit_PhysicalStatus_ConcurrencyToken_ConfiguredInModel()
    {
        using var db = CreateInMemoryDbContext();
        var entityType = db.Model.FindEntityType(typeof(StorageUnit));
        Assert.NotNull(entityType);

        var property = entityType.FindProperty(nameof(StorageUnit.PhysicalStatus));
        Assert.NotNull(property);
        Assert.True(property.IsConcurrencyToken);
    }
}
