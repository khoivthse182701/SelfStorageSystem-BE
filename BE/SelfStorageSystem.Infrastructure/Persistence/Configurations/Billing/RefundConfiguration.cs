using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Billing;

public sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__refunds__3213E83F6C90B5ED");
        entity.ToTable("refunds", "core", tb =>
        {
            tb.HasTrigger("trg_refunds_guard");
            tb.HasCheckConstraint("CK_refunds_amount", "[amount] > 0");
            tb.HasCheckConstraint("CK_refunds_status", EnumDbMapper.ToSqlCheckInClause<RefundStatus>("status"));
            tb.HasCheckConstraint("CK_refunds_currency_upper", "[currency] = upper([currency])");
            tb.HasCheckConstraint("CK_refunds_refunded_at", "([status] IN ('succeeded', 'completed') AND [refunded_at] IS NOT NULL) OR [status] NOT IN ('succeeded', 'completed')");
        });

        entity.HasIndex(e => e.IdempotencyKey, "UQ__refunds__A7BA59F4B0FC8B69").IsUnique();
        entity.HasIndex(e => e.AgreementId, "refunds_agreement_id_idx").HasFilter("([agreement_id] IS NOT NULL)");
        entity.HasIndex(e => e.PaymentId, "refunds_payment_id_idx");
        entity.HasIndex(e => new { e.Provider, e.ProviderRefundId }, "refunds_provider_refund_uidx")
            .IsUnique()
            .HasFilter("([provider_refund_id] IS NOT NULL)");
        entity.HasIndex(e => e.RequestedBy, "refunds_requested_by_idx");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
        entity.Property(e => e.Amount).HasColumnType("numeric(14, 2)").HasColumnName("amount");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.Currency).HasMaxLength(3).IsUnicode(false).HasDefaultValue("VND").IsFixedLength().HasColumnName("currency");
        entity.Property(e => e.IdempotencyKey).HasMaxLength(255).HasColumnName("idempotency_key");
        entity.Property(e => e.PaymentId).HasColumnName("payment_id");
        entity.Property(e => e.Provider).HasMaxLength(255).HasColumnName("provider");
        entity.Property(e => e.ProviderRefundId).HasMaxLength(255).HasColumnName("provider_refund_id");
        entity.Property(e => e.Reason).HasMaxLength(255).HasColumnName("reason");
        entity.Property(e => e.RefundedAt).HasColumnName("refunded_at");
        entity.Property(e => e.RequestedBy).HasColumnName("requested_by");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("requested").HasColumnName("status");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("updated_at");

        entity.HasOne(d => d.Agreement).WithMany(p => p.Refunds)
            .HasForeignKey(d => d.AgreementId)
            .HasConstraintName("FK__refunds__agreeme__7D98A078");

        entity.HasOne(d => d.Payment).WithMany(p => p.Refunds)
            .HasForeignKey(d => d.PaymentId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__refunds__payment__7CA47C3F");

        entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.Refunds)
            .HasForeignKey(d => d.RequestedBy)
            .HasConstraintName("FK__refunds__request__025D5595");
    }
}
