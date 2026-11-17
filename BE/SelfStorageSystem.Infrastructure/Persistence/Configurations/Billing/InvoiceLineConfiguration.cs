using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Billing;

public sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__invoice___3213E83F555DA62F");
        entity.ToTable("invoice_lines", "core", tb =>
        {
            tb.HasTrigger("trg_invoice_lines_refresh_totals");
            tb.HasTrigger("trg_invoice_lines_guard");
            tb.HasCheckConstraint("CK_invoice_lines_line_type", EnumDbMapper.ToSqlCheckInClause<InvoiceLineType>("line_type"));
            tb.HasCheckConstraint("CK_invoice_lines_quantity", "[quantity] > 0");
            tb.HasCheckConstraint("CK_invoice_lines_metadata_json", "ISJSON([metadata]) = 1");
            tb.HasCheckConstraint("CK_invoice_lines_unit_price", "([line_type] = 'discount' AND [unit_price] <= 0) OR ([line_type] <> 'discount' AND [unit_price] >= 0)");
        });

        entity.HasIndex(e => e.InvoiceId, "invoice_lines_invoice_id_idx");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.Description).HasMaxLength(255).HasColumnName("description");
        entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
        entity.Property(e => e.LineAmount)
            .HasComputedColumnSql("(CONVERT([numeric](14,2),round([quantity]*[unit_price],(2))))", true)
            .HasColumnType("numeric(14, 2)")
            .HasColumnName("line_amount");
        entity.Property(e => e.LineType).HasMaxLength(255).HasColumnName("line_type");
        entity.Property(e => e.Metadata).HasDefaultValue("{}").HasColumnName("metadata");
        entity.Property(e => e.Quantity).HasDefaultValue(1m).HasColumnType("numeric(12, 3)").HasColumnName("quantity");
        entity.Property(e => e.UnitPrice).HasColumnType("numeric(14, 2)").HasColumnName("unit_price");

        entity.HasOne(d => d.Invoice).WithMany(p => p.InvoiceLines)
            .HasForeignKey(d => d.InvoiceId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__invoice_l__invoi__5E1FF51F");
    }
}
