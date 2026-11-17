using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Billing;

public sealed class DelinquencyActionConfiguration : IEntityTypeConfiguration<DelinquencyAction>
{
    public void Configure(EntityTypeBuilder<DelinquencyAction> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__delinque__3213E83F0C91ECFE");
        entity.ToTable("delinquency_actions", "core", tb =>
        {
            tb.HasCheckConstraint("CK_delinquency_actions_type", EnumDbMapper.ToSqlCheckInClause<DelinquencyActionType>("action_type"));
            tb.HasCheckConstraint("CK_delinquency_actions_details_json", "ISJSON([details]) = 1");
        });

        entity.HasIndex(e => new { e.DelinquencyCaseId, e.OccurredAt }, "delinquency_actions_case_occurred_idx");
        entity.HasIndex(e => e.PerformedBy, "delinquency_actions_performed_by_idx").HasFilter("([performed_by] IS NOT NULL)");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.ActionType).HasMaxLength(255).HasColumnName("action_type");
        entity.Property(e => e.DelinquencyCaseId).HasColumnName("delinquency_case_id");
        entity.Property(e => e.Details).HasDefaultValue("{}").HasColumnName("details");
        entity.Property(e => e.OccurredAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("occurred_at");
        entity.Property(e => e.PerformedBy).HasColumnName("performed_by");

        entity.HasOne(d => d.DelinquencyCase).WithMany(p => p.DelinquencyActions)
            .HasForeignKey(d => d.DelinquencyCaseId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__delinquen__delin__60C757A0");

        entity.HasOne(d => d.PerformedByNavigation).WithMany(p => p.DelinquencyActions)
            .HasForeignKey(d => d.PerformedBy)
            .HasConstraintName("FK__delinquen__perfo__62AFA012");
    }
}
