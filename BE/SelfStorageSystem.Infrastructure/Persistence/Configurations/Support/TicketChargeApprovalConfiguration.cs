using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Support;

public sealed class TicketChargeApprovalConfiguration : IEntityTypeConfiguration<TicketChargeApproval>
{
    public void Configure(EntityTypeBuilder<TicketChargeApproval> entity)
    {
        entity.HasKey(e => e.ProposalId).HasName("PK__ticket_c__A7BC641C435D978F");
        entity.ToTable("ticket_charge_approvals", "core", tb =>
            tb.HasCheckConstraint("CK_ticket_charge_approvals_decision", EnumDbMapper.ToSqlCheckInClause<ApprovalDecision>("decision")));

        entity.HasIndex(e => e.DecidedBy, "ticket_charge_approvals_decided_by_idx");

        entity.Property(e => e.ProposalId).ValueGeneratedNever().HasColumnName("proposal_id");
        entity.Property(e => e.DecidedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("decided_at");
        entity.Property(e => e.DecidedBy).HasColumnName("decided_by");
        entity.Property(e => e.Decision).HasMaxLength(255).HasColumnName("decision");
        entity.Property(e => e.Reason).HasMaxLength(255).HasColumnName("reason");

        entity.HasOne(d => d.DecidedByNavigation).WithMany(p => p.TicketChargeApprovals)
            .HasForeignKey(d => d.DecidedBy)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__ticket_ch__decid__3AD6B8E2");

        entity.HasOne(d => d.Proposal).WithOne(p => p.TicketChargeApproval)
            .HasForeignKey<TicketChargeApproval>(d => d.ProposalId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__ticket_ch__propo__38EE7070");
    }
}
