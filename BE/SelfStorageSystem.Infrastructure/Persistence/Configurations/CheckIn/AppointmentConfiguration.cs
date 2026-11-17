using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.CheckIn;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__appointm__3213E83F154E5845");
        entity.ToTable("appointments", "core", tb =>
        {
            tb.HasTrigger("trg_appointments_validate_scope");
            tb.HasCheckConstraint("CK_appointments_type", "[appointment_type] IN ('check_in', 'check_out', 'transfer', 'support', 'inspection')");
            tb.HasCheckConstraint("CK_appointments_status", "[status] IN ('scheduled', 'confirmed', 'in_progress', 'completed', 'cancelled', 'no_show')");
            tb.HasCheckConstraint("CK_appointments_xor_target", "(CASE WHEN [reservation_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [agreement_id] IS NULL THEN 0 ELSE 1 END) = 1");
            tb.HasCheckConstraint("CK_appointments_dates", "[ends_at] > [starts_at]");
        });

        entity.HasIndex(e => e.AgreementId, "appointments_agreement_id_idx").HasFilter("([agreement_id] IS NOT NULL)");
        entity.HasIndex(e => e.AssignedStaffId, "appointments_assigned_staff_id_idx").HasFilter("([assigned_staff_id] IS NOT NULL)");
        entity.HasIndex(e => new { e.FacilityId, e.StartsAt, e.Status }, "appointments_facility_schedule_idx");
        entity.HasIndex(e => e.ReservationId, "appointments_reservation_id_idx").HasFilter("([reservation_id] IS NOT NULL)");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
        entity.Property(e => e.AppointmentType).HasMaxLength(255).HasColumnName("appointment_type");
        entity.Property(e => e.AssignedStaffId).HasColumnName("assigned_staff_id");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.EndsAt).HasColumnName("ends_at");
        entity.Property(e => e.FacilityId).HasColumnName("facility_id");
        entity.Property(e => e.Notes).HasMaxLength(255).HasColumnName("notes");
        entity.Property(e => e.ReservationId).HasColumnName("reservation_id");
        entity.Property(e => e.StartsAt).HasColumnName("starts_at");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("scheduled").HasColumnName("status");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("updated_at");

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
    }
}
