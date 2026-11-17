using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Facility;

public sealed class StorageUnitConfiguration : IEntityTypeConfiguration<StorageUnit>
{
    public void Configure(EntityTypeBuilder<StorageUnit> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__storage___3213E83F401545AC");
        entity.ToTable("storage_units", "core", tb =>
        {
            tb.HasTrigger("trg_storage_unit_status_transition");
            tb.HasTrigger("trg_storage_units_validate_area");
            tb.HasCheckConstraint("CK_storage_units_physical_status", EnumDbMapper.ToSqlCheckInClause<UnitStatus>("physical_status"));
        });

        entity.HasIndex(e => new { e.Id, e.FacilityId, e.UnitTypeId }, "UQ__storage___01486F6FDC4326A5").IsUnique();
        entity.HasIndex(e => new { e.FacilityId, e.UnitCode }, "UQ__storage___17930DB60694841D").IsUnique();
        entity.HasIndex(e => new { e.Id, e.FacilityId }, "UQ__storage___C93D6694EC620F41").IsUnique();
        entity.HasIndex(e => e.AreaId, "storage_units_area_id_idx").HasFilter("([area_id] IS NOT NULL)");
        entity.HasIndex(e => new { e.FacilityId, e.UnitTypeId, e.PhysicalStatus }, "storage_units_catalog_idx").HasFilter("([is_listed]=(1))");
        entity.HasIndex(e => e.UnitTypeId, "storage_units_unit_type_id_idx");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AreaId).HasColumnName("area_id");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.FacilityId).HasColumnName("facility_id");
        entity.Property(e => e.FloorLabel).HasMaxLength(255).HasColumnName("floor_label");
        entity.Property(e => e.IsListed).HasDefaultValue(true).HasColumnName("is_listed");
        entity.Property(e => e.Notes).HasMaxLength(255).HasColumnName("notes");
        entity.Property(e => e.PhysicalStatus)
            .HasMaxLength(255)
            .HasDefaultValue("available")
            .HasColumnName("physical_status")
            .IsConcurrencyToken();
        entity.Property(e => e.UnitCode).HasMaxLength(255).HasColumnName("unit_code");
        entity.Property(e => e.UnitTypeId).HasColumnName("unit_type_id");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("updated_at");
        entity.Property(e => e.ZoneLabel).HasMaxLength(255).HasColumnName("zone_label");

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
    }
}
