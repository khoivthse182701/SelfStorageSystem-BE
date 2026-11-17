using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Support;

public sealed class IntegrationEventConfiguration : IEntityTypeConfiguration<IntegrationEvent>
{
    public void Configure(EntityTypeBuilder<IntegrationEvent> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__integrat__3213E83FAE9A93FF");
        entity.ToTable("integration_events", "core", tb =>
        {
            tb.HasCheckConstraint("CK_integration_events_status", EnumDbMapper.ToSqlCheckInClause<IntegrationEventStatus>("status"));
            tb.HasCheckConstraint("CK_integration_events_payload_json", "ISJSON([payload]) = 1");
        });

        entity.HasIndex(e => new { e.Source, e.ExternalEventId }, "UQ__integrat__86EB106AF963411C").IsUnique();
        entity.HasIndex(e => new { e.ReceivedAt, e.Id }, "integration_events_processing_queue_idx").HasFilter("([status] IN ('received', 'failed'))");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.ErrorMessage).HasMaxLength(255).HasColumnName("error_message");
        entity.Property(e => e.EventType).HasMaxLength(255).HasColumnName("event_type");
        entity.Property(e => e.ExternalEventId).HasMaxLength(255).HasColumnName("external_event_id");
        entity.Property(e => e.Payload).HasColumnName("payload");
        entity.Property(e => e.ProcessedAt).HasColumnName("processed_at");
        entity.Property(e => e.ReceivedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("received_at");
        entity.Property(e => e.Source).HasMaxLength(255).HasColumnName("source");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("received").HasColumnName("status");
    }
}
