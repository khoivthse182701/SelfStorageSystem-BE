using Microsoft.EntityFrameworkCore;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence;

public partial class SelfStorageDbContext : DbContext
{
    public SelfStorageDbContext(DbContextOptions<SelfStorageDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AccessCredential> AccessCredentials { get; set; }
    public virtual DbSet<AccessEvent> AccessEvents { get; set; }
    public virtual DbSet<AccessPoint> AccessPoints { get; set; }
    public virtual DbSet<Appointment> Appointments { get; set; }
    public virtual DbSet<AuditLog> AuditLogs { get; set; }
    public virtual DbSet<AuthorizedAccessMember> AuthorizedAccessMembers { get; set; }
    public virtual DbSet<CustomerProfile> CustomerProfiles { get; set; }
    public virtual DbSet<DelinquencyAction> DelinquencyActions { get; set; }
    public virtual DbSet<DelinquencyCase> DelinquencyCases { get; set; }
    public virtual DbSet<EmployeeProfile> EmployeeProfiles { get; set; }
    public virtual DbSet<Facility> Facilities { get; set; }
    public virtual DbSet<FacilityArea> FacilityAreas { get; set; }
    public virtual DbSet<FacilityRate> FacilityRates { get; set; }
    public virtual DbSet<FeeRule> FeeRules { get; set; }
    public virtual DbSet<HandoverRecord> HandoverRecords { get; set; }
    public virtual DbSet<IdentityVerification> IdentityVerifications { get; set; }
    public virtual DbSet<Inspection> Inspections { get; set; }
    public virtual DbSet<InspectionItem> InspectionItems { get; set; }
    public virtual DbSet<IntegrationEvent> IntegrationEvents { get; set; }
    public virtual DbSet<Invoice> Invoices { get; set; }
    public virtual DbSet<InvoiceLine> InvoiceLines { get; set; }
    public virtual DbSet<LoginHistory> LoginHistories { get; set; }
    public virtual DbSet<MaintenanceWorkOrder> MaintenanceWorkOrders { get; set; }
    public virtual DbSet<MoveOutRequest> MoveOutRequests { get; set; }
    public virtual DbSet<Notification> Notifications { get; set; }
    public virtual DbSet<Payment> Payments { get; set; }
    public virtual DbSet<PaymentAllocation> PaymentAllocations { get; set; }
    public virtual DbSet<PolicyVersion> PolicyVersions { get; set; }
    public virtual DbSet<PriceRange> PriceRanges { get; set; }
    public virtual DbSet<Promotion> Promotions { get; set; }
    public virtual DbSet<PromotionRedemption> PromotionRedemptions { get; set; }
    public virtual DbSet<PromotionRule> PromotionRules { get; set; }
    public virtual DbSet<Refund> Refunds { get; set; }
    public virtual DbSet<RefundApproval> RefundApprovals { get; set; }
    public virtual DbSet<RentalAgreement> RentalAgreements { get; set; }
    public virtual DbSet<RentalRenewal> RentalRenewals { get; set; }
    public virtual DbSet<Reservation> Reservations { get; set; }
    public virtual DbSet<Role> Roles { get; set; }
    public virtual DbSet<ServiceRating> ServiceRatings { get; set; }
    public virtual DbSet<ShiftAssignment> ShiftAssignments { get; set; }
    public virtual DbSet<StaffFacilityAssignment> StaffFacilityAssignments { get; set; }
    public virtual DbSet<StaffShift> StaffShifts { get; set; }
    public virtual DbSet<StaffTask> StaffTasks { get; set; }
    public virtual DbSet<StorageUnit> StorageUnits { get; set; }
    public virtual DbSet<PricingConfig> PricingConfigs { get; set; }
    public virtual DbSet<StorageCategory> StorageCategories { get; set; }
    public virtual DbSet<StoredItem> StoredItems { get; set; }
    public virtual DbSet<SupportTicket> SupportTickets { get; set; }
    public virtual DbSet<TicketAssignment> TicketAssignments { get; set; }
    public virtual DbSet<TicketAttachment> TicketAttachments { get; set; }
    public virtual DbSet<TicketChargeApproval> TicketChargeApprovals { get; set; }
    public virtual DbSet<TicketChargeProposal> TicketChargeProposals { get; set; }
    public virtual DbSet<TicketMessage> TicketMessages { get; set; }
    public virtual DbSet<UnitAllocation> UnitAllocations { get; set; }
    public virtual DbSet<UnitMapPosition> UnitMapPositions { get; set; }
    public virtual DbSet<UnitStatusHistory> UnitStatusHistories { get; set; }
    public virtual DbSet<UnitTransferRequest> UnitTransferRequests { get; set; }
    public virtual DbSet<UnitType> UnitTypes { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<UserRole> UserRoles { get; set; }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceDatabaseInvariants();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        EnforceDatabaseInvariants();
        return base.SaveChanges();
    }

    private void EnforceDatabaseInvariants()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<UnitAllocation>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            var alloc = entry.Entity;
            if (string.Equals(alloc.Status, AllocationStatus.Active.ToDbValue(), StringComparison.OrdinalIgnoreCase))
            {
                alloc.EndedAt = null;
            }
            else
            {
                alloc.EndedAt ??= now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<MoveOutRequest>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            var req = entry.Entity;
            if (string.Equals(req.Status, MoveOutStatus.Completed.ToDbValue(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(req.Status, MoveOutStatus.Cancelled.ToDbValue(), StringComparison.OrdinalIgnoreCase))
            {
                req.FinalizedAt ??= now;
            }
            else
            {
                req.FinalizedAt = null;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("core");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SelfStorageDbContext).Assembly);

        modelBuilder.HasSequence("agreement_no_seq", "core").StartsAt(1001L);
        modelBuilder.HasSequence("invoice_no_seq", "core").StartsAt(1001L);
        modelBuilder.HasSequence("reservation_code_seq", "core").StartsAt(1001L);
        modelBuilder.HasSequence("ticket_no_seq", "core").StartsAt(1001L);
        modelBuilder.HasSequence("work_order_no_seq", "core").StartsAt(1001L);

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
