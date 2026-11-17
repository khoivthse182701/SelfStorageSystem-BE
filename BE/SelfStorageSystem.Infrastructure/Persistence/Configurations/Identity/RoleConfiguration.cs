using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Identity;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__roles__3213E83F47C336B5");
        entity.ToTable("roles", "core", tb =>
            tb.HasCheckConstraint("CK_roles_code", "[code] IN ('storage_customer', 'facility_staff', 'facility_manager', 'system_administrator')"));

        entity.HasIndex(e => e.Code, "UQ__roles__357D4CF96675988C").IsUnique();

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.Code).HasMaxLength(255).HasColumnName("code");
        entity.Property(e => e.Description).HasMaxLength(255).HasColumnName("description");
        entity.Property(e => e.DisplayName).HasMaxLength(255).HasColumnName("display_name");
    }
}
