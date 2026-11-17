using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Facility;

public sealed class FacilityAreaConfiguration : IEntityTypeConfiguration<FacilityArea>
{
    public void Configure(EntityTypeBuilder<FacilityArea> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__facility__3213E83FEE469076");
        entity.ToTable("facility_areas", "core", tb =>
        {
            tb.HasCheckConstraint("CK_facility_areas_area_type", EnumDbMapper.ToSqlCheckInClause<AreaType>("area_type"));
            tb.HasCheckConstraint("CK_facility_areas_map_metadata_json", "ISJSON([map_metadata]) = 1");
            tb.HasCheckConstraint("CK_facility_areas_no_self_parent", "[parent_area_id] IS NULL OR [parent_area_id] <> [id]");
        });

        entity.HasIndex(e => new { e.FacilityId, e.Code }, "UQ__facility__21BF3E60BE1B2B38").IsUnique();
        entity.HasIndex(e => new { e.Id, e.FacilityId }, "UQ__facility__C93D6694F87C9E30").IsUnique();
        entity.HasIndex(e => e.ParentAreaId, "facility_areas_parent_area_id_idx").HasFilter("([parent_area_id] IS NOT NULL)");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AreaType).HasMaxLength(255).HasColumnName("area_type");
        entity.Property(e => e.Code).HasMaxLength(255).HasColumnName("code");
        entity.Property(e => e.DisplayOrder).HasColumnName("display_order");
        entity.Property(e => e.FacilityId).HasColumnName("facility_id");
        entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
        entity.Property(e => e.MapMetadata).HasDefaultValue("{}").HasColumnName("map_metadata");
        entity.Property(e => e.Name).HasMaxLength(255).HasColumnName("name");
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
    }
}
