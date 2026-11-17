using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.ReservationLease;

public sealed class UnitAllocationConfiguration : IEntityTypeConfiguration<UnitAllocation>
{
    public void Configure(EntityTypeBuilder<UnitAllocation> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__unit_all__3213E83F3E8D7AD4");
        entity.ToTable("unit_allocations", "core", tb =>
        {
            tb.HasTrigger("trg_unit_allocation_no_overlap");
            tb.HasTrigger("trg_unit_allocations_validate_scope");
            tb.HasCheckConstraint("CK_unit_allocations_kind", EnumDbMapper.ToSqlCheckInClause<AllocationKind>("allocation_kind"));
            tb.HasCheckConstraint("CK_unit_allocations_status", EnumDbMapper.ToSqlCheckInClause<AllocationStatus>("status"));
            tb.HasCheckConstraint("CK_unit_allocations_xor_target", "(CASE WHEN [reservation_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [agreement_id] IS NULL THEN 0 ELSE 1 END) = 1");
            tb.HasCheckConstraint("CK_unit_allocations_dates", "[allocation_end_date] > [allocation_start_date]");
            tb.HasCheckConstraint("CK_unit_allocations_ended_at", "([status] = 'active' AND [ended_at] IS NULL) OR ([status] <> 'active' AND [ended_at] IS NOT NULL)");
        });

        entity.HasIndex(e => new { e.AgreementId, e.AllocationStartDate, e.AllocationEndDate }, "unit_allocations_agreement_period_idx").HasFilter("([agreement_id] IS NOT NULL)");
        entity.HasIndex(e => e.AssignedBy, "unit_allocations_assigned_by_idx").HasFilter("([assigned_by] IS NOT NULL)");
        entity.HasIndex(e => e.ReservationId, "unit_allocations_one_active_reservation_uidx")
            .IsUnique()
            .HasFilter("([status]='active' AND [reservation_id] IS NOT NULL)");
        entity.HasIndex(e => new { e.StorageUnitId, e.AllocationStartDate, e.AllocationEndDate }, "unit_allocations_unit_period_idx");
        entity.HasIndex(e => new { e.StorageUnitId, e.Status, e.AllocationStartDate, e.AllocationEndDate }, "unit_allocations_availability_idx")
            .HasFilter("([status]='active')");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
        entity.Property(e => e.AllocationEndDate).HasColumnName("allocation_end_date");
        entity.Property(e => e.AllocationKind).HasMaxLength(255).HasColumnName("allocation_kind");
        entity.Property(e => e.AllocationStartDate).HasColumnName("allocation_start_date");
        entity.Property(e => e.AssignedBy).HasColumnName("assigned_by");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.EndedAt).HasColumnName("ended_at");
        entity.Property(e => e.Reason).HasMaxLength(255).HasColumnName("reason");
        entity.Property(e => e.ReservationId).HasColumnName("reservation_id");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("active").HasColumnName("status");
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
    }
}
