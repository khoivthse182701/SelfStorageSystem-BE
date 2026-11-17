using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Support;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__audit_lo__3213E83FDE7CB008");
        entity.ToTable("audit_logs", "core");

        entity.HasIndex(e => new { e.ActorUserId, e.OccurredAt }, "audit_logs_actor_idx")
            .IsDescending(false, true)
            .HasFilter("([actor_user_id] IS NOT NULL)");
        entity.HasIndex(e => new { e.EntityType, e.EntityId, e.OccurredAt }, "audit_logs_entity_idx").IsDescending(false, false, true);
        entity.HasIndex(e => e.RequestId, "audit_logs_request_id_idx").HasFilter("([request_id] IS NOT NULL)");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.Action).HasMaxLength(255).HasColumnName("action");
        entity.Property(e => e.ActorRole).HasMaxLength(255).HasColumnName("actor_role");
        entity.Property(e => e.ActorUserId).HasColumnName("actor_user_id");
        entity.Property(e => e.EntityId).HasMaxLength(255).HasColumnName("entity_id");
        entity.Property(e => e.EntitySchema).HasMaxLength(255).HasDefaultValue("core").HasColumnName("entity_schema");
        entity.Property(e => e.EntityType).HasMaxLength(255).HasColumnName("entity_type");
        entity.Property(e => e.IpAddress).HasMaxLength(45).HasColumnName("ip_address");
        entity.Property(e => e.NewValues).HasColumnName("new_values");
        entity.Property(e => e.OccurredAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("occurred_at");
        entity.Property(e => e.OldValues).HasColumnName("old_values");
        entity.Property(e => e.RequestId).HasMaxLength(255).HasColumnName("request_id");

        entity.HasOne(d => d.ActorUser).WithMany(p => p.AuditLogs)
            .HasForeignKey(d => d.ActorUserId)
            .HasConstraintName("FK__audit_log__actor__74CE504D");
    }
}
