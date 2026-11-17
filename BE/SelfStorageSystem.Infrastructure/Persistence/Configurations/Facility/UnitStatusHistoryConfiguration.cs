using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Facility;

public sealed class UnitStatusHistoryConfiguration : IEntityTypeConfiguration<UnitStatusHistory>
{
    public void Configure(EntityTypeBuilder<UnitStatusHistory> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__unit_sta__3213E83F20E95C47");
        entity.ToTable("unit_status_history", "core");

        entity.HasIndex(e => e.ChangedBy, "unit_status_history_changed_by_idx").HasFilter("([changed_by] IS NOT NULL)");
        entity.HasIndex(e => new { e.StorageUnitId, e.ChangedAt }, "unit_status_history_unit_changed_idx").IsDescending(false, true);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.ChangedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("changed_at");
        entity.Property(e => e.ChangedBy).HasColumnName("changed_by");
        entity.Property(e => e.NewStatus).HasMaxLength(255).HasColumnName("new_status");
        entity.Property(e => e.OldStatus).HasMaxLength(255).HasColumnName("old_status");
        entity.Property(e => e.Reason).HasMaxLength(255).HasColumnName("reason");
        entity.Property(e => e.StorageUnitId).HasColumnName("storage_unit_id");

        entity.HasOne(d => d.ChangedByNavigation).WithMany(p => p.UnitStatusHistories)
            .HasForeignKey(d => d.ChangedBy)
            .HasConstraintName("FK__unit_stat__chang__2BFE89A6");

        entity.HasOne(d => d.StorageUnit).WithMany(p => p.UnitStatusHistories)
            .HasForeignKey(d => d.StorageUnitId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__unit_stat__stora__2B0A656D");
    }
}
