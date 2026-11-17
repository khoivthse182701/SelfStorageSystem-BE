using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Facility;

public sealed class StaffFacilityAssignmentConfiguration : IEntityTypeConfiguration<StaffFacilityAssignment>
{
    public void Configure(EntityTypeBuilder<StaffFacilityAssignment> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__staff_fa__3213E83FFD9A71D4");
        entity.ToTable("staff_facility_assignments", "core", tb =>
        {
            tb.HasTrigger("trg_staff_facility_assignment_no_overlap");
            tb.HasCheckConstraint("CK_staff_facility_assignments_role", "[assignment_role] IN ('facility_staff', 'facility_manager')");
            tb.HasCheckConstraint("CK_staff_facility_assignments_period", "[ends_at] IS NULL OR [ends_at] > [starts_at]");
        });

        entity.HasIndex(e => e.AssignedBy, "staff_facility_assignments_assigned_by_idx").HasFilter("([assigned_by] IS NOT NULL)");
        entity.HasIndex(e => new { e.FacilityId, e.AssignmentRole, e.StartsAt, e.EndsAt }, "staff_facility_assignments_facility_idx");
        entity.HasIndex(e => new { e.EmployeeId, e.FacilityId, e.EndsAt }, "ix_staff_facility_assignments_lookup");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AssignedBy).HasColumnName("assigned_by");
        entity.Property(e => e.AssignmentRole).HasMaxLength(255).HasColumnName("assignment_role");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
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
    }
}
