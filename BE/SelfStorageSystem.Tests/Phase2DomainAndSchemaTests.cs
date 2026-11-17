using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Tests;

public class Phase2DomainAndSchemaTests
{
    private static SelfStorageDbContext CreateSqlServerModelOnlyContext()
    {
        var options = new DbContextOptionsBuilder<SelfStorageDbContext>()
            .UseSqlServer("Server=localhost;Database=Phase2ModelInspectionOnly;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;
        return new SelfStorageDbContext(options);
    }

    private static SelfStorageDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<SelfStorageDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new SelfStorageDbContext(options);
    }

    [Fact]
    public void EnumDbMapper_RoundTripsAllCanonicalCoreStatusEnums()
    {
        foreach (var value in Enum.GetValues<UnitStatus>())
        {
            var db = value.ToDbValue();
            Assert.Equal(value, EnumDbMapper.ParseUnitStatus(db));
        }

        foreach (var value in Enum.GetValues<ReservationStatus>())
        {
            var db = value.ToDbValue();
            Assert.Equal(value, EnumDbMapper.ParseReservationStatus(db));
        }

        foreach (var value in Enum.GetValues<ContractStatus>())
        {
            var db = value.ToDbValue();
            Assert.Equal(value, EnumDbMapper.ParseContractStatus(db));
        }

        foreach (var value in Enum.GetValues<PaymentStatus>())
        {
            var db = value.ToDbValue();
            Assert.Equal(value, EnumDbMapper.ParsePaymentStatus(db));
        }

        foreach (var value in Enum.GetValues<TicketStatus>())
        {
            var db = value.ToDbValue();
            Assert.Equal(value, EnumDbMapper.ParseTicketStatus(db));
        }

        foreach (var value in Enum.GetValues<InvoiceStatus>())
        {
            var db = value.ToDbValue();
            Assert.Equal(value, EnumDbMapper.ParseInvoiceStatus(db));
        }

        foreach (var value in Enum.GetValues<RefundStatus>())
        {
            var db = value.ToDbValue();
            Assert.Equal(value, EnumDbMapper.ParseRefundStatus(db));
        }
    }

