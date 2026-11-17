using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.CheckIn;

public sealed class InspectionConfiguration : IEntityTypeConfiguration<Inspection>
{
    public void Configure(EntityTypeBuilder<Inspection> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__inspecti__3213E83F24198D74");
        entity.ToTable("inspections", "core", tb =>
        {
            tb.HasTrigger("trg_inspections_validate_scope");
            tb.HasCheckConstraint("CK_inspections_type", EnumDbMapper.ToSqlCheckInClause<InspectionType>("inspection_type"));
            tb.HasCheckConstraint("CK_inspections_status", EnumDbMapper.ToSqlCheckInClause<InspectionStatus>("status"));
            tb.HasCheckConstraint("CK_inspections_overall_condition", $"[overall_condition] IS NULL OR ({EnumDbMapper.ToSqlCheckInClause<OverallCondition>("overall_condition")})");
            tb.HasCheckConstraint("CK_inspections_unit_required", "[inspection_type] NOT IN ('check_in', 'check_out', 'transfer') OR [storage_unit_id] IS NOT NULL");
            tb.HasCheckConstraint("CK_inspections_inspected_at", "([status] = 'draft' AND [inspected_at] IS NULL) OR ([status] <> 'draft' AND [inspected_at] IS NOT NULL)");
        });

        entity.HasIndex(e => e.AgreementId, "inspections_agreement_id_idx").HasFilter("([agreement_id] IS NOT NULL)");
        entity.HasIndex(e => new { e.FacilityId, e.InspectionType, e.CreatedAt }, "inspections_facility_type_date_idx").IsDescending(false, false, true);
        entity.HasIndex(e => e.InspectedBy, "inspections_inspected_by_idx");
        entity.HasIndex(e => e.ReservationId, "inspections_reservation_id_idx").HasFilter("([reservation_id] IS NOT NULL)");
        entity.HasIndex(e => e.StorageUnitId, "inspections_storage_unit_id_idx").HasFilter("([storage_unit_id] IS NOT NULL)");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.FacilityId).HasColumnName("facility_id");
        entity.Property(e => e.InspectedAt).HasColumnName("inspected_at");
        entity.Property(e => e.InspectedBy).HasColumnName("inspected_by");
        entity.Property(e => e.InspectionType).HasMaxLength(255).HasColumnName("inspection_type");
        entity.Property(e => e.OverallCondition).HasMaxLength(255).HasColumnName("overall_condition");
        entity.Property(e => e.ReservationId).HasColumnName("reservation_id");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("draft").HasColumnName("status");
        entity.Property(e => e.StorageUnitId).HasColumnName("storage_unit_id");
        entity.Property(e => e.Summary).HasMaxLength(255).HasColumnName("summary");

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
    }
}
