using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Support;

public sealed class TicketChargeProposalConfiguration : IEntityTypeConfiguration<TicketChargeProposal>
{
    public void Configure(EntityTypeBuilder<TicketChargeProposal> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__ticket_c__3213E83F9CB5A852");
        entity.ToTable("ticket_charge_proposals", "core", tb =>
        {
            tb.HasCheckConstraint("CK_ticket_charge_proposals_amount", "[amount] > 0");
            tb.HasCheckConstraint("CK_ticket_charge_proposals_status", EnumDbMapper.ToSqlCheckInClause<ProposalStatus>("status"));
        });

        entity.HasIndex(e => e.ProposedBy, "ticket_charge_proposals_proposed_by_idx");
        entity.HasIndex(e => new { e.TicketId, e.Status, e.CreatedAt }, "ticket_charge_proposals_ticket_idx").IsDescending(false, false, true);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.Amount).HasColumnType("numeric(14, 2)").HasColumnName("amount");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.Description).HasMaxLength(255).HasColumnName("description");
        entity.Property(e => e.ProposedBy).HasColumnName("proposed_by");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("pending").HasColumnName("status");
        entity.Property(e => e.TicketId).HasColumnName("ticket_id");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("updated_at");

        entity.HasOne(d => d.ProposedByNavigation).WithMany(p => p.TicketChargeProposals)
            .HasForeignKey(d => d.ProposedBy)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__ticket_ch__propo__314D4EA8");

        entity.HasOne(d => d.Ticket).WithMany(p => p.TicketChargeProposals)
            .HasForeignKey(d => d.TicketId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__ticket_ch__ticke__30592A6F");
    }
}
