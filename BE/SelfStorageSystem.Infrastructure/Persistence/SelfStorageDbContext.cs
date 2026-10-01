using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SelfStorageSystem.Domain.Entities;

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

    public virtual DbSet<StorageUnitHaTdt> StorageUnitHaTdts { get; set; }

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

    public virtual DbSet<UnitTypeHaTdt> UnitTypeHaTdts { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccessCredential>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__access_c__3213E83FDE7FE5B7");

            entity.ToTable("access_credentials", "core", tb => tb.HasTrigger("trg_access_credentials_validate_scope"));

            entity.HasIndex(e => new { e.AgreementId, e.Status }, "access_credentials_agreement_idx");

            entity.HasIndex(e => e.AuthorizedMemberId, "access_credentials_authorized_member_id_idx").HasFilter("([authorized_member_id] IS NOT NULL)");

            entity.HasIndex(e => e.IssuedBy, "access_credentials_issued_by_idx").HasFilter("([issued_by] IS NOT NULL)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
            entity.Property(e => e.AuthorizedMemberId).HasColumnName("authorized_member_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CredentialType)
                .HasMaxLength(255)
                .HasColumnName("credential_type");
            entity.Property(e => e.DisplayHint)
                .HasMaxLength(255)
                .HasColumnName("display_hint");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.ExternalSecretRef)
                .HasMaxLength(255)
                .HasColumnName("external_secret_ref");
            entity.Property(e => e.IssuedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("issued_at");
            entity.Property(e => e.IssuedBy).HasColumnName("issued_by");
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");
            entity.Property(e => e.SecretDigest)
                .HasMaxLength(255)
                .HasColumnName("secret_digest");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("pending")
                .HasColumnName("status");

            entity.HasOne(d => d.Agreement).WithMany(p => p.AccessCredentials)
                .HasForeignKey(d => d.AgreementId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__access_cr__agree__442B18F2");

            entity.HasOne(d => d.AuthorizedMember).WithMany(p => p.AccessCredentials)
                .HasForeignKey(d => d.AuthorizedMemberId)
                .HasConstraintName("FK__access_cr__autho__451F3D2B");

            entity.HasOne(d => d.IssuedByNavigation).WithMany(p => p.AccessCredentials)
                .HasForeignKey(d => d.IssuedBy)
                .HasConstraintName("FK__access_cr__issue__49E3F248");
        });

        modelBuilder.Entity<AccessEvent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__access_e__3213E83FC1162516");

            entity.ToTable("access_events", "core");

            entity.HasIndex(e => e.ExternalEventId, "UQ__access_e__A0144288E5B75B80").IsUnique();

            entity.HasIndex(e => new { e.CredentialId, e.OccurredAt }, "access_events_credential_occurred_idx")
                .IsDescending(false, true)
                .HasFilter("([credential_id] IS NOT NULL)");

            entity.HasIndex(e => new { e.AccessPointId, e.OccurredAt }, "access_events_point_occurred_idx").IsDescending(false, true);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AccessPointId).HasColumnName("access_point_id");
            entity.Property(e => e.CredentialId).HasColumnName("credential_id");
            entity.Property(e => e.ExternalEventId)
                .HasMaxLength(255)
                .HasColumnName("external_event_id");
            entity.Property(e => e.Metadata)
                .HasDefaultValue("{}")
                .HasColumnName("metadata");
            entity.Property(e => e.OccurredAt).HasColumnName("occurred_at");
            entity.Property(e => e.Reason)
                .HasMaxLength(255)
                .HasColumnName("reason");
            entity.Property(e => e.Result)
                .HasMaxLength(255)
                .HasColumnName("result");

            entity.HasOne(d => d.AccessPoint).WithMany(p => p.AccessEvents)
                .HasForeignKey(d => d.AccessPointId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__access_ev__acces__51851410");

            entity.HasOne(d => d.Credential).WithMany(p => p.AccessEvents)
                .HasForeignKey(d => d.CredentialId)
                .HasConstraintName("FK__access_ev__crede__5090EFD7");
        });

        modelBuilder.Entity<AccessPoint>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__access_p__3213E83FFEB8BC50");

            entity.ToTable("access_points", "core");

            entity.HasIndex(e => new { e.FacilityId, e.Code }, "UQ__access_p__21BF3E60701E44D7").IsUnique();

            entity.HasIndex(e => new { e.FacilityId, e.Status }, "access_points_facility_id_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AccessPointType)
                .HasMaxLength(255)
                .HasColumnName("access_point_type");
            entity.Property(e => e.Code)
                .HasMaxLength(255)
                .HasColumnName("code");
            entity.Property(e => e.ExternalDeviceCode)
                .HasMaxLength(255)
                .HasColumnName("external_device_code");
            entity.Property(e => e.FacilityId).HasColumnName("facility_id");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("active")
                .HasColumnName("status");

            entity.HasOne(d => d.Facility).WithMany(p => p.AccessPoints)
                .HasForeignKey(d => d.FacilityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__access_po__facil__3E723F9C");
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__appointm__3213E83F154E5845");

            entity.ToTable("appointments", "core");

            entity.HasIndex(e => e.AgreementId, "appointments_agreement_id_idx").HasFilter("([agreement_id] IS NOT NULL)");

            entity.HasIndex(e => e.AssignedStaffId, "appointments_assigned_staff_id_idx").HasFilter("([assigned_staff_id] IS NOT NULL)");

            entity.HasIndex(e => new { e.FacilityId, e.StartsAt, e.Status }, "appointments_facility_schedule_idx");

            entity.HasIndex(e => e.ReservationId, "appointments_reservation_id_idx").HasFilter("([reservation_id] IS NOT NULL)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
            entity.Property(e => e.AppointmentType)
                .HasMaxLength(255)
                .HasColumnName("appointment_type");
            entity.Property(e => e.AssignedStaffId).HasColumnName("assigned_staff_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.EndsAt).HasColumnName("ends_at");
            entity.Property(e => e.FacilityId).HasColumnName("facility_id");
            entity.Property(e => e.Notes)
                .HasMaxLength(255)
                .HasColumnName("notes");
            entity.Property(e => e.ReservationId).HasColumnName("reservation_id");
            entity.Property(e => e.StartsAt).HasColumnName("starts_at");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("scheduled")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Agreement).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.AgreementId)
                .HasConstraintName("FK__appointme__agree__214BF109");

            entity.HasOne(d => d.AssignedStaff).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.AssignedStaffId)
                .HasConstraintName("FK__appointme__assig__22401542");

            entity.HasOne(d => d.Facility).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.FacilityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__appointme__facil__1F63A897");

            entity.HasOne(d => d.Reservation).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.ReservationId)
                .HasConstraintName("FK__appointme__reser__2057CCD0");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__audit_lo__3213E83FDE7CB008");

            entity.ToTable("audit_logs", "core");

            entity.HasIndex(e => new { e.ActorUserId, e.OccurredAt }, "audit_logs_actor_idx")
                .IsDescending(false, true)
                .HasFilter("([actor_user_id] IS NOT NULL)");

            entity.HasIndex(e => new { e.EntityType, e.EntityId, e.OccurredAt }, "audit_logs_entity_idx").IsDescending(false, false, true);

            entity.HasIndex(e => e.RequestId, "audit_logs_request_id_idx").HasFilter("([request_id] IS NOT NULL)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Action)
                .HasMaxLength(255)
                .HasColumnName("action");
            entity.Property(e => e.ActorRole)
                .HasMaxLength(255)
                .HasColumnName("actor_role");
            entity.Property(e => e.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(e => e.EntityId)
                .HasMaxLength(255)
                .HasColumnName("entity_id");
            entity.Property(e => e.EntitySchema)
                .HasMaxLength(255)
                .HasDefaultValue("core")
                .HasColumnName("entity_schema");
            entity.Property(e => e.EntityType)
                .HasMaxLength(255)
                .HasColumnName("entity_type");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .HasColumnName("ip_address");
            entity.Property(e => e.NewValues).HasColumnName("new_values");
            entity.Property(e => e.OccurredAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("occurred_at");
            entity.Property(e => e.OldValues).HasColumnName("old_values");
            entity.Property(e => e.RequestId)
                .HasMaxLength(255)
                .HasColumnName("request_id");

            entity.HasOne(d => d.ActorUser).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.ActorUserId)
                .HasConstraintName("FK__audit_log__actor__74CE504D");
        });

        modelBuilder.Entity<AuthorizedAccessMember>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__authoriz__3213E83F51D983B0");

            entity.ToTable("authorized_access_members", "core");

            entity.HasIndex(e => new { e.AgreementId, e.Status }, "authorized_access_members_agreement_idx");

            entity.HasIndex(e => e.CreatedBy, "authorized_access_members_created_by_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.FullName)
                .HasMaxLength(255)
                .HasColumnName("full_name");
            entity.Property(e => e.IdentityFingerprint)
                .HasMaxLength(255)
                .HasColumnName("identity_fingerprint");
            entity.Property(e => e.RelationshipToCustomer)
                .HasMaxLength(255)
                .HasColumnName("relationship_to_customer");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("active")
                .HasColumnName("status");
            entity.Property(e => e.ValidFrom)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("valid_from");
            entity.Property(e => e.ValidTo).HasColumnName("valid_to");

            entity.HasOne(d => d.Agreement).WithMany(p => p.AuthorizedAccessMembers)
                .HasForeignKey(d => d.AgreementId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__authorize__agree__4D2A7347");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.AuthorizedAccessMembers)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__authorize__creat__50FB042B");
        });

        modelBuilder.Entity<CustomerProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__customer__B9BE370F6AA710AD");

            entity.ToTable("customer_profiles", "core");

            entity.HasIndex(e => e.IdentityNumber, "customer_profiles_identity_number_uidx")
                .IsUnique()
                .HasFilter("([identity_number] IS NOT NULL)");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("user_id");
            entity.Property(e => e.Address)
                .HasMaxLength(255)
                .HasColumnName("address");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.DateOfBirth).HasColumnName("date_of_birth");
            entity.Property(e => e.EmergencyContactName)
                .HasMaxLength(255)
                .HasColumnName("emergency_contact_name");
            entity.Property(e => e.EmergencyContactPhone)
                .HasMaxLength(255)
                .HasColumnName("emergency_contact_phone");
            entity.Property(e => e.FullName)
                .HasMaxLength(255)
                .HasColumnName("full_name");
            entity.Property(e => e.IdentityNumber)
                .HasMaxLength(255)
                .HasColumnName("identity_number");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.User).WithOne(p => p.CustomerProfile)
                .HasForeignKey<CustomerProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__customer___user___60A75C0F");
        });

        modelBuilder.Entity<DelinquencyAction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__delinque__3213E83F0C91ECFE");

            entity.ToTable("delinquency_actions", "core");

            entity.HasIndex(e => new { e.DelinquencyCaseId, e.OccurredAt }, "delinquency_actions_case_occurred_idx");

            entity.HasIndex(e => e.PerformedBy, "delinquency_actions_performed_by_idx").HasFilter("([performed_by] IS NOT NULL)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ActionType)
                .HasMaxLength(255)
                .HasColumnName("action_type");
            entity.Property(e => e.DelinquencyCaseId).HasColumnName("delinquency_case_id");
            entity.Property(e => e.Details)
                .HasDefaultValue("{}")
                .HasColumnName("details");
            entity.Property(e => e.OccurredAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("occurred_at");
            entity.Property(e => e.PerformedBy).HasColumnName("performed_by");

            entity.HasOne(d => d.DelinquencyCase).WithMany(p => p.DelinquencyActions)
                .HasForeignKey(d => d.DelinquencyCaseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__delinquen__delin__60C757A0");

            entity.HasOne(d => d.PerformedByNavigation).WithMany(p => p.DelinquencyActions)
                .HasForeignKey(d => d.PerformedBy)
                .HasConstraintName("FK__delinquen__perfo__62AFA012");
        });

        modelBuilder.Entity<DelinquencyCase>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__delinque__3213E83F82280640");

            entity.ToTable("delinquency_cases", "core");

            entity.HasIndex(e => new { e.AgreementId, e.Status, e.OpenedAt }, "delinquency_cases_agreement_idx").IsDescending(false, false, true);

            entity.HasIndex(e => e.InvoiceId, "delinquency_cases_invoice_id_idx");

            entity.HasIndex(e => e.InvoiceId, "delinquency_cases_one_open_invoice_uidx")
                .IsUnique()
                .HasFilter("([resolved_at] IS NULL)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
            entity.Property(e => e.GraceEndsAt).HasColumnName("grace_ends_at");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.Notes)
                .HasMaxLength(255)
                .HasColumnName("notes");
            entity.Property(e => e.OpenedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("opened_at");
            entity.Property(e => e.OutstandingSnapshot)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("outstanding_snapshot");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("grace")
                .HasColumnName("status");

            entity.HasOne(d => d.Agreement).WithMany(p => p.DelinquencyCases)
                .HasForeignKey(d => d.AgreementId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__delinquen__agree__573DED66");

            entity.HasOne(d => d.Invoice).WithOne(p => p.DelinquencyCase)
                .HasForeignKey<DelinquencyCase>(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__delinquen__invoi__5832119F");
        });

        modelBuilder.Entity<EmployeeProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__employee__B9BE370FE5C68310");

            entity.ToTable("employee_profiles", "core");

            entity.HasIndex(e => e.EmployeeCode, "UQ__employee__B0AA73450E145A67").IsUnique();

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("user_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.EmployeeCode)
                .HasMaxLength(255)
                .HasColumnName("employee_code");
            entity.Property(e => e.EmploymentStatus)
                .HasMaxLength(255)
                .HasDefaultValue("active")
                .HasColumnName("employment_status");
            entity.Property(e => e.FullName)
                .HasMaxLength(255)
                .HasColumnName("full_name");
            entity.Property(e => e.HireDate).HasColumnName("hire_date");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.User).WithOne(p => p.EmployeeProfile)
                .HasForeignKey<EmployeeProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__employee___user___66603565");
        });

        modelBuilder.Entity<Facility>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__faciliti__3213E83FE0B4338E");

            entity.ToTable("facilities", "core");

            entity.HasIndex(e => e.Code, "UQ__faciliti__357D4CF9081C918B").IsUnique();

            entity.HasIndex(e => new { e.City, e.Status }, "facilities_city_status_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AddressLine)
                .HasMaxLength(255)
                .HasColumnName("address_line");
            entity.Property(e => e.City)
                .HasMaxLength(255)
                .HasColumnName("city");
            entity.Property(e => e.ClosingTime).HasColumnName("closing_time");
            entity.Property(e => e.Code)
                .HasMaxLength(255)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.District)
                .HasMaxLength(255)
                .HasColumnName("district");
            entity.Property(e => e.Latitude)
                .HasColumnType("numeric(9, 6)")
                .HasColumnName("latitude");
            entity.Property(e => e.Longitude)
                .HasColumnType("numeric(9, 6)")
                .HasColumnName("longitude");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.OpeningTime).HasColumnName("opening_time");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("active")
                .HasColumnName("status");
            entity.Property(e => e.Timezone)
                .HasMaxLength(255)
                .HasDefaultValue("Asia/Ho_Chi_Minh")
                .HasColumnName("timezone");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.Ward)
                .HasMaxLength(255)
                .HasColumnName("ward");
        });

        modelBuilder.Entity<FacilityArea>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__facility__3213E83FEE469076");

            entity.ToTable("facility_areas", "core");

            entity.HasIndex(e => new { e.FacilityId, e.Code }, "UQ__facility__21BF3E60BE1B2B38").IsUnique();

            entity.HasIndex(e => new { e.Id, e.FacilityId }, "UQ__facility__C93D6694F87C9E30").IsUnique();

            entity.HasIndex(e => e.ParentAreaId, "facility_areas_parent_area_id_idx").HasFilter("([parent_area_id] IS NOT NULL)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AreaType)
                .HasMaxLength(255)
                .HasColumnName("area_type");
            entity.Property(e => e.Code)
                .HasMaxLength(255)
                .HasColumnName("code");
            entity.Property(e => e.DisplayOrder).HasColumnName("display_order");
            entity.Property(e => e.FacilityId).HasColumnName("facility_id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.MapMetadata)
                .HasDefaultValue("{}")
                .HasColumnName("map_metadata");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.ParentAreaId).HasColumnName("parent_area_id");

            entity.HasOne(d => d.Facility).WithMany(p => p.FacilityAreas)
                .HasForeignKey(d => d.FacilityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__facility___facil__01142BA1");

            entity.HasOne(d => d.ParentArea).WithMany(p => p.InverseParentArea)
                .HasForeignKey(d => d.ParentAreaId)
                .HasConstraintName("FK__facility___paren__02084FDA");

            entity.HasOne(d => d.FacilityAreaNavigation).WithMany(p => p.InverseFacilityAreaNavigation)
                .HasPrincipalKey(p => new { p.Id, p.FacilityId })
                .HasForeignKey(d => new { d.ParentAreaId, d.FacilityId })
                .HasConstraintName("FK__facility_areas__08B54D69");
        });

        modelBuilder.Entity<FacilityRate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__facility__3213E83F925384A1");

            entity.ToTable("facility_rates", "core", tb => tb.HasTrigger("trg_facility_rate_no_overlap"));

            entity.HasIndex(e => e.CreatedBy, "facility_rates_created_by_idx").HasFilter("([created_by] IS NOT NULL)");

            entity.HasIndex(e => e.UnitTypeId, "facility_rates_unit_type_id_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.BookingFee)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("booking_fee");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DepositAmount)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("deposit_amount");
            entity.Property(e => e.FacilityId).HasColumnName("facility_id");
            entity.Property(e => e.MonthlyRate)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("monthly_rate");
            entity.Property(e => e.UnitTypeId).HasColumnName("unit_type_id");
            entity.Property(e => e.ValidFrom).HasColumnName("valid_from");
            entity.Property(e => e.ValidTo).HasColumnName("valid_to");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.FacilityRates)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__facility___creat__3D2915A8");

            entity.HasOne(d => d.Facility).WithMany(p => p.FacilityRates)
                .HasForeignKey(d => d.FacilityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__facility___facil__37703C52");

            entity.HasOne(d => d.UnitType).WithMany(p => p.FacilityRates)
                .HasForeignKey(d => d.UnitTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__facility___unit___3864608B");
        });

        modelBuilder.Entity<FeeRule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__fee_rule__3213E83F5F89760E");

            entity.ToTable("fee_rules", "core", tb => tb.HasTrigger("trg_fee_rule_no_overlap"));

            entity.HasIndex(e => e.CreatedBy, "fee_rules_created_by_idx").HasFilter("([created_by] IS NOT NULL)");

            entity.HasIndex(e => e.FacilityId, "fee_rules_facility_id_idx").HasFilter("([facility_id] IS NOT NULL)");

            entity.HasIndex(e => new { e.FacilityId, e.Code, e.ValidFrom }, "fee_rules_scope_code_start_uidx").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Amount)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("amount");
            entity.Property(e => e.CalculationMethod)
                .HasMaxLength(255)
                .HasColumnName("calculation_method");
            entity.Property(e => e.Code)
                .HasMaxLength(255)
                .HasColumnName("code");
            entity.Property(e => e.Conditions)
                .HasDefaultValue("{}")
                .HasColumnName("conditions");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.FacilityId).HasColumnName("facility_id");
            entity.Property(e => e.FeeType)
                .HasMaxLength(255)
                .HasColumnName("fee_type");
            entity.Property(e => e.GraceDays).HasColumnName("grace_days");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.RatePercent)
                .HasColumnType("numeric(7, 4)")
                .HasColumnName("rate_percent");
            entity.Property(e => e.ValidFrom).HasColumnName("valid_from");
            entity.Property(e => e.ValidTo).HasColumnName("valid_to");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.FeeRules)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__fee_rules__creat__531856C7");

            entity.HasOne(d => d.Facility).WithMany(p => p.FeeRules)
                .HasForeignKey(d => d.FacilityId)
                .HasConstraintName("FK__fee_rules__facil__498EEC8D");
        });

        modelBuilder.Entity<HandoverRecord>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__handover__3213E83F4B215F3D");

            entity.ToTable("handover_records", "core");

            entity.HasIndex(e => new { e.AgreementId, e.CreatedAt }, "handover_records_agreement_idx").IsDescending(false, true);

            entity.HasIndex(e => e.HandledBy, "handover_records_handled_by_idx");

            entity.HasIndex(e => e.InspectionId, "handover_records_inspection_id_idx");

            entity.HasIndex(e => e.UnitAllocationId, "handover_records_unit_allocation_id_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CustomerSignatureRef)
                .HasMaxLength(255)
                .HasColumnName("customer_signature_ref");
            entity.Property(e => e.CustomerSignedAt).HasColumnName("customer_signed_at");
            entity.Property(e => e.HandledBy).HasColumnName("handled_by");
            entity.Property(e => e.HandoverType)
                .HasMaxLength(255)
                .HasColumnName("handover_type");
            entity.Property(e => e.InspectionId).HasColumnName("inspection_id");
            entity.Property(e => e.Notes)
                .HasMaxLength(255)
                .HasColumnName("notes");
            entity.Property(e => e.StaffSignatureRef)
                .HasMaxLength(255)
                .HasColumnName("staff_signature_ref");
            entity.Property(e => e.StaffSignedAt).HasColumnName("staff_signed_at");
            entity.Property(e => e.UnitAllocationId).HasColumnName("unit_allocation_id");

            entity.HasOne(d => d.Agreement).WithMany(p => p.HandoverRecords)
                .HasForeignKey(d => d.AgreementId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__handover___agree__4589517F");

            entity.HasOne(d => d.HandledByNavigation).WithMany(p => p.HandoverRecords)
                .HasForeignKey(d => d.HandledBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__handover___handl__4865BE2A");

            entity.HasOne(d => d.Inspection).WithMany(p => p.HandoverRecords)
                .HasForeignKey(d => d.InspectionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__handover___inspe__477199F1");

            entity.HasOne(d => d.UnitAllocation).WithMany(p => p.HandoverRecords)
                .HasForeignKey(d => d.UnitAllocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__handover___unit___467D75B8");
        });

        modelBuilder.Entity<IdentityVerification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__identity__3213E83FFD6D658F");

            entity.ToTable("identity_verifications", "core");

            entity.HasIndex(e => new { e.ReservationId, e.VerifiedAt }, "identity_verifications_reservation_idx").IsDescending(false, true);

            entity.HasIndex(e => e.VerifiedBy, "identity_verifications_verified_by_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DocumentFingerprint)
                .HasMaxLength(255)
                .HasColumnName("document_fingerprint");
            entity.Property(e => e.Notes)
                .HasMaxLength(255)
                .HasColumnName("notes");
            entity.Property(e => e.ReservationId).HasColumnName("reservation_id");
            entity.Property(e => e.Result)
                .HasMaxLength(255)
                .HasColumnName("result");
            entity.Property(e => e.VerificationMethod)
                .HasMaxLength(255)
                .HasColumnName("verification_method");
            entity.Property(e => e.VerifiedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("verified_at");
            entity.Property(e => e.VerifiedBy).HasColumnName("verified_by");

            entity.HasOne(d => d.Reservation).WithMany(p => p.IdentityVerifications)
                .HasForeignKey(d => d.ReservationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__identity___reser__2BC97F7C");

            entity.HasOne(d => d.VerifiedByNavigation).WithMany(p => p.IdentityVerifications)
                .HasForeignKey(d => d.VerifiedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__identity___verif__2CBDA3B5");
        });

        modelBuilder.Entity<Inspection>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__inspecti__3213E83F24198D74");

            entity.ToTable("inspections", "core");

            entity.HasIndex(e => e.AgreementId, "inspections_agreement_id_idx").HasFilter("([agreement_id] IS NOT NULL)");

            entity.HasIndex(e => new { e.FacilityId, e.InspectionType, e.CreatedAt }, "inspections_facility_type_date_idx").IsDescending(false, false, true);

            entity.HasIndex(e => e.InspectedBy, "inspections_inspected_by_idx");

            entity.HasIndex(e => e.ReservationId, "inspections_reservation_id_idx").HasFilter("([reservation_id] IS NOT NULL)");

            entity.HasIndex(e => e.StorageUnitId, "inspections_storage_unit_id_idx").HasFilter("([storage_unit_id] IS NOT NULL)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.FacilityId).HasColumnName("facility_id");
            entity.Property(e => e.InspectedAt).HasColumnName("inspected_at");
            entity.Property(e => e.InspectedBy).HasColumnName("inspected_by");
            entity.Property(e => e.InspectionType)
                .HasMaxLength(255)
                .HasColumnName("inspection_type");
            entity.Property(e => e.OverallCondition)
                .HasMaxLength(255)
                .HasColumnName("overall_condition");
            entity.Property(e => e.ReservationId).HasColumnName("reservation_id");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("draft")
                .HasColumnName("status");
            entity.Property(e => e.StorageUnitId).HasColumnName("storage_unit_id");
            entity.Property(e => e.Summary)
                .HasMaxLength(255)
                .HasColumnName("summary");

            entity.HasOne(d => d.Agreement).WithMany(p => p.Inspections)
                .HasForeignKey(d => d.AgreementId)
                .HasConstraintName("FK__inspectio__agree__3552E9B6");

            entity.HasOne(d => d.Facility).WithMany(p => p.Inspections)
                .HasForeignKey(d => d.FacilityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__inspectio__facil__32767D0B");

            entity.HasOne(d => d.InspectedByNavigation).WithMany(p => p.Inspections)
                .HasForeignKey(d => d.InspectedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__inspectio__inspe__36470DEF");

            entity.HasOne(d => d.Reservation).WithMany(p => p.Inspections)
                .HasForeignKey(d => d.ReservationId)
                .HasConstraintName("FK__inspectio__reser__345EC57D");

            entity.HasOne(d => d.StorageUnit).WithMany(p => p.Inspections)
                .HasForeignKey(d => d.StorageUnitId)
                .HasConstraintName("FK__inspectio__stora__336AA144");
        });

        modelBuilder.Entity<InspectionItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__inspecti__3213E83F1C247A95");

            entity.ToTable("inspection_items", "core");

            entity.HasIndex(e => e.InspectionId, "inspection_items_inspection_id_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ChargeAmount)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("charge_amount");
            entity.Property(e => e.Condition)
                .HasMaxLength(255)
                .HasColumnName("condition");
            entity.Property(e => e.InspectionId).HasColumnName("inspection_id");
            entity.Property(e => e.ItemName)
                .HasMaxLength(255)
                .HasColumnName("item_name");
            entity.Property(e => e.Notes)
                .HasMaxLength(255)
                .HasColumnName("notes");
            entity.Property(e => e.PhotoUrl)
                .HasMaxLength(255)
                .HasColumnName("photo_url");

            entity.HasOne(d => d.Inspection).WithMany(p => p.InspectionItems)
                .HasForeignKey(d => d.InspectionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__inspectio__inspe__3FD07829");
        });

        modelBuilder.Entity<IntegrationEvent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__integrat__3213E83FAE9A93FF");

            entity.ToTable("integration_events", "core");

            entity.HasIndex(e => new { e.Source, e.ExternalEventId }, "UQ__integrat__86EB106AF963411C").IsUnique();

            entity.HasIndex(e => new { e.ReceivedAt, e.Id }, "integration_events_processing_queue_idx").HasFilter("([status] IN ('received', 'failed'))");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(255)
                .HasColumnName("error_message");
            entity.Property(e => e.EventType)
                .HasMaxLength(255)
                .HasColumnName("event_type");
            entity.Property(e => e.ExternalEventId)
                .HasMaxLength(255)
                .HasColumnName("external_event_id");
            entity.Property(e => e.Payload).HasColumnName("payload");
            entity.Property(e => e.ProcessedAt).HasColumnName("processed_at");
            entity.Property(e => e.ReceivedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("received_at");
            entity.Property(e => e.Source)
                .HasMaxLength(255)
                .HasColumnName("source");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("received")
                .HasColumnName("status");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__invoices__3213E83F6609AFEF");

            entity.ToTable("invoices", "core", tb => tb.HasTrigger("trg_invoice_status_transition"));

            entity.HasIndex(e => e.TicketChargeProposalId, "UQ__invoices__DABC6CDB01A88B8E").IsUnique();

            entity.HasIndex(e => e.InvoiceNo, "UQ__invoices__F58CA1E2D09A819E").IsUnique();

            entity.HasIndex(e => new { e.AgreementId, e.BillingPeriod }, "invoices_agreement_billing_period_uidx")
                .IsUnique()
                .HasFilter("([agreement_id] IS NOT NULL AND [billing_period] IS NOT NULL AND [status]<>'voided')");

            entity.HasIndex(e => e.AgreementId, "invoices_agreement_id_idx").HasFilter("([agreement_id] IS NOT NULL)");

            entity.HasIndex(e => new { e.CustomerId, e.Status, e.DueDate }, "invoices_customer_status_due_idx");

            entity.HasIndex(e => e.DueDate, "invoices_open_due_idx").HasFilter("([status] IN ('open', 'partially_paid', 'overdue'))");

            entity.HasIndex(e => e.ReservationId, "invoices_reservation_id_idx").HasFilter("([reservation_id] IS NOT NULL)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
            entity.Property(e => e.BillingPeriod)
                .HasMaxLength(64)
                .HasColumnName("billing_period");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.Currency)
                .HasMaxLength(3)
                .IsUnicode(false)
                .HasDefaultValue("VND")
                .IsFixedLength()
                .HasColumnName("currency");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.DiscountAmount)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("discount_amount");
            entity.Property(e => e.DueDate).HasColumnName("due_date");
            entity.Property(e => e.InvoiceNo)
                .HasMaxLength(255)
                .HasColumnName("invoice_no");
            entity.Property(e => e.IssueDate).HasColumnName("issue_date");
            entity.Property(e => e.OpenedAt).HasColumnName("opened_at");
            entity.Property(e => e.PaidAmount)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("paid_amount");
            entity.Property(e => e.ReservationId).HasColumnName("reservation_id");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("draft")
                .HasColumnName("status");
            entity.Property(e => e.SubtotalAmount)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("subtotal_amount");
            entity.Property(e => e.TaxAmount)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("tax_amount");
            entity.Property(e => e.TicketChargeProposalId).HasColumnName("ticket_charge_proposal_id");
            entity.Property(e => e.TotalAmount)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("total_amount");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.VoidedAt).HasColumnName("voided_at");

            entity.HasOne(d => d.Agreement).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.AgreementId)
                .HasConstraintName("FK__invoices__agreem__4830B400");

            entity.HasOne(d => d.Customer).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__invoices__custom__46486B8E");

            entity.HasOne(d => d.Reservation).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.ReservationId)
                .HasConstraintName("FK__invoices__reserv__473C8FC7");

            entity.HasOne(d => d.TicketChargeProposal).WithOne(p => p.Invoice)
                .HasForeignKey<Invoice>(d => d.TicketChargeProposalId)
                .HasConstraintName("FK__invoices__ticket__4924D839");
        });

        modelBuilder.Entity<InvoiceLine>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__invoice___3213E83F555DA62F");

            entity.ToTable("invoice_lines", "core", tb => tb.HasTrigger("trg_invoice_lines_refresh_totals"));

            entity.HasIndex(e => e.InvoiceId, "invoice_lines_invoice_id_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.LineAmount)
                .HasComputedColumnSql("(CONVERT([numeric](14,2),round([quantity]*[unit_price],(2))))", true)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("line_amount");
            entity.Property(e => e.LineType)
                .HasMaxLength(255)
                .HasColumnName("line_type");
            entity.Property(e => e.Metadata)
                .HasDefaultValue("{}")
                .HasColumnName("metadata");
            entity.Property(e => e.Quantity)
                .HasDefaultValue(1m)
                .HasColumnType("numeric(12, 3)")
                .HasColumnName("quantity");
            entity.Property(e => e.UnitPrice)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("unit_price");

            entity.HasOne(d => d.Invoice).WithMany(p => p.InvoiceLines)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__invoice_l__invoi__5E1FF51F");
        });

        modelBuilder.Entity<LoginHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__login_hi__3213E83F95DB5DA2");

            entity.ToTable("login_history", "core");

            entity.HasIndex(e => new { e.AttemptedEmail, e.OccurredAt }, "login_history_email_occurred_idx")
                .IsDescending(false, true)
                .HasFilter("([attempted_email] IS NOT NULL)");

            entity.HasIndex(e => new { e.UserId, e.OccurredAt }, "login_history_user_occurred_idx")
                .IsDescending(false, true)
                .HasFilter("([user_id] IS NOT NULL)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AttemptedEmail)
                .HasMaxLength(255)
                .HasColumnName("attempted_email");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .HasColumnName("ip_address");
            entity.Property(e => e.OccurredAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("occurred_at");
            entity.Property(e => e.Result)
                .HasMaxLength(255)
                .HasColumnName("result");
            entity.Property(e => e.UserAgent)
                .HasMaxLength(255)
                .HasColumnName("user_agent");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.LoginHistories)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__login_his__user___7993056A");
        });

        modelBuilder.Entity<MaintenanceWorkOrder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__maintena__3213E83F73AEF50C");

            entity.ToTable("maintenance_work_orders", "core");

            entity.HasIndex(e => e.WorkOrderNo, "UQ__maintena__58954FA67EC34FED").IsUnique();

            entity.HasIndex(e => e.AssignedEmployeeId, "maintenance_work_orders_assigned_employee_id_idx").HasFilter("([assigned_employee_id] IS NOT NULL)");

            entity.HasIndex(e => new { e.FacilityId, e.Status, e.Priority, e.CreatedAt }, "maintenance_work_orders_facility_queue_idx");

            entity.HasIndex(e => e.OpenedBy, "maintenance_work_orders_opened_by_idx");

            entity.HasIndex(e => e.SourceInspectionId, "maintenance_work_orders_source_inspection_id_idx").HasFilter("([source_inspection_id] IS NOT NULL)");

            entity.HasIndex(e => e.SourceTicketId, "maintenance_work_orders_source_ticket_id_idx").HasFilter("([source_ticket_id] IS NOT NULL)");

            entity.HasIndex(e => new { e.StorageUnitId, e.Status }, "maintenance_work_orders_storage_unit_id_idx").HasFilter("([storage_unit_id] IS NOT NULL)");

            entity.HasIndex(e => e.VerifiedBy, "maintenance_work_orders_verified_by_idx").HasFilter("([verified_by] IS NOT NULL)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ActualCost)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("actual_cost");
            entity.Property(e => e.AssignedEmployeeId).HasColumnName("assigned_employee_id");
            entity.Property(e => e.BlocksBooking)
                .HasDefaultValue(true)
                .HasColumnName("blocks_booking");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.EstimatedCost)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("estimated_cost");
            entity.Property(e => e.FacilityId).HasColumnName("facility_id");
            entity.Property(e => e.OpenedBy).HasColumnName("opened_by");
            entity.Property(e => e.Priority)
                .HasMaxLength(255)
                .HasDefaultValue("normal")
                .HasColumnName("priority");
            entity.Property(e => e.SourceInspectionId).HasColumnName("source_inspection_id");
            entity.Property(e => e.SourceTicketId).HasColumnName("source_ticket_id");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("open")
                .HasColumnName("status");
            entity.Property(e => e.StorageUnitId).HasColumnName("storage_unit_id");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.VerifiedBy).HasColumnName("verified_by");
            entity.Property(e => e.WorkOrderNo)
                .HasMaxLength(255)
                .HasColumnName("work_order_no");

            entity.HasOne(d => d.AssignedEmployee).WithMany(p => p.MaintenanceWorkOrderAssignedEmployees)
                .HasForeignKey(d => d.AssignedEmployeeId)
                .HasConstraintName("FK__maintenan__assig__1E05700A");

            entity.HasOne(d => d.Facility).WithMany(p => p.MaintenanceWorkOrders)
                .HasForeignKey(d => d.FacilityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__maintenan__facil__1A34DF26");

            entity.HasOne(d => d.OpenedByNavigation).WithMany(p => p.MaintenanceWorkOrders)
                .HasForeignKey(d => d.OpenedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__maintenan__opene__25A691D2");

            entity.HasOne(d => d.SourceInspection).WithMany(p => p.MaintenanceWorkOrders)
                .HasForeignKey(d => d.SourceInspectionId)
                .HasConstraintName("FK__maintenan__sourc__1D114BD1");

            entity.HasOne(d => d.SourceTicket).WithMany(p => p.MaintenanceWorkOrders)
                .HasForeignKey(d => d.SourceTicketId)
                .HasConstraintName("FK__maintenan__sourc__1C1D2798");

            entity.HasOne(d => d.StorageUnit).WithMany(p => p.MaintenanceWorkOrders)
                .HasForeignKey(d => d.StorageUnitId)
                .HasConstraintName("FK__maintenan__stora__1B29035F");

            entity.HasOne(d => d.VerifiedByNavigation).WithMany(p => p.MaintenanceWorkOrderVerifiedByNavigations)
                .HasForeignKey(d => d.VerifiedBy)
                .HasConstraintName("FK__maintenan__verif__269AB60B");
        });

        modelBuilder.Entity<MoveOutRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__move_out__3213E83F18F51BD9");

            entity.ToTable("move_out_requests", "core");

            entity.HasIndex(e => new { e.AgreementId, e.Status, e.CreatedAt }, "move_out_requests_agreement_idx").IsDescending(false, false, true);

            entity.HasIndex(e => e.AppointmentId, "move_out_requests_appointment_id_idx").HasFilter("([appointment_id] IS NOT NULL)");

            entity.HasIndex(e => e.AgreementId, "move_out_requests_one_open_uidx")
                .IsUnique()
                .HasFilter("([status]<>'completed' AND [status]<>'cancelled')");

            entity.HasIndex(e => e.RequestedBy, "move_out_requests_requested_by_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
            entity.Property(e => e.AppointmentId).HasColumnName("appointment_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.FinalizedAt).HasColumnName("finalized_at");
            entity.Property(e => e.Reason)
                .HasMaxLength(255)
                .HasColumnName("reason");
            entity.Property(e => e.RequestedBy).HasColumnName("requested_by");
            entity.Property(e => e.RequestedMoveOutDate).HasColumnName("requested_move_out_date");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("requested")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Agreement).WithOne(p => p.MoveOutRequest)
                .HasForeignKey<MoveOutRequest>(d => d.AgreementId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__move_out___agree__6BAEFA67");

            entity.HasOne(d => d.Appointment).WithMany(p => p.MoveOutRequests)
                .HasForeignKey(d => d.AppointmentId)
                .HasConstraintName("FK__move_out___appoi__6D9742D9");

            entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.MoveOutRequests)
                .HasForeignKey(d => d.RequestedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__move_out___reque__6CA31EA0");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__notifica__3213E83F5750E434");

            entity.ToTable("notifications", "core");

            entity.HasIndex(e => e.DeduplicationKey, "UQ__notifica__336F0ABBB6A704D7").IsUnique();

            entity.HasIndex(e => new { e.ScheduledAt, e.Id }, "notifications_delivery_queue_idx").HasFilter("([status] IN ('pending', 'failed'))");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "notifications_user_created_idx").IsDescending(false, true);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Attempts).HasColumnName("attempts");
            entity.Property(e => e.Channel)
                .HasMaxLength(255)
                .HasColumnName("channel");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.DeduplicationKey)
                .HasMaxLength(255)
                .HasColumnName("deduplication_key");
            entity.Property(e => e.LastError)
                .HasMaxLength(255)
                .HasColumnName("last_error");
            entity.Property(e => e.Payload)
                .HasDefaultValue("{}")
                .HasColumnName("payload");
            entity.Property(e => e.ScheduledAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("scheduled_at");
            entity.Property(e => e.SentAt).HasColumnName("sent_at");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("pending")
                .HasColumnName("status");
            entity.Property(e => e.Subject)
                .HasMaxLength(255)
                .HasColumnName("subject");
            entity.Property(e => e.TemplateCode)
                .HasMaxLength(255)
                .HasColumnName("template_code");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__notificat__user___695C9DA1");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__payments__3213E83F1EE84ED7");

            entity.ToTable("payments", "core", tb => tb.HasTrigger("trg_payments_refresh_invoices"));

            entity.HasIndex(e => e.IdempotencyKey, "UQ__payments__A7BA59F488C60FE3").IsUnique();

            entity.HasIndex(e => new { e.CustomerId, e.CreatedAt }, "payments_customer_created_idx").IsDescending(false, true);

            entity.HasIndex(e => new { e.Provider, e.ProviderTransactionId }, "payments_provider_transaction_uidx")
                .IsUnique()
                .HasFilter("([provider_transaction_id] IS NOT NULL)");

            entity.HasIndex(e => e.TargetInvoiceId, "payments_target_invoice_id_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Amount)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("amount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.Currency)
                .HasMaxLength(3)
                .IsUnicode(false)
                .HasDefaultValue("VND")
                .IsFixedLength()
                .HasColumnName("currency");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.FailureReason)
                .HasMaxLength(255)
                .HasColumnName("failure_reason");
            entity.Property(e => e.IdempotencyKey)
                .HasMaxLength(255)
                .HasColumnName("idempotency_key");
            entity.Property(e => e.Metadata)
                .HasDefaultValue("{}")
                .HasColumnName("metadata");
            entity.Property(e => e.Method)
                .HasMaxLength(255)
                .HasColumnName("method");
            entity.Property(e => e.PaidAt).HasColumnName("paid_at");
            entity.Property(e => e.Provider)
                .HasMaxLength(255)
                .HasColumnName("provider");
            entity.Property(e => e.ProviderTransactionId)
                .HasMaxLength(255)
                .HasColumnName("provider_transaction_id");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("initiated")
                .HasColumnName("status");
            entity.Property(e => e.TargetInvoiceId).HasColumnName("target_invoice_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Customer).WithMany(p => p.Payments)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__payments__custom__67A95F59");

            entity.HasOne(d => d.TargetInvoice).WithMany(p => p.Payments)
                .HasForeignKey(d => d.TargetInvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__payments__target__689D8392");
        });

        modelBuilder.Entity<PaymentAllocation>(entity =>
        {
            entity.HasKey(e => new { e.PaymentId, e.InvoiceId }).HasName("PK__payment___6247163E1D5CC991");

            entity.ToTable("payment_allocations", "core", tb => tb.HasTrigger("trg_payment_allocation_guard"));

            entity.HasIndex(e => new { e.InvoiceId, e.PaymentId }, "payment_allocations_invoice_id_idx");

            entity.Property(e => e.PaymentId).HasColumnName("payment_id");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.AllocatedAmount)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("allocated_amount");
            entity.Property(e => e.AllocatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("allocated_at");

            entity.HasOne(d => d.Invoice).WithMany(p => p.PaymentAllocations)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__payment_a__invoi__76EBA2E9");

            entity.HasOne(d => d.Payment).WithMany(p => p.PaymentAllocations)
                .HasForeignKey(d => d.PaymentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__payment_a__payme__75F77EB0");
        });

        modelBuilder.Entity<PolicyVersion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__policy_v__3213E83FC5850671");

            entity.ToTable("policy_versions", "core", tb => tb.HasTrigger("trg_policy_version_no_overlap"));

            entity.HasIndex(e => new { e.PolicyType, e.Version }, "UQ__policy_v__913F32FAF5C65EB9").IsUnique();

            entity.HasIndex(e => e.CreatedBy, "policy_versions_created_by_idx").HasFilter("([created_by] IS NOT NULL)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.PolicyType)
                .HasMaxLength(255)
                .HasColumnName("policy_type");
            entity.Property(e => e.ValidFrom).HasColumnName("valid_from");
            entity.Property(e => e.ValidTo).HasColumnName("valid_to");
            entity.Property(e => e.Version)
                .HasMaxLength(255)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PolicyVersions)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__policy_ve__creat__44CA3770");
        });

        modelBuilder.Entity<PriceRange>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__price_ra__3213E83F2173EE04");

            entity.ToTable("price_ranges", "core", tb => tb.HasTrigger("trg_price_range_no_overlap"));

            entity.HasIndex(e => e.CreatedBy, "price_ranges_created_by_idx").HasFilter("([created_by] IS NOT NULL)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.MaxMonthlyRate)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("max_monthly_rate");
            entity.Property(e => e.MinMonthlyRate)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("min_monthly_rate");
            entity.Property(e => e.UnitTypeId).HasColumnName("unit_type_id");
            entity.Property(e => e.ValidFrom).HasColumnName("valid_from");
            entity.Property(e => e.ValidTo).HasColumnName("valid_to");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PriceRanges)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__price_ran__creat__31B762FC");

            entity.HasOne(d => d.UnitType).WithMany(p => p.PriceRanges)
                .HasForeignKey(d => d.UnitTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__price_ran__unit___2FCF1A8A");
        });

        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__promotio__3213E83F39C628D1");

            entity.ToTable("promotions", "core");

            entity.HasIndex(e => e.Code, "UQ__promotio__357D4CF9D56E4DD4").IsUnique();

            entity.HasIndex(e => new { e.ValidFrom, e.ValidTo }, "promotions_active_period_idx").HasFilter("([is_active]=(1))");

            entity.HasIndex(e => e.CreatedBy, "promotions_created_by_idx").HasFilter("([created_by] IS NOT NULL)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(255)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.DiscountType)
                .HasMaxLength(255)
                .HasColumnName("discount_type");
            entity.Property(e => e.DiscountValue)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("discount_value");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.MaxDiscountAmount)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("max_discount_amount");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.PerCustomerLimit).HasColumnName("per_customer_limit");
            entity.Property(e => e.UsageLimit).HasColumnName("usage_limit");
            entity.Property(e => e.ValidFrom).HasColumnName("valid_from");
            entity.Property(e => e.ValidTo).HasColumnName("valid_to");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Promotions)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__promotion__creat__5F7E2DAC");
        });

        modelBuilder.Entity<PromotionRedemption>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__promotio__3213E83F4B06451A");

            entity.ToTable("promotion_redemptions", "core", tb => tb.HasTrigger("trg_promotion_redemptions_validate_scope"));

            entity.HasIndex(e => new { e.CustomerId, e.PromotionId, e.Status }, "promotion_redemptions_customer_idx");

            entity.HasIndex(e => e.InvoiceId, "promotion_redemptions_invoice_id_idx").HasFilter("([invoice_id] IS NOT NULL)");

            entity.HasIndex(e => new { e.PromotionId, e.Status, e.RedeemedAt }, "promotion_redemptions_promotion_idx");

            entity.HasIndex(e => e.ReservationId, "promotion_redemptions_reservation_id_idx").HasFilter("([reservation_id] IS NOT NULL)");

            entity.HasIndex(e => new { e.PromotionId, e.ReservationId }, "promotion_redemptions_reservation_uidx")
                .IsUnique()
                .HasFilter("([reservation_id] IS NOT NULL AND [status]<>'released')");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.DiscountAmount)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("discount_amount");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.PromotionId).HasColumnName("promotion_id");
            entity.Property(e => e.RedeemedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("redeemed_at");
            entity.Property(e => e.ReservationId).HasColumnName("reservation_id");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("reserved")
                .HasColumnName("status");

            entity.HasOne(d => d.Customer).WithMany(p => p.PromotionRedemptions)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__promotion__custo__0FB750B3");

            entity.HasOne(d => d.Invoice).WithMany(p => p.PromotionRedemptions)
                .HasForeignKey(d => d.InvoiceId)
                .HasConstraintName("FK__promotion__invoi__119F9925");

            entity.HasOne(d => d.Promotion).WithMany(p => p.PromotionRedemptions)
                .HasForeignKey(d => d.PromotionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__promotion__promo__0EC32C7A");

            entity.HasOne(d => d.Reservation).WithMany(p => p.PromotionRedemptions)
                .HasForeignKey(d => d.ReservationId)
                .HasConstraintName("FK__promotion__reser__10AB74EC");
        });

        modelBuilder.Entity<PromotionRule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__promotio__3213E83F600E839C");

            entity.ToTable("promotion_rules", "core");

            entity.HasIndex(e => new { e.PromotionId, e.RuleType }, "UQ__promotio__DE90F0E956A0A645").IsUnique();

            entity.HasIndex(e => e.PromotionId, "promotion_rules_promotion_id_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Operator)
                .HasMaxLength(255)
                .HasDefaultValue("eq")
                .HasColumnName("operator");
            entity.Property(e => e.PromotionId).HasColumnName("promotion_id");
            entity.Property(e => e.RuleType)
                .HasMaxLength(255)
                .HasColumnName("rule_type");
            entity.Property(e => e.RuleValue).HasColumnName("rule_value");

            entity.HasOne(d => d.Promotion).WithMany(p => p.PromotionRules)
                .HasForeignKey(d => d.PromotionId)
                .HasConstraintName("FK__promotion__promo__662B2B3B");
        });

        modelBuilder.Entity<Refund>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__refunds__3213E83F6C90B5ED");

            entity.ToTable("refunds", "core");

            entity.HasIndex(e => e.IdempotencyKey, "UQ__refunds__A7BA59F4B0FC8B69").IsUnique();

            entity.HasIndex(e => e.AgreementId, "refunds_agreement_id_idx").HasFilter("([agreement_id] IS NOT NULL)");

            entity.HasIndex(e => e.PaymentId, "refunds_payment_id_idx");

            entity.HasIndex(e => new { e.Provider, e.ProviderRefundId }, "refunds_provider_refund_uidx")
                .IsUnique()
                .HasFilter("([provider_refund_id] IS NOT NULL)");

            entity.HasIndex(e => e.RequestedBy, "refunds_requested_by_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
            entity.Property(e => e.Amount)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("amount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.Currency)
                .HasMaxLength(3)
                .IsUnicode(false)
                .HasDefaultValue("VND")
                .IsFixedLength()
                .HasColumnName("currency");
            entity.Property(e => e.IdempotencyKey)
                .HasMaxLength(255)
                .HasColumnName("idempotency_key");
            entity.Property(e => e.PaymentId).HasColumnName("payment_id");
            entity.Property(e => e.Provider)
                .HasMaxLength(255)
                .HasColumnName("provider");
            entity.Property(e => e.ProviderRefundId)
                .HasMaxLength(255)
                .HasColumnName("provider_refund_id");
            entity.Property(e => e.Reason)
                .HasMaxLength(255)
                .HasColumnName("reason");
            entity.Property(e => e.RefundedAt).HasColumnName("refunded_at");
            entity.Property(e => e.RequestedBy).HasColumnName("requested_by");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("requested")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Agreement).WithMany(p => p.Refunds)
                .HasForeignKey(d => d.AgreementId)
                .HasConstraintName("FK__refunds__agreeme__7D98A078");

            entity.HasOne(d => d.Payment).WithMany(p => p.Refunds)
                .HasForeignKey(d => d.PaymentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__refunds__payment__7CA47C3F");

            entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.Refunds)
                .HasForeignKey(d => d.RequestedBy)
                .HasConstraintName("FK__refunds__request__025D5595");
        });

        modelBuilder.Entity<RefundApproval>(entity =>
        {
            entity.HasKey(e => e.RefundId).HasName("PK__refund_a__897E9EA3E281922A");

            entity.ToTable("refund_approvals", "core");

            entity.HasIndex(e => e.DecidedBy, "refund_approvals_decided_by_idx");

            entity.Property(e => e.RefundId)
                .ValueGeneratedNever()
                .HasColumnName("refund_id");
            entity.Property(e => e.DecidedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("decided_at");
            entity.Property(e => e.DecidedBy).HasColumnName("decided_by");
            entity.Property(e => e.Decision)
                .HasMaxLength(255)
                .HasColumnName("decision");
            entity.Property(e => e.Reason)
                .HasMaxLength(255)
                .HasColumnName("reason");

            entity.HasOne(d => d.DecidedByNavigation).WithMany(p => p.RefundApprovals)
                .HasForeignKey(d => d.DecidedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__refund_ap__decid__0AF29B96");

            entity.HasOne(d => d.Refund).WithOne(p => p.RefundApproval)
                .HasForeignKey<RefundApproval>(d => d.RefundId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__refund_ap__refun__090A5324");
        });

        modelBuilder.Entity<RentalAgreement>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__rental_a__3213E83F74E7BFF0");

            entity.ToTable("rental_agreements", "core", tb => tb.HasTrigger("trg_agreement_status_transition"));

            entity.HasIndex(e => e.ReservationId, "UQ__rental_a__31384C28E1114D31").IsUnique();

            entity.HasIndex(e => e.AgreementNo, "UQ__rental_a__A476927E4078718C").IsUnique();

            entity.HasIndex(e => new { e.Id, e.CustomerId, e.FacilityId }, "UQ__rental_a__D1775C6C828F19B8").IsUnique();

            entity.HasIndex(e => new { e.CustomerId, e.Status, e.EndDate }, "rental_agreements_customer_status_idx");

            entity.HasIndex(e => new { e.FacilityId, e.Status, e.EndDate }, "rental_agreements_facility_status_idx");

            entity.HasIndex(e => e.PolicyVersionId, "rental_agreements_policy_version_id_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ActualEndDate).HasColumnName("actual_end_date");
            entity.Property(e => e.AgreementNo)
                .HasMaxLength(255)
                .HasColumnName("agreement_no");
            entity.Property(e => e.CheckedInAt).HasColumnName("checked_in_at");
            entity.Property(e => e.CheckedOutAt).HasColumnName("checked_out_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.DepositBalance)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("deposit_balance");
            entity.Property(e => e.DepositSnapshot)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("deposit_snapshot");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.FacilityId).HasColumnName("facility_id");
            entity.Property(e => e.MonthlyRateSnapshot)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("monthly_rate_snapshot");
            entity.Property(e => e.PolicyVersionId).HasColumnName("policy_version_id");
            entity.Property(e => e.ReservationId).HasColumnName("reservation_id");
            entity.Property(e => e.SignedAt).HasColumnName("signed_at");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("draft")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Customer).WithMany(p => p.RentalAgreements)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__rental_ag__custo__04AFB25B");

            entity.HasOne(d => d.Facility).WithMany(p => p.RentalAgreements)
                .HasForeignKey(d => d.FacilityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__rental_ag__facil__05A3D694");

            entity.HasOne(d => d.PolicyVersion).WithMany(p => p.RentalAgreements)
                .HasForeignKey(d => d.PolicyVersionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__rental_ag__polic__0697FACD");

            entity.HasOne(d => d.Reservation).WithOne(p => p.RentalAgreement)
                .HasForeignKey<RentalAgreement>(d => d.ReservationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__rental_ag__reser__03BB8E22");
        });

        modelBuilder.Entity<RentalRenewal>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__rental_r__3213E83FD6FCC618");

            entity.ToTable("rental_renewals", "core");

            entity.HasIndex(e => new { e.AgreementId, e.CreatedAt }, "rental_renewals_agreement_idx").IsDescending(false, true);

            entity.HasIndex(e => e.AgreementId, "rental_renewals_one_open_uidx")
                .IsUnique()
                .HasFilter("([status] IN ('pending_payment', 'paid'))");

            entity.HasIndex(e => e.RequestedBy, "rental_renewals_requested_by_idx");

            entity.HasIndex(e => e.ReviewedBy, "rental_renewals_reviewed_by_idx").HasFilter("([reviewed_by] IS NOT NULL)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
            entity.Property(e => e.ApprovedEndDate).HasColumnName("approved_end_date");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.NewMonthlyRate)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("new_monthly_rate");
            entity.Property(e => e.OldEndDate).HasColumnName("old_end_date");
            entity.Property(e => e.OldMonthlyRate)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("old_monthly_rate");
            entity.Property(e => e.RequestedBy).HasColumnName("requested_by");
            entity.Property(e => e.RequestedEndDate).HasColumnName("requested_end_date");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("pending_payment")
                .HasColumnName("status");

            entity.HasOne(d => d.Agreement).WithOne(p => p.RentalRenewal)
                .HasForeignKey<RentalRenewal>(d => d.AgreementId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__rental_re__agree__55BFB948");

            entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.RentalRenewalRequestedByNavigations)
                .HasForeignKey(d => d.RequestedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__rental_re__reque__56B3DD81");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.RentalRenewalReviewedByNavigations)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("FK__rental_re__revie__5B78929E");
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__reservat__3213E83FB121B783");

            entity.ToTable("reservations", "core", tb => tb.HasTrigger("trg_reservation_status_transition"));

            entity.HasIndex(e => new { e.Id, e.CustomerId, e.FacilityId, e.UnitTypeId }, "UQ__reservat__6DF00CF31E5AFD72").IsUnique();

            entity.HasIndex(e => e.ReservationCode, "UQ__reservat__FA8FADE4BD1ADEE8").IsUnique();

            entity.HasIndex(e => new { e.CustomerId, e.CreatedAt }, "reservations_customer_created_idx").IsDescending(false, true);

            entity.HasIndex(e => e.FacilityRateId, "reservations_facility_rate_id_idx");

            entity.HasIndex(e => new { e.FacilityId, e.Status, e.StartDate }, "reservations_facility_status_start_idx");

            entity.HasIndex(e => e.HoldUntil, "reservations_open_hold_idx").HasFilter("([status] IN ('pending', 'awaiting_deposit'))");

            entity.HasIndex(e => e.UnitTypeId, "reservations_unit_type_id_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.BookingFeeSnapshot)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("booking_fee_snapshot");
            entity.Property(e => e.CancellationReason)
                .HasMaxLength(255)
                .HasColumnName("cancellation_reason");
            entity.Property(e => e.CancelledAt).HasColumnName("cancelled_at");
            entity.Property(e => e.ConfirmedAt).HasColumnName("confirmed_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.DepositSnapshot)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("deposit_snapshot");
            entity.Property(e => e.DiscountSnapshot)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("discount_snapshot");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.FacilityId).HasColumnName("facility_id");
            entity.Property(e => e.FacilityRateId).HasColumnName("facility_rate_id");
            entity.Property(e => e.HoldUntil).HasColumnName("hold_until");
            entity.Property(e => e.MonthlyRateSnapshot)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("monthly_rate_snapshot");
            entity.Property(e => e.QuotedTotal)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("quoted_total");
            entity.Property(e => e.ReservationCode)
                .HasMaxLength(255)
                .HasColumnName("reservation_code");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("pending")
                .HasColumnName("status");
            entity.Property(e => e.UnitTypeId).HasColumnName("unit_type_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Customer).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__reservati__custo__6DCC4D03");

            entity.HasOne(d => d.Facility).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.FacilityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__reservati__facil__6EC0713C");

            entity.HasOne(d => d.FacilityRate).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.FacilityRateId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__reservati__facil__70A8B9AE");

            entity.HasOne(d => d.UnitType).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.UnitTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__reservati__unit___6FB49575");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__roles__3213E83F47C336B5");

            entity.ToTable("roles", "core");

            entity.HasIndex(e => e.Code, "UQ__roles__357D4CF96675988C").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(255)
                .HasColumnName("code");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.DisplayName)
                .HasMaxLength(255)
                .HasColumnName("display_name");
        });

        modelBuilder.Entity<ServiceRating>(entity =>
        {
            entity.HasKey(e => e.TicketId).HasName("PK__service___D596F96B2887DCB4");

            entity.ToTable("service_ratings", "core");

            entity.HasIndex(e => e.CustomerId, "service_ratings_customer_id_idx");

            entity.Property(e => e.TicketId)
                .ValueGeneratedNever()
                .HasColumnName("ticket_id");
            entity.Property(e => e.Comment)
                .HasMaxLength(255)
                .HasColumnName("comment");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.Score).HasColumnName("score");

            entity.HasOne(d => d.Customer).WithMany(p => p.ServiceRatings)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__service_r__custo__3F9B6DFF");

            entity.HasOne(d => d.Ticket).WithOne(p => p.ServiceRating)
                .HasForeignKey<ServiceRating>(d => d.TicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__service_r__ticke__3EA749C6");
        });

        modelBuilder.Entity<ShiftAssignment>(entity =>
        {
            entity.HasKey(e => new { e.ShiftId, e.EmployeeId }).HasName("PK__shift_as__E774929A092DBDEE");

            entity.ToTable("shift_assignments", "core");

            entity.HasIndex(e => new { e.EmployeeId, e.ShiftId }, "shift_assignments_employee_id_idx");

            entity.Property(e => e.ShiftId).HasColumnName("shift_id");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.CheckInAt).HasColumnName("check_in_at");
            entity.Property(e => e.CheckOutAt).HasColumnName("check_out_at");
            entity.Property(e => e.DutyRole)
                .HasMaxLength(255)
                .HasColumnName("duty_role");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("scheduled")
                .HasColumnName("status");

            entity.HasOne(d => d.Employee).WithMany(p => p.ShiftAssignments)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__shift_ass__emplo__7DCDAAA2");

            entity.HasOne(d => d.Shift).WithMany(p => p.ShiftAssignments)
                .HasForeignKey(d => d.ShiftId)
                .HasConstraintName("FK__shift_ass__shift__7CD98669");
        });

        modelBuilder.Entity<StaffFacilityAssignment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__staff_fa__3213E83FFD9A71D4");

            entity.ToTable("staff_facility_assignments", "core", tb => tb.HasTrigger("trg_staff_facility_assignment_no_overlap"));

            entity.HasIndex(e => e.AssignedBy, "staff_facility_assignments_assigned_by_idx").HasFilter("([assigned_by] IS NOT NULL)");

            entity.HasIndex(e => new { e.FacilityId, e.AssignmentRole, e.StartsAt, e.EndsAt }, "staff_facility_assignments_facility_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AssignedBy).HasColumnName("assigned_by");
            entity.Property(e => e.AssignmentRole)
                .HasMaxLength(255)
                .HasColumnName("assignment_role");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.EndsAt).HasColumnName("ends_at");
            entity.Property(e => e.FacilityId).HasColumnName("facility_id");
            entity.Property(e => e.StartsAt).HasColumnName("starts_at");

            entity.HasOne(d => d.AssignedByNavigation).WithMany(p => p.StaffFacilityAssignments)
                .HasForeignKey(d => d.AssignedBy)
                .HasConstraintName("FK__staff_fac__assig__7A672E12");

            entity.HasOne(d => d.Employee).WithMany(p => p.StaffFacilityAssignments)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__staff_fac__emplo__778AC167");

            entity.HasOne(d => d.Facility).WithMany(p => p.StaffFacilityAssignments)
                .HasForeignKey(d => d.FacilityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__staff_fac__facil__787EE5A0");
        });

        modelBuilder.Entity<StaffShift>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__staff_sh__3213E83F1E638B20");

            entity.ToTable("staff_shifts", "core");

            entity.HasIndex(e => e.CreatedBy, "staff_shifts_created_by_idx");

            entity.HasIndex(e => new { e.FacilityId, e.StartsAt, e.EndsAt, e.Status }, "staff_shifts_facility_schedule_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EndsAt).HasColumnName("ends_at");
            entity.Property(e => e.FacilityId).HasColumnName("facility_id");
            entity.Property(e => e.ShiftName)
                .HasMaxLength(255)
                .HasColumnName("shift_name");
            entity.Property(e => e.StartsAt).HasColumnName("starts_at");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("planned")
                .HasColumnName("status");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.StaffShifts)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__staff_shi__creat__7814D14C");

            entity.HasOne(d => d.Facility).WithMany(p => p.StaffShifts)
                .HasForeignKey(d => d.FacilityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__staff_shi__facil__753864A1");
        });

        modelBuilder.Entity<StaffTask>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__staff_ta__3213E83FFDB96EA1");

            entity.ToTable("staff_tasks", "core");

            entity.HasIndex(e => new { e.AssignedEmployeeId, e.Status, e.DueAt }, "staff_tasks_assigned_employee_id_idx").HasFilter("([assigned_employee_id] IS NOT NULL)");

            entity.HasIndex(e => e.CreatedBy, "staff_tasks_created_by_idx");

            entity.HasIndex(e => new { e.FacilityId, e.Status, e.DueAt }, "staff_tasks_facility_queue_idx");

            entity.HasIndex(e => e.MaintenanceWorkOrderId, "staff_tasks_maintenance_id_idx").HasFilter("([maintenance_work_order_id] IS NOT NULL)");

            entity.HasIndex(e => e.ShiftId, "staff_tasks_shift_id_idx").HasFilter("([shift_id] IS NOT NULL)");

            entity.HasIndex(e => e.TicketId, "staff_tasks_ticket_id_idx").HasFilter("([ticket_id] IS NOT NULL)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AssignedEmployeeId).HasColumnName("assigned_employee_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DueAt).HasColumnName("due_at");
            entity.Property(e => e.FacilityId).HasColumnName("facility_id");
            entity.Property(e => e.MaintenanceWorkOrderId).HasColumnName("maintenance_work_order_id");
            entity.Property(e => e.ProgressPercent).HasColumnName("progress_percent");
            entity.Property(e => e.ShiftId).HasColumnName("shift_id");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("todo")
                .HasColumnName("status");
            entity.Property(e => e.TaskType)
                .HasMaxLength(255)
                .HasColumnName("task_type");
            entity.Property(e => e.TicketId).HasColumnName("ticket_id");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.AssignedEmployee).WithMany(p => p.StaffTasks)
                .HasForeignKey(d => d.AssignedEmployeeId)
                .HasConstraintName("FK__staff_tas__assig__30242045");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.StaffTasks)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__staff_tas__creat__37C5420D");

            entity.HasOne(d => d.Facility).WithMany(p => p.StaffTasks)
                .HasForeignKey(d => d.FacilityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__staff_tas__facil__2E3BD7D3");

            entity.HasOne(d => d.MaintenanceWorkOrder).WithMany(p => p.StaffTasks)
                .HasForeignKey(d => d.MaintenanceWorkOrderId)
                .HasConstraintName("FK__staff_tas__maint__320C68B7");

            entity.HasOne(d => d.Shift).WithMany(p => p.StaffTasks)
                .HasForeignKey(d => d.ShiftId)
                .HasConstraintName("FK__staff_tas__shift__2F2FFC0C");

            entity.HasOne(d => d.Ticket).WithMany(p => p.StaffTasks)
                .HasForeignKey(d => d.TicketId)
                .HasConstraintName("FK__staff_tas__ticke__3118447E");
        });

        modelBuilder.Entity<StorageUnit>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__storage___3213E83F401545AC");

            entity.ToTable("storage_units", "core", tb => tb.HasTrigger("trg_storage_unit_status_transition"));

            entity.HasIndex(e => new { e.Id, e.FacilityId, e.UnitTypeId }, "UQ__storage___01486F6FDC4326A5").IsUnique();

            entity.HasIndex(e => new { e.FacilityId, e.UnitCode }, "UQ__storage___17930DB60694841D").IsUnique();

            entity.HasIndex(e => new { e.Id, e.FacilityId }, "UQ__storage___C93D6694EC620F41").IsUnique();

            entity.HasIndex(e => e.AreaId, "storage_units_area_id_idx").HasFilter("([area_id] IS NOT NULL)");

            entity.HasIndex(e => new { e.FacilityId, e.UnitTypeId, e.PhysicalStatus }, "storage_units_catalog_idx").HasFilter("([is_listed]=(1))");

            entity.HasIndex(e => e.UnitTypeId, "storage_units_unit_type_id_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.FacilityId).HasColumnName("facility_id");
            entity.Property(e => e.FloorLabel)
                .HasMaxLength(255)
                .HasColumnName("floor_label");
            entity.Property(e => e.IsListed)
                .HasDefaultValue(true)
                .HasColumnName("is_listed");
            entity.Property(e => e.Notes)
                .HasMaxLength(255)
                .HasColumnName("notes");
            entity.Property(e => e.PhysicalStatus)
                .HasMaxLength(255)
                .HasDefaultValue("available")
                .HasColumnName("physical_status");
            entity.Property(e => e.UnitCode)
                .HasMaxLength(255)
                .HasColumnName("unit_code");
            entity.Property(e => e.UnitTypeId).HasColumnName("unit_type_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.ZoneLabel)
                .HasMaxLength(255)
                .HasColumnName("zone_label");

            entity.HasOne(d => d.Area).WithMany(p => p.StorageUnits)
                .HasForeignKey(d => d.AreaId)
                .HasConstraintName("FK__storage_u__area___1AD3FDA4");

            entity.HasOne(d => d.Facility).WithMany(p => p.StorageUnits)
                .HasForeignKey(d => d.FacilityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__storage_u__facil__18EBB532");

            entity.HasOne(d => d.UnitType).WithMany(p => p.StorageUnits)
                .HasForeignKey(d => d.UnitTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__storage_u__unit___19DFD96B");
        });

        modelBuilder.Entity<StorageUnitHaTdt>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("StorageUnitHaTDT", "core");

            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.FacilityId).HasColumnName("facility_id");
            entity.Property(e => e.FloorLabel)
                .HasMaxLength(255)
                .HasColumnName("floor_label");
            entity.Property(e => e.IsListed).HasColumnName("is_listed");
            entity.Property(e => e.Notes)
                .HasMaxLength(255)
                .HasColumnName("notes");
            entity.Property(e => e.PhysicalStatus)
                .HasMaxLength(255)
                .HasColumnName("physical_status");
            entity.Property(e => e.StorageUnitHaTdtid)
                .ValueGeneratedOnAdd()
                .HasColumnName("StorageUnitHaTDTId");
            entity.Property(e => e.UnitCode)
                .HasMaxLength(255)
                .HasColumnName("unit_code");
            entity.Property(e => e.UnitTypeHaTdtid).HasColumnName("UnitTypeHaTDTId");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.ZoneLabel)
                .HasMaxLength(255)
                .HasColumnName("zone_label");
        });

        modelBuilder.Entity<SupportTicket>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__support___3213E83F1DA69E75");

            entity.ToTable("support_tickets", "core");

            entity.HasIndex(e => e.TicketNo, "UQ__support___D596C19626217F01").IsUnique();

            entity.HasIndex(e => e.AgreementId, "support_tickets_agreement_id_idx").HasFilter("([agreement_id] IS NOT NULL)");

            entity.HasIndex(e => new { e.CustomerId, e.CreatedAt }, "support_tickets_customer_created_idx").IsDescending(false, true);

            entity.HasIndex(e => new { e.FacilityId, e.Status, e.Priority, e.CreatedAt }, "support_tickets_facility_queue_idx");

            entity.HasIndex(e => e.StorageUnitId, "support_tickets_storage_unit_id_idx").HasFilter("([storage_unit_id] IS NOT NULL)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
            entity.Property(e => e.Category)
                .HasMaxLength(255)
                .HasColumnName("category");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.FacilityId).HasColumnName("facility_id");
            entity.Property(e => e.Priority)
                .HasMaxLength(255)
                .HasDefaultValue("normal")
                .HasColumnName("priority");
            entity.Property(e => e.Resolution)
                .HasMaxLength(255)
                .HasColumnName("resolution");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("open")
                .HasColumnName("status");
            entity.Property(e => e.StorageUnitId).HasColumnName("storage_unit_id");
            entity.Property(e => e.Subject)
                .HasMaxLength(255)
                .HasColumnName("subject");
            entity.Property(e => e.TicketNo)
                .HasMaxLength(255)
                .HasColumnName("ticket_no");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Agreement).WithMany(p => p.SupportTicketAgreements)
                .HasForeignKey(d => d.AgreementId)
                .HasConstraintName("FK__support_t__agree__084B3915");

            entity.HasOne(d => d.Customer).WithMany(p => p.SupportTickets)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__support_t__custo__0662F0A3");

            entity.HasOne(d => d.Facility).WithMany(p => p.SupportTickets)
                .HasForeignKey(d => d.FacilityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__support_t__facil__075714DC");

            entity.HasOne(d => d.StorageUnit).WithMany(p => p.SupportTicketStorageUnits)
                .HasForeignKey(d => d.StorageUnitId)
                .HasConstraintName("FK__support_t__stora__093F5D4E");

            entity.HasOne(d => d.StorageUnitNavigation).WithMany(p => p.SupportTicketStorageUnitNavigations)
                .HasPrincipalKey(p => new { p.Id, p.FacilityId })
                .HasForeignKey(d => new { d.StorageUnitId, d.FacilityId })
                .HasConstraintName("FK__support_tickets__11D4A34F");

            entity.HasOne(d => d.RentalAgreement).WithMany(p => p.SupportTicketRentalAgreements)
                .HasPrincipalKey(p => new { p.Id, p.CustomerId, p.FacilityId })
                .HasForeignKey(d => new { d.AgreementId, d.CustomerId, d.FacilityId })
                .HasConstraintName("FK__support_tickets__10E07F16");
        });

        modelBuilder.Entity<TicketAssignment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ticket_a__3213E83FD8FED18D");

            entity.ToTable("ticket_assignments", "core");

            entity.HasIndex(e => e.AssignedBy, "ticket_assignments_assigned_by_idx").HasFilter("([assigned_by] IS NOT NULL)");

            entity.HasIndex(e => new { e.EmployeeId, e.EndedAt }, "ticket_assignments_employee_id_idx");

            entity.HasIndex(e => e.TicketId, "ticket_assignments_one_active_uidx")
                .IsUnique()
                .HasFilter("([ended_at] IS NULL)");

            entity.HasIndex(e => new { e.TicketId, e.AssignedAt }, "ticket_assignments_ticket_history_idx").IsDescending(false, true);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("assigned_at");
            entity.Property(e => e.AssignedBy).HasColumnName("assigned_by");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.EndReason)
                .HasMaxLength(255)
                .HasColumnName("end_reason");
            entity.Property(e => e.EndedAt).HasColumnName("ended_at");
            entity.Property(e => e.TicketId).HasColumnName("ticket_id");

            entity.HasOne(d => d.AssignedByNavigation).WithMany(p => p.TicketAssignments)
                .HasForeignKey(d => d.AssignedBy)
                .HasConstraintName("FK__ticket_as__assig__1975C517");

            entity.HasOne(d => d.Employee).WithMany(p => p.TicketAssignments)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ticket_as__emplo__1881A0DE");

            entity.HasOne(d => d.Ticket).WithOne(p => p.TicketAssignment)
                .HasForeignKey<TicketAssignment>(d => d.TicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ticket_as__ticke__178D7CA5");
        });

        modelBuilder.Entity<TicketAttachment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ticket_a__3213E83FEB7ACA3B");

            entity.ToTable("ticket_attachments", "core");

            entity.HasIndex(e => e.MessageId, "ticket_attachments_message_id_idx").HasFilter("([message_id] IS NOT NULL)");

            entity.HasIndex(e => new { e.TicketId, e.CreatedAt }, "ticket_attachments_ticket_id_idx");

            entity.HasIndex(e => e.UploadedBy, "ticket_attachments_uploaded_by_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.FileName)
                .HasMaxLength(255)
                .HasColumnName("file_name");
            entity.Property(e => e.FileSizeBytes).HasColumnName("file_size_bytes");
            entity.Property(e => e.MessageId).HasColumnName("message_id");
            entity.Property(e => e.MimeType)
                .HasMaxLength(255)
                .HasColumnName("mime_type");
            entity.Property(e => e.ObjectUrl)
                .HasMaxLength(255)
                .HasColumnName("object_url");
            entity.Property(e => e.Sha256)
                .HasMaxLength(255)
                .HasColumnName("sha256");
            entity.Property(e => e.TicketId).HasColumnName("ticket_id");
            entity.Property(e => e.UploadedBy).HasColumnName("uploaded_by");

            entity.HasOne(d => d.Message).WithMany(p => p.TicketAttachmentMessages)
                .HasForeignKey(d => d.MessageId)
                .HasConstraintName("FK__ticket_at__messa__26CFC035");

            entity.HasOne(d => d.Ticket).WithMany(p => p.TicketAttachments)
                .HasForeignKey(d => d.TicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ticket_at__ticke__25DB9BFC");

            entity.HasOne(d => d.UploadedByNavigation).WithMany(p => p.TicketAttachments)
                .HasForeignKey(d => d.UploadedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ticket_at__uploa__27C3E46E");

            entity.HasOne(d => d.TicketMessage).WithMany(p => p.TicketAttachmentTicketMessages)
                .HasPrincipalKey(p => new { p.Id, p.TicketId })
                .HasForeignKey(d => new { d.MessageId, d.TicketId })
                .HasConstraintName("FK__ticket_attachmen__2AA05119");
        });

        modelBuilder.Entity<TicketChargeApproval>(entity =>
        {
            entity.HasKey(e => e.ProposalId).HasName("PK__ticket_c__A7BC641C435D978F");

            entity.ToTable("ticket_charge_approvals", "core");

            entity.HasIndex(e => e.DecidedBy, "ticket_charge_approvals_decided_by_idx");

            entity.Property(e => e.ProposalId)
                .ValueGeneratedNever()
                .HasColumnName("proposal_id");
            entity.Property(e => e.DecidedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("decided_at");
            entity.Property(e => e.DecidedBy).HasColumnName("decided_by");
            entity.Property(e => e.Decision)
                .HasMaxLength(255)
                .HasColumnName("decision");
            entity.Property(e => e.Reason)
                .HasMaxLength(255)
                .HasColumnName("reason");

            entity.HasOne(d => d.DecidedByNavigation).WithMany(p => p.TicketChargeApprovals)
                .HasForeignKey(d => d.DecidedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ticket_ch__decid__3AD6B8E2");

            entity.HasOne(d => d.Proposal).WithOne(p => p.TicketChargeApproval)
                .HasForeignKey<TicketChargeApproval>(d => d.ProposalId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ticket_ch__propo__38EE7070");
        });

        modelBuilder.Entity<TicketChargeProposal>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ticket_c__3213E83F9CB5A852");

            entity.ToTable("ticket_charge_proposals", "core");

            entity.HasIndex(e => e.ProposedBy, "ticket_charge_proposals_proposed_by_idx");

            entity.HasIndex(e => new { e.TicketId, e.Status, e.CreatedAt }, "ticket_charge_proposals_ticket_idx").IsDescending(false, false, true);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Amount)
                .HasColumnType("numeric(14, 2)")
                .HasColumnName("amount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.ProposedBy).HasColumnName("proposed_by");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("pending")
                .HasColumnName("status");
            entity.Property(e => e.TicketId).HasColumnName("ticket_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.ProposedByNavigation).WithMany(p => p.TicketChargeProposals)
                .HasForeignKey(d => d.ProposedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ticket_ch__propo__314D4EA8");

            entity.HasOne(d => d.Ticket).WithMany(p => p.TicketChargeProposals)
                .HasForeignKey(d => d.TicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ticket_ch__ticke__30592A6F");
        });

        modelBuilder.Entity<TicketMessage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ticket_m__3213E83F379E3A8F");

            entity.ToTable("ticket_messages", "core");

            entity.HasIndex(e => new { e.Id, e.TicketId }, "UQ__ticket_m__9F4A87A8C907397C").IsUnique();

            entity.HasIndex(e => e.AuthorUserId, "ticket_messages_author_user_id_idx").HasFilter("([author_user_id] IS NOT NULL)");

            entity.HasIndex(e => new { e.TicketId, e.CreatedAt }, "ticket_messages_ticket_created_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AuthorUserId).HasColumnName("author_user_id");
            entity.Property(e => e.Body)
                .HasMaxLength(255)
                .HasColumnName("body");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.IsInternal).HasColumnName("is_internal");
            entity.Property(e => e.TicketId).HasColumnName("ticket_id");

            entity.HasOne(d => d.AuthorUser).WithMany(p => p.TicketMessages)
                .HasForeignKey(d => d.AuthorUserId)
                .HasConstraintName("FK__ticket_me__autho__2022C2A6");

            entity.HasOne(d => d.Ticket).WithMany(p => p.TicketMessages)
                .HasForeignKey(d => d.TicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ticket_me__ticke__1F2E9E6D");
        });

        modelBuilder.Entity<UnitAllocation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__unit_all__3213E83F3E8D7AD4");

            entity.ToTable("unit_allocations", "core", tb => tb.HasTrigger("trg_unit_allocation_no_overlap"));

            entity.HasIndex(e => new { e.AgreementId, e.AllocationStartDate, e.AllocationEndDate }, "unit_allocations_agreement_period_idx").HasFilter("([agreement_id] IS NOT NULL)");

            entity.HasIndex(e => e.AssignedBy, "unit_allocations_assigned_by_idx").HasFilter("([assigned_by] IS NOT NULL)");

            entity.HasIndex(e => e.ReservationId, "unit_allocations_one_active_reservation_uidx")
                .IsUnique()
                .HasFilter("([status]='active' AND [reservation_id] IS NOT NULL)");

            entity.HasIndex(e => new { e.StorageUnitId, e.AllocationStartDate, e.AllocationEndDate }, "unit_allocations_unit_period_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
            entity.Property(e => e.AllocationEndDate).HasColumnName("allocation_end_date");
            entity.Property(e => e.AllocationKind)
                .HasMaxLength(255)
                .HasColumnName("allocation_kind");
            entity.Property(e => e.AllocationStartDate).HasColumnName("allocation_start_date");
            entity.Property(e => e.AssignedBy).HasColumnName("assigned_by");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.EndedAt).HasColumnName("ended_at");
            entity.Property(e => e.Reason)
                .HasMaxLength(255)
                .HasColumnName("reason");
            entity.Property(e => e.ReservationId).HasColumnName("reservation_id");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("active")
                .HasColumnName("status");
            entity.Property(e => e.StorageUnitId).HasColumnName("storage_unit_id");

            entity.HasOne(d => d.Agreement).WithMany(p => p.UnitAllocations)
                .HasForeignKey(d => d.AgreementId)
                .HasConstraintName("FK__unit_allo__agree__14E61A24");

            entity.HasOne(d => d.AssignedByNavigation).WithMany(p => p.UnitAllocations)
                .HasForeignKey(d => d.AssignedBy)
                .HasConstraintName("FK__unit_allo__assig__18B6AB08");

            entity.HasOne(d => d.Reservation).WithOne(p => p.UnitAllocation)
                .HasForeignKey<UnitAllocation>(d => d.ReservationId)
                .HasConstraintName("FK__unit_allo__reser__13F1F5EB");

            entity.HasOne(d => d.StorageUnit).WithMany(p => p.UnitAllocations)
                .HasForeignKey(d => d.StorageUnitId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__unit_allo__stora__12FDD1B2");
        });

        modelBuilder.Entity<UnitMapPosition>(entity =>
        {
            entity.HasKey(e => e.UnitId).HasName("PK__unit_map__D3AF5BD7302169A8");

            entity.ToTable("unit_map_positions", "core");

            entity.HasIndex(e => e.AreaId, "unit_map_positions_area_id_idx");

            entity.Property(e => e.UnitId)
                .ValueGeneratedNever()
                .HasColumnName("unit_id");
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.Height)
                .HasColumnType("numeric(10, 2)")
                .HasColumnName("height");
            entity.Property(e => e.Metadata)
                .HasDefaultValue("{}")
                .HasColumnName("metadata");
            entity.Property(e => e.RotationDegrees)
                .HasColumnType("numeric(6, 2)")
                .HasColumnName("rotation_degrees");
            entity.Property(e => e.Width)
                .HasColumnType("numeric(10, 2)")
                .HasColumnName("width");
            entity.Property(e => e.X)
                .HasColumnType("numeric(10, 2)")
                .HasColumnName("x");
            entity.Property(e => e.Y)
                .HasColumnType("numeric(10, 2)")
                .HasColumnName("y");

            entity.HasOne(d => d.Area).WithMany(p => p.UnitMapPositions)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__unit_map___area___236943A5");

            entity.HasOne(d => d.Unit).WithOne(p => p.UnitMapPosition)
                .HasForeignKey<UnitMapPosition>(d => d.UnitId)
                .HasConstraintName("FK__unit_map___unit___22751F6C");
        });

        modelBuilder.Entity<UnitStatusHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__unit_sta__3213E83F20E95C47");

            entity.ToTable("unit_status_history", "core");

            entity.HasIndex(e => e.ChangedBy, "unit_status_history_changed_by_idx").HasFilter("([changed_by] IS NOT NULL)");

            entity.HasIndex(e => new { e.StorageUnitId, e.ChangedAt }, "unit_status_history_unit_changed_idx").IsDescending(false, true);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ChangedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("changed_at");
            entity.Property(e => e.ChangedBy).HasColumnName("changed_by");
            entity.Property(e => e.NewStatus)
                .HasMaxLength(255)
                .HasColumnName("new_status");
            entity.Property(e => e.OldStatus)
                .HasMaxLength(255)
                .HasColumnName("old_status");
            entity.Property(e => e.Reason)
                .HasMaxLength(255)
                .HasColumnName("reason");
            entity.Property(e => e.StorageUnitId).HasColumnName("storage_unit_id");

            entity.HasOne(d => d.ChangedByNavigation).WithMany(p => p.UnitStatusHistories)
                .HasForeignKey(d => d.ChangedBy)
                .HasConstraintName("FK__unit_stat__chang__2BFE89A6");

            entity.HasOne(d => d.StorageUnit).WithMany(p => p.UnitStatusHistories)
                .HasForeignKey(d => d.StorageUnitId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__unit_stat__stora__2B0A656D");
        });

        modelBuilder.Entity<UnitTransferRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__unit_tra__3213E83F997BA96F");

            entity.ToTable("unit_transfer_requests", "core");

            entity.HasIndex(e => new { e.AgreementId, e.Status, e.CreatedAt }, "unit_transfer_requests_agreement_idx").IsDescending(false, false, true);

            entity.HasIndex(e => e.FromUnitId, "unit_transfer_requests_from_unit_id_idx");

            entity.HasIndex(e => e.AgreementId, "unit_transfer_requests_one_open_uidx")
                .IsUnique()
                .HasFilter("([status] IN ('pending', 'approved', 'scheduled'))");

            entity.HasIndex(e => e.RequestedBy, "unit_transfer_requests_requested_by_idx");

            entity.HasIndex(e => e.RequestedUnitTypeId, "unit_transfer_requests_requested_unit_type_id_idx");

            entity.HasIndex(e => e.ReviewedBy, "unit_transfer_requests_reviewed_by_idx").HasFilter("([reviewed_by] IS NOT NULL)");

            entity.HasIndex(e => e.ToUnitId, "unit_transfer_requests_to_unit_id_idx").HasFilter("([to_unit_id] IS NOT NULL)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.FromUnitId).HasColumnName("from_unit_id");
            entity.Property(e => e.Reason)
                .HasMaxLength(255)
                .HasColumnName("reason");
            entity.Property(e => e.RequestedBy).HasColumnName("requested_by");
            entity.Property(e => e.RequestedEffectiveDate).HasColumnName("requested_effective_date");
            entity.Property(e => e.RequestedUnitTypeId).HasColumnName("requested_unit_type_id");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("pending")
                .HasColumnName("status");
            entity.Property(e => e.ToUnitId).HasColumnName("to_unit_id");

            entity.HasOne(d => d.Agreement).WithOne(p => p.UnitTransferRequest)
                .HasForeignKey<UnitTransferRequest>(d => d.AgreementId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__unit_tran__agree__61316BF4");

            entity.HasOne(d => d.FromUnit).WithMany(p => p.UnitTransferRequestFromUnits)
                .HasForeignKey(d => d.FromUnitId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__unit_tran__from___6319B466");

            entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.UnitTransferRequestRequestedByNavigations)
                .HasForeignKey(d => d.RequestedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__unit_tran__reque__66EA454A");

            entity.HasOne(d => d.RequestedUnitType).WithMany(p => p.UnitTransferRequests)
                .HasForeignKey(d => d.RequestedUnitTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__unit_tran__reque__6225902D");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.UnitTransferRequestReviewedByNavigations)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("FK__unit_tran__revie__67DE6983");

            entity.HasOne(d => d.ToUnit).WithMany(p => p.UnitTransferRequestToUnits)
                .HasForeignKey(d => d.ToUnitId)
                .HasConstraintName("FK__unit_tran__to_un__640DD89F");
        });

        modelBuilder.Entity<UnitType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__unit_typ__3213E83F18088A55");

            entity.ToTable("unit_types", "core");

            entity.HasIndex(e => e.Code, "UQ__unit_typ__357D4CF933DE2D48").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AreaM2)
                .HasComputedColumnSql("(CONVERT([numeric](10,2),[width_m]*[length_m]))", true)
                .HasColumnType("numeric(10, 2)")
                .HasColumnName("area_m2");
            entity.Property(e => e.ClimateControlled).HasColumnName("climate_controlled");
            entity.Property(e => e.Code)
                .HasMaxLength(255)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.HeightM)
                .HasColumnType("numeric(8, 2)")
                .HasColumnName("height_m");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.LengthM)
                .HasColumnType("numeric(8, 2)")
                .HasColumnName("length_m");
            entity.Property(e => e.MaxWeightKg)
                .HasColumnType("numeric(12, 2)")
                .HasColumnName("max_weight_kg");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.VolumeM3)
                .HasComputedColumnSql("(CONVERT([numeric](12,2),([width_m]*[length_m])*[height_m]))", true)
                .HasColumnType("numeric(12, 2)")
                .HasColumnName("volume_m3");
            entity.Property(e => e.WidthM)
                .HasColumnType("numeric(8, 2)")
                .HasColumnName("width_m");
        });

        modelBuilder.Entity<UnitTypeHaTdt>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("UnitTypeHaTDT", "core");

            entity.Property(e => e.AreaM2)
                .HasColumnType("numeric(10, 2)")
                .HasColumnName("area_m2");
            entity.Property(e => e.ClimateControlled).HasColumnName("climate_controlled");
            entity.Property(e => e.Code)
                .HasMaxLength(255)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.HeightM)
                .HasColumnType("numeric(8, 2)")
                .HasColumnName("height_m");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.LengthM)
                .HasColumnType("numeric(8, 2)")
                .HasColumnName("length_m");
            entity.Property(e => e.MaxWeightKg)
                .HasColumnType("numeric(12, 2)")
                .HasColumnName("max_weight_kg");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.UnitTypeHaTdtid)
                .ValueGeneratedOnAdd()
                .HasColumnName("UnitTypeHaTDTId");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.VolumeM3)
                .HasColumnType("numeric(12, 2)")
                .HasColumnName("volume_m3");
            entity.Property(e => e.WidthM)
                .HasColumnType("numeric(8, 2)")
                .HasColumnName("width_m");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__users__3213E83F69F02F0F");

            entity.ToTable("users", "core");

            entity.HasIndex(e => e.Email, "users_email_unique").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(255)
                .HasColumnName("phone_number");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValue("active")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RoleId }).HasName("PK__user_rol__6EDEA1530C21B709");

            entity.ToTable("user_roles", "core");

            entity.HasIndex(e => e.GrantedBy, "user_roles_granted_by_idx").HasFilter("([granted_by] IS NOT NULL)");

            entity.HasIndex(e => new { e.RoleId, e.UserId }, "user_roles_role_id_idx");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.GrantedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("granted_at");
            entity.Property(e => e.GrantedBy).HasColumnName("granted_by");

            entity.HasOne(d => d.GrantedByNavigation).WithMany(p => p.UserRoleGrantedByNavigations)
                .HasForeignKey(d => d.GrantedBy)
                .HasConstraintName("FK__user_role__grant__5CD6CB2B");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__user_role__role___5BE2A6F2");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoleUsers)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__user_role__user___5AEE82B9");
        });
        modelBuilder.HasSequence("agreement_no_seq", "core").StartsAt(1001L);
        modelBuilder.HasSequence("invoice_no_seq", "core").StartsAt(1001L);
        modelBuilder.HasSequence("reservation_code_seq", "core").StartsAt(1001L);
        modelBuilder.HasSequence("ticket_no_seq", "core").StartsAt(1001L);
        modelBuilder.HasSequence("work_order_no_seq", "core").StartsAt(1001L);

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
