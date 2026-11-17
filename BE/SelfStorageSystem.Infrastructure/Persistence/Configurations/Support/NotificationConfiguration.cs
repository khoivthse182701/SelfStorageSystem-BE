using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Support;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__notifica__3213E83F5750E434");
        entity.ToTable("notifications", "core", tb =>
        {
            tb.HasCheckConstraint("CK_notifications_channel", EnumDbMapper.ToSqlCheckInClause<NotificationChannel>("channel"));
            tb.HasCheckConstraint("CK_notifications_status", EnumDbMapper.ToSqlCheckInClause<NotificationStatus>("status"));
            tb.HasCheckConstraint("CK_notifications_payload_json", "ISJSON([payload]) = 1");
            tb.HasCheckConstraint("CK_notifications_attempts", "[attempts] >= 0");
        });

        entity.HasIndex(e => e.DeduplicationKey, "notifications_deduplication_key_uidx")
            .IsUnique()
            .HasFilter("([deduplication_key] IS NOT NULL)");
        entity.HasIndex(e => new { e.ScheduledAt, e.Id }, "notifications_delivery_queue_idx").HasFilter("([status] IN ('pending', 'failed'))");
        entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "notifications_user_created_idx").IsDescending(false, true);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.Attempts).HasColumnName("attempts");
        entity.Property(e => e.Channel).HasMaxLength(255).HasColumnName("channel");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.DeduplicationKey).HasMaxLength(255).HasColumnName("deduplication_key");
        entity.Property(e => e.LastError).HasMaxLength(255).HasColumnName("last_error");
        entity.Property(e => e.Payload).HasDefaultValue("{}").HasColumnName("payload");
        entity.Property(e => e.ScheduledAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("scheduled_at");
        entity.Property(e => e.SentAt).HasColumnName("sent_at");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("pending").HasColumnName("status");
        entity.Property(e => e.Subject).HasMaxLength(255).HasColumnName("subject");
        entity.Property(e => e.TemplateCode).HasMaxLength(255).HasColumnName("template_code");
        entity.Property(e => e.UserId).HasColumnName("user_id");

        entity.HasOne(d => d.User).WithMany(p => p.Notifications)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__notificat__user___695C9DA1");
    }
}