    [Theory]
    [InlineData("under_maintenance", UnitStatus.Maintenance)]
    [InlineData("in_use", UnitStatus.Occupied)]
    [InlineData("  DAMAGED  ", UnitStatus.Damaged)]
    public void EnumDbMapper_UnitStatus_AcceptsNormalizedAndLegacyAliases(string raw, UnitStatus expected)
    {
        Assert.True(EnumDbMapper.TryParseUnitStatus(raw, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("storage_customer", UserRoleCode.Customer)]
    [InlineData("Customer", UserRoleCode.Customer)]
    [InlineData("facility_staff", UserRoleCode.Staff)]
    [InlineData("staff", UserRoleCode.Staff)]
    [InlineData("facility_manager", UserRoleCode.Manager)]
    [InlineData("business_operations_manager", UserRoleCode.Manager)]
    [InlineData("system_administrator", UserRoleCode.Manager)]
    [InlineData("admin", UserRoleCode.Manager)]
    public void EnumDbMapper_RoleCode_MapsLegacyAndCanonicalRolesIntoThreeRoles(string raw, UserRoleCode expected)
    {
        Assert.True(EnumDbMapper.TryParseRoleCode(raw, out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumDbMapper_RejectsInvalidAndEmptyValues()
    {
        Assert.False(EnumDbMapper.TryParseUnitStatus("", out _));
        Assert.False(EnumDbMapper.TryParseUnitStatus(null, out _));
        Assert.False(EnumDbMapper.TryParseReservationStatus("unknown_state", out _));
        Assert.False(EnumDbMapper.TryParseContractStatus("invalid", out _));
        Assert.False(EnumDbMapper.TryParsePaymentStatus("void", out _));
        Assert.False(EnumDbMapper.TryParseTicketStatus("archived", out _));
        Assert.False(EnumDbMapper.TryParseRoleCode("super_root", out _));

        Assert.Throws<ArgumentException>(() => EnumDbMapper.ParseUnitStatus("bogus"));
        Assert.Throws<ArgumentException>(() => EnumDbMapper.ParseReservationStatus(null));
        Assert.Throws<ArgumentException>(() => EnumDbMapper.ParseContractStatus(" "));
        Assert.Throws<ArgumentException>(() => EnumDbMapper.ParsePaymentStatus("bogus"));
        Assert.Throws<ArgumentException>(() => EnumDbMapper.ParseTicketStatus("bogus"));
    }

    [Fact]
    public void DomainStateMachine_AndEntities_EnforceLegalAndIllegalTransitions()
    {
        var unit = new StorageUnit { PhysicalStatus = UnitStatus.Available.ToDbValue() };
        unit.TransitionStatus(UnitStatus.Reserved);
        Assert.Equal(UnitStatus.Reserved, unit.UnitStatus);
        unit.TransitionStatus(UnitStatus.Occupied);
        unit.TransitionStatus(UnitStatus.PendingInspection);
        unit.TransitionStatus(UnitStatus.Damaged);
        Assert.False(unit.CanTransitionTo(UnitStatus.Occupied));
        Assert.Throws<InvalidOperationException>(() => unit.TransitionStatus(UnitStatus.Occupied));
        unit.TransitionStatus(UnitStatus.PendingInspection);
        unit.TransitionStatus(UnitStatus.Damaged);
        unit.TransitionStatus(UnitStatus.Maintenance);
        unit.TransitionStatus(UnitStatus.OutOfService);
        Assert.True(unit.CanTransitionTo(UnitStatus.Damaged));
        Assert.False(unit.CanTransitionTo(UnitStatus.Occupied));
        Assert.Throws<InvalidOperationException>(() => unit.TransitionStatus(UnitStatus.Occupied));
        unit.TransitionStatus(UnitStatus.Available);
        unit.TransitionStatus(UnitStatus.Reserved);
        Assert.False(unit.CanTransitionTo(UnitStatus.PendingInspection));
        Assert.Throws<InvalidOperationException>(() => unit.TransitionStatus(UnitStatus.PendingInspection));

        var reservation = new Reservation { Status = ReservationStatus.Pending.ToDbValue() };
        reservation.TransitionStatus(ReservationStatus.AwaitingDeposit);
        reservation.TransitionStatus(ReservationStatus.Confirmed);
        Assert.NotNull(reservation.ConfirmedAt);
        reservation.TransitionStatus(ReservationStatus.CheckedIn);
        reservation.TransitionStatus(ReservationStatus.Converted);
        reservation.TransitionStatus(ReservationStatus.Completed);
        Assert.Throws<InvalidOperationException>(() => reservation.TransitionStatus(ReservationStatus.Confirmed));

        var cancelledReservation = new Reservation { Status = ReservationStatus.Pending.ToDbValue() };
        cancelledReservation.TransitionStatus(ReservationStatus.Cancelled);
        Assert.NotNull(cancelledReservation.CancelledAt);

        var directCheckInContract = new RentalAgreement { Status = ContractStatus.Draft.ToDbValue() };
        directCheckInContract.TransitionStatus(ContractStatus.Active);
        directCheckInContract.TransitionStatus(ContractStatus.CheckoutPending);
        directCheckInContract.TransitionStatus(ContractStatus.Completed);
        directCheckInContract.TransitionStatus(ContractStatus.Closed);

        var contract = new RentalAgreement { Status = ContractStatus.Draft.ToDbValue() };
        contract.TransitionStatus(ContractStatus.Scheduled);
        contract.TransitionStatus(ContractStatus.Active);
        contract.TransitionStatus(ContractStatus.ExpiringSoon);
        contract.TransitionStatus(ContractStatus.Overdue);
        contract.TransitionStatus(ContractStatus.Defaulted);
        contract.TransitionStatus(ContractStatus.Terminated);
        contract.TransitionStatus(ContractStatus.Closed);
        Assert.Throws<InvalidOperationException>(() => contract.TransitionStatus(ContractStatus.Active));

        var payment = new Payment { Status = PaymentStatus.Initiated.ToDbValue() };
        payment.TransitionStatus(PaymentStatus.Pending);
        payment.TransitionStatus(PaymentStatus.Succeeded);
        Assert.NotNull(payment.PaidAt);
        payment.TransitionStatus(PaymentStatus.PartiallyRefunded);
        payment.TransitionStatus(PaymentStatus.Refunded);
        Assert.Throws<InvalidOperationException>(() => payment.TransitionStatus(PaymentStatus.Pending));

        var invoice = new Invoice { Status = InvoiceStatus.Draft.ToDbValue() };
        invoice.TransitionStatus(InvoiceStatus.Open);
        Assert.NotNull(invoice.OpenedAt);
        invoice.TransitionStatus(InvoiceStatus.Voided);
        Assert.NotNull(invoice.VoidedAt);
        Assert.Throws<InvalidOperationException>(() => invoice.TransitionStatus(InvoiceStatus.Paid));

        var refund = new Refund { Status = RefundStatus.Requested.ToDbValue() };
        refund.TransitionStatus(RefundStatus.Approved);
        refund.TransitionStatus(RefundStatus.Succeeded);
        Assert.NotNull(refund.RefundedAt);
        Assert.Throws<InvalidOperationException>(() => refund.TransitionStatus(RefundStatus.Pending));

        var ticket = new SupportTicket { Status = TicketStatus.Open.ToDbValue() };
        ticket.TransitionStatus(TicketStatus.Assigned);
        ticket.TransitionStatus(TicketStatus.InProgress);
        ticket.TransitionStatus(TicketStatus.WaitingForCustomer);
        ticket.TransitionStatus(TicketStatus.Resolved);
        Assert.NotNull(ticket.ResolvedAt);
        ticket.TransitionStatus(TicketStatus.Closed);
        Assert.Throws<InvalidOperationException>(() => ticket.TransitionStatus(TicketStatus.Open));
    }

    [Fact]
    public void RentalAgreement_IsExpiringSoon_RespectsThresholdAndStatusBoundaries()
    {
        var today = new DateOnly(2026, 6, 10);
        var activeContract = new RentalAgreement
        {
            Status = ContractStatus.Active.ToDbValue(),
            EndDate = today.AddDays(7)
        };

        Assert.True(activeContract.IsExpiringSoon(today, thresholdDays: 7));
        Assert.True(activeContract.IsExpiringSoon(today.AddDays(7), thresholdDays: 7)); // exact end date (0 days remaining)
        Assert.False(activeContract.IsExpiringSoon(today.AddDays(-1), thresholdDays: 7)); // 8 days remaining
        Assert.False(activeContract.IsExpiringSoon(today.AddDays(8), thresholdDays: 7)); // past end date

        activeContract.Status = ContractStatus.Overdue.ToDbValue();
        Assert.False(activeContract.IsExpiringSoon(today, thresholdDays: 7));
    }

    [Fact]
    public void SelfStorageDbContext_ModelMatchesClean58TableSchemaAndConstraints()
    {
        using var db = CreateSqlServerModelOnlyContext();
        var model = db.Model;
        var designModel = db.GetService<IDesignTimeModel>().Model;

        Assert.Equal("core", model.GetDefaultSchema());

        var entityTypes = model.GetEntityTypes().ToList();
        Assert.Equal(58, entityTypes.Count);
        Assert.DoesNotContain(entityTypes, et =>
            string.Equals(et.GetTableName(), "UnitTypeHaTDT", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(et.GetTableName(), "StorageUnitHaTDT", StringComparison.OrdinalIgnoreCase));

        var sequences = model.GetSequences().Select(s => $"{s.Schema}.{s.Name}").OrderBy(x => x).ToArray();
        Assert.Equal(
            [
                "core.agreement_no_seq",
                "core.invoice_no_seq",
                "core.reservation_code_seq",
                "core.ticket_no_seq",
                "core.work_order_no_seq"
            ],
            sequences);

        var storageUnitEntity = designModel.FindEntityType(typeof(StorageUnit))!;
        Assert.Contains(storageUnitEntity.GetCheckConstraints(), cc =>
            cc.Name == "CK_storage_units_physical_status" &&
            cc.Sql == EnumDbMapper.ToSqlCheckInClause<UnitStatus>("physical_status"));
        Assert.Contains(storageUnitEntity.GetDeclaredTriggers(), t => t.GetDatabaseName() == "trg_storage_unit_status_transition");

        var rentalAgreementEntity = designModel.FindEntityType(typeof(RentalAgreement))!;
        Assert.Contains(rentalAgreementEntity.GetCheckConstraints(), cc =>
            cc.Name == "CK_rental_agreements_status" &&
            cc.Sql == EnumDbMapper.ToSqlCheckInClause<ContractStatus>("status"));
        Assert.Contains(rentalAgreementEntity.GetDeclaredTriggers(), t => t.GetDatabaseName() == "trg_agreement_status_transition");

        var reservationEntity = designModel.FindEntityType(typeof(Reservation))!;
        Assert.Contains(reservationEntity.GetCheckConstraints(), cc =>
            cc.Name == "CK_reservations_status" &&
            cc.Sql == EnumDbMapper.ToSqlCheckInClause<ReservationStatus>("status"));

        var paymentEntity = designModel.FindEntityType(typeof(Payment))!;
        Assert.Contains(paymentEntity.GetCheckConstraints(), cc =>
            cc.Name == "CK_payments_status" &&
            cc.Sql == EnumDbMapper.ToSqlCheckInClause<PaymentStatus>("status"));
        Assert.Contains(paymentEntity.GetCheckConstraints(), cc =>
            cc.Name == "CK_payments_method" &&
            cc.Sql == EnumDbMapper.ToSqlCheckInClause<PaymentMethod>("method"));

        var ticketEntity = designModel.FindEntityType(typeof(SupportTicket))!;
        Assert.Contains(ticketEntity.GetCheckConstraints(), cc =>
            cc.Name == "CK_support_tickets_status" &&
            cc.Sql == EnumDbMapper.ToSqlCheckInClause<TicketStatus>("status"));

        var transferEntity = designModel.FindEntityType(typeof(UnitTransferRequest))!;
        Assert.NotNull(transferEntity.FindProperty(nameof(UnitTransferRequest.CleaningFee)));
        Assert.Contains(transferEntity.GetForeignKeys(), fk => fk.GetConstraintName() == "FK_unit_transfer_requests_requested_facility");

        var allocationEntity = designModel.FindEntityType(typeof(UnitAllocation))!;
        var availabilityIdx = allocationEntity.GetIndexes().Single(ix => ix.GetDatabaseName() == "unit_allocations_availability_idx");
        Assert.Equal("([status]='active')", availabilityIdx.GetFilter());
        Assert.Contains(allocationEntity.GetDeclaredTriggers(), t => t.GetDatabaseName() == "trg_unit_allocations_validate_scope");

        var feeRuleEntity = designModel.FindEntityType(typeof(FeeRule))!;
        var feeRuleUidx = feeRuleEntity.GetIndexes().Single(ix => ix.GetDatabaseName() == "fee_rules_scope_code_start_uidx");
        Assert.True(feeRuleUidx.IsUnique);
        Assert.Null(feeRuleUidx.GetFilter());

        var storedItemEntity = designModel.FindEntityType(typeof(StoredItem))!;
        Assert.True(storedItemEntity.FindProperty(nameof(StoredItem.Category))!.IsNullable);
    }

    [Fact]
    public async Task SelfStorageDbContext_SaveChanges_EnforcesAllocationAndMoveOutTimestamps()
    {
        using var db = CreateInMemoryContext(nameof(SelfStorageDbContext_SaveChanges_EnforcesAllocationAndMoveOutTimestamps));
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var allocation = new UnitAllocation
        {
            StorageUnitId = 1,
            ReservationId = 1,
            AllocationKind = AllocationKind.ReservationHold.ToDbValue(),
            AllocationStartDate = today,
            AllocationEndDate = today.AddMonths(1),
            Status = AllocationStatus.Active.ToDbValue(),
            EndedAt = now, // should be cleared while active
            CreatedAt = now
        };
        var moveOut = new MoveOutRequest
        {
            AgreementId = 1,
            RequestedBy = 1,
            RequestedMoveOutDate = today.AddDays(5),
            Status = MoveOutStatus.Requested.ToDbValue(),
            FinalizedAt = now, // should be cleared while non-terminal
            CreatedAt = now,
            UpdatedAt = now
        };

        db.UnitAllocations.Add(allocation);
        db.MoveOutRequests.Add(moveOut);
        await db.SaveChangesAsync();

        Assert.Null(allocation.EndedAt);
        Assert.Null(moveOut.FinalizedAt);

        allocation.Status = AllocationStatus.Released.ToDbValue();
        moveOut.Status = MoveOutStatus.Completed.ToDbValue();
        await db.SaveChangesAsync();

        Assert.NotNull(allocation.EndedAt);
        Assert.NotNull(moveOut.FinalizedAt);
    }

    [Fact]
    public async Task DbSeeder_SeedsAllThreeRolesAndSevenFlowsIdempotently()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => DbSeeder.SeedAsync(null!));

        using var db = CreateInMemoryContext(nameof(DbSeeder_SeedsAllThreeRolesAndSevenFlowsIdempotently));

        await DbSeeder.SeedAsync(db);

        var roles = await db.Roles.Select(r => r.Code).OrderBy(x => x).ToListAsync();
        Assert.Equal(["facility_manager", "facility_staff", "storage_customer"], roles);

        var usersCount = await db.Users.CountAsync();
        var unitsCount = await db.StorageUnits.CountAsync();
        var reservationsCount = await db.Reservations.CountAsync();
        var agreementsCount = await db.RentalAgreements.CountAsync();
        var invoicesCount = await db.Invoices.CountAsync();
        var paymentsCount = await db.Payments.CountAsync();
        var ticketsCount = await db.SupportTickets.CountAsync();
        var refundsCount = await db.Refunds.CountAsync();
        var membersCount = await db.AuthorizedAccessMembers.CountAsync();
        var workOrdersCount = await db.MaintenanceWorkOrders.CountAsync();
        var delinquencyCount = await db.DelinquencyCases.CountAsync();

        Assert.Equal(6, usersCount);
        Assert.Equal(12, unitsCount);
        Assert.Equal(5, reservationsCount);
        Assert.Equal(4, agreementsCount);
        Assert.Equal(5, invoicesCount);
        Assert.Equal(3, paymentsCount);
        Assert.Equal(3, ticketsCount);
        Assert.True(refundsCount >= 1);
        Assert.True(membersCount >= 1);
        Assert.True(workOrdersCount >= 1);
        Assert.True(delinquencyCount >= 1);

        // Flow 1: SePay idempotency event + pending hold & confirmed reservations
        Assert.True(await db.IntegrationEvents.AnyAsync(e => e.Source == "sepay"));
        Assert.True(await db.Reservations.AnyAsync(r => r.Status == ReservationStatus.Pending.ToDbValue()));
        Assert.True(await db.Reservations.AnyAsync(r => r.Status == ReservationStatus.Confirmed.ToDbValue()));

        // Flow 2: Identity verification + handover + PIN credentials + inspection
        Assert.True(await db.IdentityVerifications.AnyAsync());
        Assert.True(await db.HandoverRecords.AnyAsync());
        Assert.True(await db.AccessCredentials.AnyAsync(c => c.CredentialType == CredentialType.Pin.ToDbValue()));
        Assert.True(await db.Inspections.AnyAsync());

        // Flow 3: Authorized access member + access logs + move-out + transfer request
        Assert.True(await db.AccessEvents.AnyAsync());
        Assert.True(await db.MoveOutRequests.AnyAsync());
        Assert.True(await db.UnitTransferRequests.AnyAsync());

        // Flow 4: Price ranges, rates, fee rules, promotions, policies
        Assert.True(await db.PriceRanges.AnyAsync());
        Assert.True(await db.FacilityRates.AnyAsync());
        Assert.True(await db.FeeRules.AnyAsync());
        Assert.True(await db.Promotions.AnyAsync());
        Assert.Equal(6, await db.PolicyVersions.Select(p => p.PolicyType).Distinct().CountAsync());

        // Flow 5: Facility assignments, areas, shifts, damaged/maintenance units
        Assert.True(await db.StaffFacilityAssignments.AnyAsync());
        Assert.True(await db.StaffShifts.AnyAsync());
        Assert.True(await db.StorageUnits.AnyAsync(u => u.PhysicalStatus == UnitStatus.Maintenance.ToDbValue()));
        Assert.True(await db.StorageUnits.AnyAsync(u => u.PhysicalStatus == UnitStatus.Damaged.ToDbValue()));

        // Flow 6: Overdue contract (genuinely 3 days past end date, before 30-day default) + delinquency action + renewal
        var overdueContract = await db.RentalAgreements.SingleAsync(a => a.Status == ContractStatus.Overdue.ToDbValue());
        var todayUtc = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        Assert.Equal(3, todayUtc.DayNumber - overdueContract.EndDate.DayNumber);
        Assert.True(await db.DelinquencyActions.AnyAsync());
        Assert.True(await db.RentalRenewals.AnyAsync());

        // Flow 7: Ticket attachments, messages, charge proposal/approval, refund/approval, service rating
        Assert.True(await db.TicketAttachments.AnyAsync());
        Assert.True(await db.TicketMessages.AnyAsync());
        Assert.True(await db.TicketChargeProposals.AnyAsync());
        Assert.True(await db.TicketChargeApprovals.AnyAsync());
        Assert.True(await db.RefundApprovals.AnyAsync());
        Assert.True(await db.ServiceRatings.AnyAsync());

        // Idempotent second run must not create duplicate records
        await DbSeeder.SeedAsync(db);

        Assert.Equal(usersCount, await db.Users.CountAsync());
        Assert.Equal(unitsCount, await db.StorageUnits.CountAsync());
        Assert.Equal(reservationsCount, await db.Reservations.CountAsync());
        Assert.Equal(agreementsCount, await db.RentalAgreements.CountAsync());
        Assert.Equal(invoicesCount, await db.Invoices.CountAsync());
        Assert.Equal(paymentsCount, await db.Payments.CountAsync());
        Assert.Equal(ticketsCount, await db.SupportTickets.CountAsync());
        Assert.Equal(refundsCount, await db.Refunds.CountAsync());
        Assert.Equal(membersCount, await db.AuthorizedAccessMembers.CountAsync());
        Assert.Equal(workOrdersCount, await db.MaintenanceWorkOrders.CountAsync());
        Assert.Equal(delinquencyCount, await db.DelinquencyCases.CountAsync());
    }
}
