using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Identity;

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> entity)
    {
        entity.HasKey(e => new { e.UserId, e.RoleId }).HasName("PK__user_rol__6EDEA1530C21B709");
        entity.ToTable("user_roles", "core");

        entity.HasIndex(e => e.GrantedBy, "user_roles_granted_by_idx").HasFilter("([granted_by] IS NOT NULL)");
        entity.HasIndex(e => new { e.RoleId, e.UserId }, "user_roles_role_id_idx");

        entity.Property(e => e.UserId).HasColumnName("user_id");
        entity.Property(e => e.RoleId).HasColumnName("role_id");
        entity.Property(e => e.GrantedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("granted_at");
        entity.Property(e => e.GrantedBy).HasColumnName("granted_by");

        entity.HasOne(d => d.GrantedByNavigation).WithMany(p => p.UserRoleGrantedByNavigations)
            .HasForeignKey(d => d.GrantedBy)
            .HasConstraintName("FK__user_role__grant__5CD6CB2B");

        entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
            .HasForeignKey(d => d.RoleId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__user_role__role___5BE2A6F2");

        entity.HasOne(d => d.User).WithMany(p => p.UserRoleUsers)
            .HasForeignKey(d => d.UserId)
            .HasConstraintName("FK__user_role__user___5AEE82B9");
    }
}
