using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Operations;

public sealed class AccessEventConfiguration : IEntityTypeConfiguration<AccessEvent>
{
    public void Configure(EntityTypeBuilder<AccessEvent> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__access_e__3213E83FC1162516");
        entity.ToTable("access_events", "core", tb =>
        {
            tb.HasCheckConstraint("CK_access_events_result", EnumDbMapper.ToSqlCheckInClause<AccessEventResult>("result"));
            tb.HasCheckConstraint("CK_access_events_metadata_json", "ISJSON([metadata]) = 1");
        });

        entity.HasIndex(e => e.ExternalEventId, "access_events_external_event_id_uidx")
            .IsUnique()
            .HasFilter("([external_event_id] IS NOT NULL)");
        entity.HasIndex(e => new { e.CredentialId, e.OccurredAt }, "access_events_credential_occurred_idx")
            .IsDescending(false, true)
            .HasFilter("([credential_id] IS NOT NULL)");
        entity.HasIndex(e => new { e.AccessPointId, e.OccurredAt }, "access_events_point_occurred_idx").IsDescending(false, true);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AccessPointId).HasColumnName("access_point_id");
        entity.Property(e => e.CredentialId).HasColumnName("credential_id");
        entity.Property(e => e.ExternalEventId).HasMaxLength(255).HasColumnName("external_event_id");
        entity.Property(e => e.Metadata).HasDefaultValue("{}").HasColumnName("metadata");
        entity.Property(e => e.OccurredAt).HasColumnName("occurred_at");
        entity.Property(e => e.Reason).HasMaxLength(255).HasColumnName("reason");
        entity.Property(e => e.Result).HasMaxLength(255).HasColumnName("result");

        entity.HasOne(d => d.AccessPoint).WithMany(p => p.AccessEvents)
            .HasForeignKey(d => d.AccessPointId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__access_ev__acces__51851410");

        entity.HasOne(d => d.Credential).WithMany(p => p.AccessEvents)
            .HasForeignKey(d => d.CredentialId)
            .HasConstraintName("FK__access_ev__crede__5090EFD7");
    }
}
