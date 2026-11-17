using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Support;

public sealed class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__support___3213E83F1DA69E75");
        entity.ToTable("support_tickets", "core", tb =>
        {
            tb.HasTrigger("trg_support_tickets_validate_scope");
            tb.HasCheckConstraint("CK_support_tickets_category", EnumDbMapper.ToSqlCheckInClause<TicketCategory>("category"));
            tb.HasCheckConstraint("CK_support_tickets_priority", EnumDbMapper.ToSqlCheckInClause<TicketPriority>("priority"));
            tb.HasCheckConstraint("CK_support_tickets_status", EnumDbMapper.ToSqlCheckInClause<TicketStatus>("status"));
            tb.HasCheckConstraint("CK_support_tickets_subject_non_empty", "LTRIM(RTRIM([subject])) <> ''");
            tb.HasCheckConstraint("CK_support_tickets_description_non_empty", "LTRIM(RTRIM([description])) <> ''");
            tb.HasCheckConstraint("CK_support_tickets_resolved_consistency", "([status] IN ('resolved', 'closed') AND [resolved_at] IS NOT NULL AND [resolution] IS NOT NULL) OR [status] NOT IN ('resolved', 'closed')");
        });

        entity.HasIndex(e => e.TicketNo, "UQ__support___D596C19626217F01").IsUnique();
        entity.HasIndex(e => e.AgreementId, "support_tickets_agreement_id_idx").HasFilter("([agreement_id] IS NOT NULL)");
        entity.HasIndex(e => new { e.CustomerId, e.CreatedAt }, "support_tickets_customer_created_idx").IsDescending(false, true);
        entity.HasIndex(e => new { e.FacilityId, e.Status, e.Priority, e.CreatedAt }, "support_tickets_facility_queue_idx");
        entity.HasIndex(e => e.StorageUnitId, "support_tickets_storage_unit_id_idx").HasFilter("([storage_unit_id] IS NOT NULL)");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
        entity.Property(e => e.Category).HasMaxLength(255).HasColumnName("category");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.CustomerId).HasColumnName("customer_id");
        entity.Property(e => e.Description).HasMaxLength(255).HasColumnName("description");
        entity.Property(e => e.FacilityId).HasColumnName("facility_id");
        entity.Property(e => e.Priority).HasMaxLength(255).HasDefaultValue("normal").HasColumnName("priority");
        entity.Property(e => e.Resolution).HasMaxLength(255).HasColumnName("resolution");
        entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("open").HasColumnName("status");
        entity.Property(e => e.StorageUnitId).HasColumnName("storage_unit_id");
        entity.Property(e => e.Subject).HasMaxLength(255).HasColumnName("subject");
        entity.Property(e => e.TicketNo).HasMaxLength(255).HasColumnName("ticket_no");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("updated_at");

        entity.HasOne(d => d.Agreement).WithMany(p => p.SupportTicketAgreements)
            .HasForeignKey(d => d.AgreementId)
            .HasConstraintName("FK__support_t__agree__084B3915");

        entity.HasOne(d => d.Customer).WithMany(p => p.SupportTickets)
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__support_t__custo__0662F0A3");

        entity.HasOne(d => d.Facility).WithMany(p => p.SupportTickets)
            .HasForeignKey(d => d.FacilityId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__support_t__facil__075714DC");

        entity.HasOne(d => d.StorageUnit).WithMany(p => p.SupportTicketStorageUnits)
            .HasForeignKey(d => d.StorageUnitId)
            .HasConstraintName("FK__support_t__stora__093F5D4E");
    }
}
