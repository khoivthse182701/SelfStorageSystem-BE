using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Identity;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__users__3213E83F69F02F0F");
        entity.ToTable("users", "core", tb =>
        {
            tb.HasCheckConstraint("CK_users_status", EnumDbMapper.ToSqlCheckInClause<UserStatus>("status"));
            tb.HasCheckConstraint("users_email_normalized", "[email] = lower(LTRIM(RTRIM([email])))");
        });

        entity.HasIndex(e => e.Email, "users_email_unique").IsUnique();

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.Email).HasMaxLength(255).HasColumnName("email");
        entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
        entity.Property(e => e.PasswordHash).HasMaxLength(255).HasColumnName("password_hash");
        entity.Property(e => e.PhoneNumber).HasMaxLength(255).HasColumnName("phone_number");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("active").HasColumnName("status");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("updated_at");
    }
}
