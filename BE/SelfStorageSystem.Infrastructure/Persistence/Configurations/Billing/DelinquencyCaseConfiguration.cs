using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Billing;

public sealed class DelinquencyCaseConfiguration : IEntityTypeConfiguration<DelinquencyCase>
{
    public void Configure(EntityTypeBuilder<DelinquencyCase> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__delinque__3213E83F82280640");
        entity.ToTable("delinquency_cases", "core", tb =>
        {
            tb.HasTrigger("trg_delinquency_cases_validate_scope");
            tb.HasCheckConstraint("CK_delinquency_cases_status", EnumDbMapper.ToSqlCheckInClause<DelinquencyCaseStatus>("status"));
            tb.HasCheckConstraint("CK_delinquency_cases_outstanding", "[outstanding_snapshot] > 0");
            tb.HasCheckConstraint("CK_delinquency_cases_dates", "[grace_ends_at] >= [opened_at] AND ([resolved_at] IS NULL OR [resolved_at] >= [opened_at])");
        });

        entity.HasIndex(e => new { e.AgreementId, e.Status, e.OpenedAt }, "delinquency_cases_agreement_idx").IsDescending(false, false, true);
        entity.HasIndex(e => e.InvoiceId, "delinquency_cases_invoice_id_idx");
        entity.HasIndex(e => e.InvoiceId, "delinquency_cases_one_open_invoice_uidx")
            .IsUnique()
            .HasFilter("([resolved_at] IS NULL)");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
        entity.Property(e => e.GraceEndsAt).HasColumnName("grace_ends_at");
        entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
        entity.Property(e => e.Notes).HasMaxLength(255).HasColumnName("notes");
        entity.Property(e => e.OpenedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("opened_at");
        entity.Property(e => e.OutstandingSnapshot).HasColumnType("numeric(14, 2)").HasColumnName("outstanding_snapshot");
        entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("grace").HasColumnName("status");

        entity.HasOne(d => d.Agreement).WithMany(p => p.DelinquencyCases)
            .HasForeignKey(d => d.AgreementId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__delinquen__agree__573DED66");

        entity.HasOne(d => d.Invoice).WithOne(p => p.DelinquencyCase)
            .HasForeignKey<DelinquencyCase>(d => d.InvoiceId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__delinquen__invoi__5832119F");
    }
}
