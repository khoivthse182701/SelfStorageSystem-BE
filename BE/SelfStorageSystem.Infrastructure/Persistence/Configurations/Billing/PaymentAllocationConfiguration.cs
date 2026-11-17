using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Billing;

public sealed class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> entity)
    {
        entity.HasKey(e => new { e.PaymentId, e.InvoiceId }).HasName("PK__payment___6247163E1D5CC991");
        entity.ToTable("payment_allocations", "core", tb =>
        {
            tb.HasTrigger("trg_payment_allocation_guard");
            tb.HasCheckConstraint("CK_payment_allocations_amount", "[allocated_amount] > 0");
        });

        entity.HasIndex(e => new { e.InvoiceId, e.PaymentId }, "payment_allocations_invoice_id_idx");

        entity.Property(e => e.PaymentId).HasColumnName("payment_id");
        entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
        entity.Property(e => e.AllocatedAmount).HasColumnType("numeric(14, 2)").HasColumnName("allocated_amount");
        entity.Property(e => e.AllocatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("allocated_at");

        entity.HasOne(d => d.Invoice).WithMany(p => p.PaymentAllocations)
            .HasForeignKey(d => d.InvoiceId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__payment_a__invoi__76EBA2E9");

        entity.HasOne(d => d.Payment).WithMany(p => p.PaymentAllocations)
            .HasForeignKey(d => d.PaymentId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__payment_a__payme__75F77EB0");
    }
}
