using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Billing;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__invoices__3213E83F6609AFEF");
        entity.ToTable("invoices", "core", tb =>
        {
            tb.HasTrigger("trg_invoice_status_transition");
            tb.HasTrigger("trg_invoices_validate_scope");
            tb.HasCheckConstraint("CK_invoices_status", EnumDbMapper.ToSqlCheckInClause<InvoiceStatus>("status"));
            tb.HasCheckConstraint("CK_invoices_amounts", "[subtotal_amount] >= 0 AND [discount_amount] >= 0 AND [tax_amount] >= 0 AND [total_amount] >= 0 AND [paid_amount] >= 0 AND [paid_amount] <= [total_amount]");
            tb.HasCheckConstraint("CK_invoices_due_date", "[due_date] >= [issue_date]");
            tb.HasCheckConstraint("CK_invoices_currency_upper", "[currency] = upper([currency])");
            tb.HasCheckConstraint("CK_invoices_target_link", "(CASE WHEN [reservation_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [agreement_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [ticket_charge_proposal_id] IS NULL THEN 0 ELSE 1 END) >= 1");
        });

        entity.HasIndex(e => e.TicketChargeProposalId, "invoices_ticket_charge_proposal_id_uidx")
            .IsUnique()
            .HasFilter("([ticket_charge_proposal_id] IS NOT NULL)");
        entity.HasIndex(e => e.InvoiceNo, "UQ__invoices__F58CA1E2D09A819E").IsUnique();
        entity.HasIndex(e => new { e.AgreementId, e.BillingPeriod }, "invoices_agreement_billing_period_uidx")
            .IsUnique()
            .HasFilter("([agreement_id] IS NOT NULL AND [billing_period] IS NOT NULL AND [status]<>'voided')");
        entity.HasIndex(e => e.AgreementId, "invoices_agreement_id_idx").HasFilter("([agreement_id] IS NOT NULL)");
        entity.HasIndex(e => new { e.CustomerId, e.Status, e.DueDate }, "invoices_customer_status_due_idx");
        entity.HasIndex(e => e.DueDate, "invoices_open_due_idx").HasFilter("([status] IN ('open', 'partially_paid', 'overdue'))");
        entity.HasIndex(e => e.ReservationId, "invoices_reservation_id_idx").HasFilter("([reservation_id] IS NOT NULL)");
        entity.HasIndex(e => new { e.Status, e.DueDate }, "ix_invoices_status_due_date");
        entity.HasIndex(e => new { e.Status, e.TotalAmount, e.PaidAmount }, "ix_invoices_status_amounts");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
        entity.Property(e => e.BillingPeriod).HasMaxLength(64).HasColumnName("billing_period");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.Currency).HasMaxLength(3).IsUnicode(false).HasDefaultValue("VND").IsFixedLength().HasColumnName("currency");
        entity.Property(e => e.CustomerId).HasColumnName("customer_id");
        entity.Property(e => e.DiscountAmount).HasColumnType("numeric(14, 2)").HasColumnName("discount_amount");
        entity.Property(e => e.DueDate).HasColumnName("due_date");
        entity.Property(e => e.InvoiceNo).HasMaxLength(255).HasColumnName("invoice_no");
        entity.Property(e => e.IssueDate).HasColumnName("issue_date");
        entity.Property(e => e.OpenedAt).HasColumnName("opened_at");
        entity.Property(e => e.PaidAmount).HasColumnType("numeric(14, 2)").HasColumnName("paid_amount");
        entity.Property(e => e.ReservationId).HasColumnName("reservation_id");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("draft").HasColumnName("status");
        entity.Property(e => e.SubtotalAmount).HasColumnType("numeric(14, 2)").HasColumnName("subtotal_amount");
        entity.Property(e => e.TaxAmount).HasColumnType("numeric(14, 2)").HasColumnName("tax_amount");
        entity.Property(e => e.TicketChargeProposalId).HasColumnName("ticket_charge_proposal_id");
        entity.Property(e => e.TotalAmount).HasColumnType("numeric(14, 2)").HasColumnName("total_amount");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("updated_at");
        entity.Property(e => e.VoidedAt).HasColumnName("voided_at");

        entity.HasOne(d => d.Agreement).WithMany(p => p.Invoices)
            .HasForeignKey(d => d.AgreementId)
            .HasConstraintName("FK__invoices__agreem__4830B400");

        entity.HasOne(d => d.Customer).WithMany(p => p.Invoices)
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__invoices__custom__46486B8E");

        entity.HasOne(d => d.Reservation).WithMany(p => p.Invoices)
            .HasForeignKey(d => d.ReservationId)
            .HasConstraintName("FK__invoices__reserv__473C8FC7");

        entity.HasOne(d => d.TicketChargeProposal).WithOne(p => p.Invoice)
            .HasForeignKey<Invoice>(d => d.TicketChargeProposalId)
            .HasConstraintName("FK__invoices__ticket__4924D839");
    }
}
