using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Billing;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__payments__3213E83F1EE84ED7");
        entity.ToTable("payments", "core", tb =>
        {
            tb.HasTrigger("trg_payments_refresh_invoices");
            tb.HasTrigger("trg_payments_validate_target");
            tb.HasCheckConstraint("CK_payments_amount", "[amount] > 0");
            tb.HasCheckConstraint("CK_payments_method", EnumDbMapper.ToSqlCheckInClause<PaymentMethod>("method"));
            tb.HasCheckConstraint("CK_payments_status", EnumDbMapper.ToSqlCheckInClause<PaymentStatus>("status"));
            tb.HasCheckConstraint("CK_payments_currency_upper", "[currency] = upper([currency])");
            tb.HasCheckConstraint("CK_payments_metadata_json", "ISJSON([metadata]) = 1");
            tb.HasCheckConstraint("CK_payments_paid_at", "([status] = 'succeeded' AND [paid_at] IS NOT NULL) OR [status] <> 'succeeded'");
        });

        entity.HasIndex(e => e.IdempotencyKey, "UQ__payments__A7BA59F488C60FE3").IsUnique();
        entity.HasIndex(e => new { e.CustomerId, e.CreatedAt }, "payments_customer_created_idx").IsDescending(false, true);
        entity.HasIndex(e => new { e.Provider, e.ProviderTransactionId }, "payments_provider_transaction_uidx")
            .IsUnique()
            .HasFilter("([provider_transaction_id] IS NOT NULL)");
        entity.HasIndex(e => e.TargetInvoiceId, "payments_target_invoice_id_idx");
        entity.HasIndex(e => new { e.CustomerId, e.TargetInvoiceId, e.Status }, "ix_payments_customer_invoice_status");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.Amount).HasColumnType("numeric(14, 2)").HasColumnName("amount");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.Currency).HasMaxLength(3).IsUnicode(false).HasDefaultValue("VND").IsFixedLength().HasColumnName("currency");
        entity.Property(e => e.CustomerId).HasColumnName("customer_id");
        entity.Property(e => e.FailureReason).HasMaxLength(255).HasColumnName("failure_reason");
        entity.Property(e => e.IdempotencyKey).HasMaxLength(255).HasColumnName("idempotency_key");
        entity.Property(e => e.Metadata).HasDefaultValue("{}").HasColumnName("metadata");
        entity.Property(e => e.Method).HasMaxLength(255).HasColumnName("method");
        entity.Property(e => e.PaidAt).HasColumnName("paid_at");
        entity.Property(e => e.Provider).HasMaxLength(255).HasColumnName("provider");
        entity.Property(e => e.ProviderTransactionId).HasMaxLength(255).HasColumnName("provider_transaction_id");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("initiated").HasColumnName("status");
        entity.Property(e => e.TargetInvoiceId).HasColumnName("target_invoice_id");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("updated_at");

        entity.HasOne(d => d.Customer).WithMany(p => p.Payments)
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__payments__custom__67A95F59");

        entity.HasOne(d => d.TargetInvoice).WithMany(p => p.Payments)
            .HasForeignKey(d => d.TargetInvoiceId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__payments__target__689D8392");
    }
}
