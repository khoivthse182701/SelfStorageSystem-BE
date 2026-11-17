using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.CheckIn;

public sealed class HandoverRecordConfiguration : IEntityTypeConfiguration<HandoverRecord>
{
    public void Configure(EntityTypeBuilder<HandoverRecord> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__handover__3213E83F4B215F3D");
        entity.ToTable("handover_records", "core", tb =>
        {
            tb.HasTrigger("trg_handover_records_validate_scope");
            tb.HasCheckConstraint("CK_handover_records_type", EnumDbMapper.ToSqlCheckInClause<HandoverType>("handover_type"));
        });

        entity.HasIndex(e => new { e.AgreementId, e.CreatedAt }, "handover_records_agreement_idx").IsDescending(false, true);
        entity.HasIndex(e => e.HandledBy, "handover_records_handled_by_idx");
        entity.HasIndex(e => e.InspectionId, "handover_records_inspection_id_idx");
        entity.HasIndex(e => e.UnitAllocationId, "handover_records_unit_allocation_id_idx");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.CustomerSignatureRef).HasMaxLength(255).HasColumnName("customer_signature_ref");
        entity.Property(e => e.CustomerSignedAt).HasColumnName("customer_signed_at");
        entity.Property(e => e.HandledBy).HasColumnName("handled_by");
        entity.Property(e => e.HandoverType).HasMaxLength(255).HasColumnName("handover_type");
        entity.Property(e => e.InspectionId).HasColumnName("inspection_id");
        entity.Property(e => e.Notes).HasMaxLength(255).HasColumnName("notes");
        entity.Property(e => e.StaffSignatureRef).HasMaxLength(255).HasColumnName("staff_signature_ref");
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
    }
}
