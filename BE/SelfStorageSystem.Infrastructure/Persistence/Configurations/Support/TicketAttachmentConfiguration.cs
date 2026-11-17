using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Support;

public sealed class TicketAttachmentConfiguration : IEntityTypeConfiguration<TicketAttachment>
{
    public void Configure(EntityTypeBuilder<TicketAttachment> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__ticket_a__3213E83FEB7ACA3B");
        entity.ToTable("ticket_attachments", "core", tb =>
        {
            tb.HasCheckConstraint("CK_ticket_attachments_size", "[file_size_bytes] > 0");
            tb.HasCheckConstraint("CK_ticket_attachments_non_empty", "LTRIM(RTRIM([file_name])) <> '' AND LTRIM(RTRIM([mime_type])) <> '' AND LTRIM(RTRIM([object_url])) <> ''");
        });

        entity.HasIndex(e => e.MessageId, "ticket_attachments_message_id_idx").HasFilter("([message_id] IS NOT NULL)");
        entity.HasIndex(e => new { e.TicketId, e.CreatedAt }, "ticket_attachments_ticket_id_idx");
        entity.HasIndex(e => e.UploadedBy, "ticket_attachments_uploaded_by_idx");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.FileName).HasMaxLength(255).HasColumnName("file_name");
        entity.Property(e => e.FileSizeBytes).HasColumnName("file_size_bytes");
        entity.Property(e => e.MessageId).HasColumnName("message_id");
        entity.Property(e => e.MimeType).HasMaxLength(255).HasColumnName("mime_type");
        entity.Property(e => e.ObjectUrl).HasMaxLength(255).HasColumnName("object_url");
        entity.Property(e => e.Sha256).HasMaxLength(255).HasColumnName("sha256");
        entity.Property(e => e.TicketId).HasColumnName("ticket_id");
        entity.Property(e => e.UploadedBy).HasColumnName("uploaded_by");

        entity.HasOne(d => d.Message).WithMany(p => p.TicketAttachmentMessages)
            .HasForeignKey(d => d.MessageId)
            .HasConstraintName("FK__ticket_at__messa__26CFC035");

        entity.HasOne(d => d.Ticket).WithMany(p => p.TicketAttachments)
            .HasForeignKey(d => d.TicketId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__ticket_at__ticke__25DB9BFC");

        entity.HasOne(d => d.UploadedByNavigation).WithMany(p => p.TicketAttachments)
            .HasForeignKey(d => d.UploadedBy)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__ticket_at__uploa__27C3E46E");

        entity.HasOne(d => d.TicketMessage).WithMany(p => p.TicketAttachmentTicketMessages)
            .HasPrincipalKey(p => new { p.Id, p.TicketId })
            .HasForeignKey(d => new { d.MessageId, d.TicketId })
            .HasConstraintName("FK__ticket_attachmen__2AA05119");
    }
}
