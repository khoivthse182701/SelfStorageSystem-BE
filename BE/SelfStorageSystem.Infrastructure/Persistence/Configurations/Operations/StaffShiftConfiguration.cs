using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Operations;

public sealed class StaffShiftConfiguration : IEntityTypeConfiguration<StaffShift>
{
    public void Configure(EntityTypeBuilder<StaffShift> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__staff_sh__3213E83F1E638B20");
        entity.ToTable("staff_shifts", "core", tb =>
        {
            tb.HasCheckConstraint("CK_staff_shifts_status", EnumDbMapper.ToSqlCheckInClause<StaffShiftStatus>("status"));
            tb.HasCheckConstraint("CK_staff_shifts_dates", "[ends_at] > [starts_at]");
        });

        entity.HasIndex(e => e.CreatedBy, "staff_shifts_created_by_idx");
        entity.HasIndex(e => new { e.FacilityId, e.StartsAt, e.EndsAt, e.Status }, "staff_shifts_facility_schedule_idx");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.CreatedBy).HasColumnName("created_by");
        entity.Property(e => e.EndsAt).HasColumnName("ends_at");
        entity.Property(e => e.FacilityId).HasColumnName("facility_id");
        entity.Property(e => e.ShiftName).HasMaxLength(255).HasColumnName("shift_name");
        entity.Property(e => e.StartsAt).HasColumnName("starts_at");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("planned").HasColumnName("status");

        entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.StaffShifts)
            .HasForeignKey(d => d.CreatedBy)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__staff_shi__creat__7814D14C");

        entity.HasOne(d => d.Facility).WithMany(p => p.StaffShifts)
            .HasForeignKey(d => d.FacilityId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__staff_shi__facil__753864A1");
    }
}
