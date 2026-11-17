using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Facility;

public sealed class UnitMapPositionConfiguration : IEntityTypeConfiguration<UnitMapPosition>
{
    public void Configure(EntityTypeBuilder<UnitMapPosition> entity)
    {
        entity.HasKey(e => e.UnitId).HasName("PK__unit_map__D3AF5BD7302169A8");
        entity.ToTable("unit_map_positions", "core", tb =>
        {
            tb.HasTrigger("trg_unit_map_positions_validate_scope");
            tb.HasCheckConstraint("CK_unit_map_positions_dimensions", "[width] > 0 AND [height] > 0");
            tb.HasCheckConstraint("CK_unit_map_positions_metadata_json", "ISJSON([metadata]) = 1");
        });

        entity.HasIndex(e => e.AreaId, "unit_map_positions_area_id_idx");

        entity.Property(e => e.UnitId).ValueGeneratedNever().HasColumnName("unit_id");
        entity.Property(e => e.AreaId).HasColumnName("area_id");
        entity.Property(e => e.Height).HasColumnType("numeric(10, 2)").HasColumnName("height");
        entity.Property(e => e.Metadata).HasDefaultValue("{}").HasColumnName("metadata");
        entity.Property(e => e.RotationDegrees).HasColumnType("numeric(6, 2)").HasColumnName("rotation_degrees");
        entity.Property(e => e.Width).HasColumnType("numeric(10, 2)").HasColumnName("width");
        entity.Property(e => e.X).HasColumnType("numeric(10, 2)").HasColumnName("x");
        entity.Property(e => e.Y).HasColumnType("numeric(10, 2)").HasColumnName("y");

        entity.HasOne(d => d.Area).WithMany(p => p.UnitMapPositions)
            .HasForeignKey(d => d.AreaId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__unit_map___area___236943A5");

        entity.HasOne(d => d.Unit).WithOne(p => p.UnitMapPosition)
            .HasForeignKey<UnitMapPosition>(d => d.UnitId)
            .HasConstraintName("FK__unit_map___unit___22751F6C");
    }
}
