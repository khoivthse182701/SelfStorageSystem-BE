using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Facility;

public sealed class UnitTypeConfiguration : IEntityTypeConfiguration<UnitType>
{
    public void Configure(EntityTypeBuilder<UnitType> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__unit_typ__3213E83F18088A55");
        entity.ToTable("unit_types", "core", tb =>
        {
            tb.HasCheckConstraint("CK_unit_types_dimensions", "[width_m] > 0 AND [length_m] > 0 AND [height_m] > 0");
            tb.HasCheckConstraint("CK_unit_types_max_weight", "[max_weight_kg] IS NULL OR [max_weight_kg] > 0");
        });

        entity.HasIndex(e => e.Code, "UQ__unit_typ__357D4CF933DE2D48").IsUnique();

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AreaM2)
            .HasComputedColumnSql("(CONVERT([numeric](10,2),[width_m]*[length_m]))", true)
            .HasColumnType("numeric(10, 2)")
            .HasColumnName("area_m2");
        entity.Property(e => e.ClimateControlled).HasColumnName("climate_controlled");
        entity.Property(e => e.Code).HasMaxLength(255).HasColumnName("code");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.Description).HasMaxLength(255).HasColumnName("description");
        entity.Property(e => e.HeightM).HasColumnType("numeric(8, 2)").HasColumnName("height_m");
        entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
        entity.Property(e => e.LengthM).HasColumnType("numeric(8, 2)").HasColumnName("length_m");
        entity.Property(e => e.MaxWeightKg).HasColumnType("numeric(12, 2)").HasColumnName("max_weight_kg");
        entity.Property(e => e.Name).HasMaxLength(255).HasColumnName("name");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("updated_at");
        entity.Property(e => e.VolumeM3)
            .HasComputedColumnSql("(CONVERT([numeric](12,2),([width_m]*[length_m])*[height_m]))", true)
            .HasColumnType("numeric(12, 2)")
            .HasColumnName("volume_m3");
        entity.Property(e => e.WidthM).HasColumnType("numeric(8, 2)").HasColumnName("width_m");
    }
}
