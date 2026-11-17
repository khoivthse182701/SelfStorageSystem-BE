using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Operations;

public sealed class ShiftAssignmentConfiguration : IEntityTypeConfiguration<ShiftAssignment>
{
    public void Configure(EntityTypeBuilder<ShiftAssignment> entity)
    {
        entity.HasKey(e => new { e.ShiftId, e.EmployeeId }).HasName("PK__shift_as__E774929A092DBDEE");
        entity.ToTable("shift_assignments", "core", tb =>
        {
            tb.HasTrigger("trg_shift_assignments_validate_scope");
            tb.HasCheckConstraint("CK_shift_assignments_duty_role", EnumDbMapper.ToSqlCheckInClause<DutyRole>("duty_role"));
            tb.HasCheckConstraint("CK_shift_assignments_status", EnumDbMapper.ToSqlCheckInClause<ShiftAssignmentStatus>("status"));
            tb.HasCheckConstraint("CK_shift_assignments_check_times", "[check_out_at] IS NULL OR ([check_in_at] IS NOT NULL AND [check_out_at] >= [check_in_at])");
        });

        entity.HasIndex(e => new { e.EmployeeId, e.ShiftId }, "shift_assignments_employee_id_idx");

        entity.Property(e => e.ShiftId).HasColumnName("shift_id");
        entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
        entity.Property(e => e.CheckInAt).HasColumnName("check_in_at");
        entity.Property(e => e.CheckOutAt).HasColumnName("check_out_at");
        entity.Property(e => e.DutyRole).HasMaxLength(255).HasColumnName("duty_role");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("scheduled").HasColumnName("status");

        entity.HasOne(d => d.Employee).WithMany(p => p.ShiftAssignments)
            .HasForeignKey(d => d.EmployeeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__shift_ass__emplo__7DCDAAA2");

        entity.HasOne(d => d.Shift).WithMany(p => p.ShiftAssignments)
            .HasForeignKey(d => d.ShiftId)
            .HasConstraintName("FK__shift_ass__shift__7CD98669");
    }
}
