using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Facility;

public sealed class FacilityConfiguration : IEntityTypeConfiguration<Domain.Entities.Facility>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Facility> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__faciliti__3213E83FE0B4338E");
        entity.ToTable("facilities", "core", tb =>
        {
            tb.HasCheckConstraint("CK_facilities_status", EnumDbMapper.ToSqlCheckInClause<FacilityStatus>("status"));
            tb.HasCheckConstraint("CK_facilities_latitude", "[latitude] IS NULL OR ([latitude] >= -90 AND [latitude] <= 90)");
            tb.HasCheckConstraint("CK_facilities_longitude", "[longitude] IS NULL OR ([longitude] >= -180 AND [longitude] <= 180)");
            tb.HasCheckConstraint("CK_facilities_hours", "[closing_time] IS NULL OR [opening_time] IS NULL OR [closing_time] > [opening_time]");
        });

        entity.HasIndex(e => e.Code, "UQ__faciliti__357D4CF9081C918B").IsUnique();
        entity.HasIndex(e => new { e.City, e.Status }, "facilities_city_status_idx");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AddressLine).HasMaxLength(255).HasColumnName("address_line");
        entity.Property(e => e.City).HasMaxLength(255).HasColumnName("city");
        entity.Property(e => e.ClosingTime).HasColumnName("closing_time");
        entity.Property(e => e.Code).HasMaxLength(255).HasColumnName("code");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.District).HasMaxLength(255).HasColumnName("district");
        entity.Property(e => e.Latitude).HasColumnType("numeric(9, 6)").HasColumnName("latitude");
        entity.Property(e => e.Longitude).HasColumnType("numeric(9, 6)").HasColumnName("longitude");
        entity.Property(e => e.Name).HasMaxLength(255).HasColumnName("name");
        entity.Property(e => e.OpeningTime).HasColumnName("opening_time");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("active").HasColumnName("status");
        entity.Property(e => e.Timezone).HasMaxLength(255).HasDefaultValue("Asia/Ho_Chi_Minh").HasColumnName("timezone");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("updated_at");
        entity.Property(e => e.Ward).HasMaxLength(255).HasColumnName("ward");
    }
}
