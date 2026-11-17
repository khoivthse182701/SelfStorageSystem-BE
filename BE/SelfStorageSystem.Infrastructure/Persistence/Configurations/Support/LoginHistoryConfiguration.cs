using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Support;

public sealed class LoginHistoryConfiguration : IEntityTypeConfiguration<LoginHistory>
{
    public void Configure(EntityTypeBuilder<LoginHistory> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__login_hi__3213E83F95DB5DA2");
        entity.ToTable("login_history", "core", tb =>
            tb.HasCheckConstraint("CK_login_history_result", EnumDbMapper.ToSqlCheckInClause<LoginHistoryResult>("result")));

        entity.HasIndex(e => new { e.AttemptedEmail, e.OccurredAt }, "login_history_email_occurred_idx")
            .IsDescending(false, true)
            .HasFilter("([attempted_email] IS NOT NULL)");
        entity.HasIndex(e => new { e.UserId, e.OccurredAt }, "login_history_user_occurred_idx")
            .IsDescending(false, true)
            .HasFilter("([user_id] IS NOT NULL)");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AttemptedEmail).HasMaxLength(255).HasColumnName("attempted_email");
        entity.Property(e => e.IpAddress).HasMaxLength(45).HasColumnName("ip_address");
        entity.Property(e => e.OccurredAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("occurred_at");
        entity.Property(e => e.Result).HasMaxLength(255).HasColumnName("result");
        entity.Property(e => e.UserAgent).HasMaxLength(255).HasColumnName("user_agent");
        entity.Property(e => e.UserId).HasColumnName("user_id");

        entity.HasOne(d => d.User).WithMany(p => p.LoginHistories)
            .HasForeignKey(d => d.UserId)
            .HasConstraintName("FK__login_his__user___7993056A");
    }
}
