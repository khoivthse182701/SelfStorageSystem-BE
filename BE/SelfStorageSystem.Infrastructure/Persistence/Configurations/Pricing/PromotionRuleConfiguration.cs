using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Pricing;

public sealed class PromotionRuleConfiguration : IEntityTypeConfiguration<PromotionRule>
{
    public void Configure(EntityTypeBuilder<PromotionRule> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__promotio__3213E83F600E839C");
        entity.ToTable("promotion_rules", "core", tb =>
        {
            tb.HasCheckConstraint("CK_promotion_rules_rule_type", EnumDbMapper.ToSqlCheckInClause<PromotionRuleType>("rule_type"));
            tb.HasCheckConstraint("CK_promotion_rules_operator", EnumDbMapper.ToSqlCheckInClause<PromotionOperator>("operator"));
        });

        entity.HasIndex(e => new { e.PromotionId, e.RuleType }, "UQ__promotio__DE90F0E956A0A645").IsUnique();
        entity.HasIndex(e => e.PromotionId, "promotion_rules_promotion_id_idx");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.Operator).HasMaxLength(255).HasDefaultValue("eq").HasColumnName("operator");
        entity.Property(e => e.PromotionId).HasColumnName("promotion_id");
        entity.Property(e => e.RuleType).HasMaxLength(255).HasColumnName("rule_type");
        entity.Property(e => e.RuleValue).HasColumnName("rule_value");

        entity.HasOne(d => d.Promotion).WithMany(p => p.PromotionRules)
            .HasForeignKey(d => d.PromotionId)
            .HasConstraintName("FK__promotion__promo__662B2B3B");
    }
}
