using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Support;

public sealed class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    public void Configure(EntityTypeBuilder<TicketMessage> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__ticket_m__3213E83F379E3A8F");
        entity.ToTable("ticket_messages", "core", tb =>
            tb.HasCheckConstraint("CK_ticket_messages_body_non_empty", "LTRIM(RTRIM([body])) <> ''"));

        entity.HasIndex(e => new { e.Id, e.TicketId }, "UQ__ticket_m__9F4A87A8C907397C").IsUnique();
        entity.HasIndex(e => e.AuthorUserId, "ticket_messages_author_user_id_idx").HasFilter("([author_user_id] IS NOT NULL)");
        entity.HasIndex(e => new { e.TicketId, e.CreatedAt }, "ticket_messages_ticket_created_idx");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AuthorUserId).HasColumnName("author_user_id");
        entity.Property(e => e.Body).HasMaxLength(255).HasColumnName("body");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.IsInternal).HasColumnName("is_internal");
        entity.Property(e => e.TicketId).HasColumnName("ticket_id");

        entity.HasOne(d => d.AuthorUser).WithMany(p => p.TicketMessages)
            .HasForeignKey(d => d.AuthorUserId)
            .HasConstraintName("FK__ticket_me__autho__2022C2A6");

        entity.HasOne(d => d.Ticket).WithMany(p => p.TicketMessages)
            .HasForeignKey(d => d.TicketId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__ticket_me__ticke__1F2E9E6D");
    }
}
