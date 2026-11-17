using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Billing;

public sealed class RefundApprovalConfiguration : IEntityTypeConfiguration<RefundApproval>
{
    public void Configure(EntityTypeBuilder<RefundApproval> entity)
    {
        entity.HasKey(e => e.RefundId).HasName("PK__refund_a__897E9EA3E281922A");
        entity.ToTable("refund_approvals", "core", tb =>
            tb.HasCheckConstraint("CK_refund_approvals_decision", EnumDbMapper.ToSqlCheckInClause<ApprovalDecision>("decision")));

        entity.HasIndex(e => e.DecidedBy, "refund_approvals_decided_by_idx");

        entity.Property(e => e.RefundId).ValueGeneratedNever().HasColumnName("refund_id");
        entity.Property(e => e.DecidedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("decided_at");
        entity.Property(e => e.DecidedBy).HasColumnName("decided_by");
        entity.Property(e => e.Decision).HasMaxLength(255).HasColumnName("decision");
        entity.Property(e => e.Reason).HasMaxLength(255).HasColumnName("reason");

        entity.HasOne(d => d.DecidedByNavigation).WithMany(p => p.RefundApprovals)
            .HasForeignKey(d => d.DecidedBy)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__refund_ap__decid__0AF29B96");

        entity.HasOne(d => d.Refund).WithOne(p => p.RefundApproval)
            .HasForeignKey<RefundApproval>(d => d.RefundId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__refund_ap__refun__090A5324");
    }
}
