using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Operations;

public sealed class AccessPointConfiguration : IEntityTypeConfiguration<AccessPoint>
{
    public void Configure(EntityTypeBuilder<AccessPoint> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__access_p__3213E83FFEB8BC50");
        entity.ToTable("access_points", "core", tb =>
        {
            tb.HasCheckConstraint("CK_access_points_type", EnumDbMapper.ToSqlCheckInClause<AccessPointType>("access_point_type"));
            tb.HasCheckConstraint("CK_access_points_status", EnumDbMapper.ToSqlCheckInClause<AccessPointStatus>("status"));
        });

        entity.HasIndex(e => new { e.FacilityId, e.Code }, "UQ__access_p__21BF3E60701E44D7").IsUnique();
        entity.HasIndex(e => new { e.FacilityId, e.Status }, "access_points_facility_id_idx");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AccessPointType).HasMaxLength(255).HasColumnName("access_point_type");
        entity.Property(e => e.Code).HasMaxLength(255).HasColumnName("code");
        entity.Property(e => e.ExternalDeviceCode).HasMaxLength(255).HasColumnName("external_device_code");
        entity.Property(e => e.FacilityId).HasColumnName("facility_id");
        entity.Property(e => e.Name).HasMaxLength(255).HasColumnName("name");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("active").HasColumnName("status");

        entity.HasOne(d => d.Facility).WithMany(p => p.AccessPoints)
            .HasForeignKey(d => d.FacilityId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__access_po__facil__3E723F9C");
    }
}
