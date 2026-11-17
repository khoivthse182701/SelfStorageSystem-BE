using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Pricing;

public sealed class FeeRuleConfiguration : IEntityTypeConfiguration<FeeRule>
{
    public void Configure(EntityTypeBuilder<FeeRule> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__fee_rule__3213E83F5F89760E");
        entity.ToTable("fee_rules", "core", tb =>
        {
            tb.HasTrigger("trg_fee_rule_no_overlap");
            tb.HasCheckConstraint("CK_fee_rules_fee_type", EnumDbMapper.ToSqlCheckInClause<FeeType>("fee_type"));
            tb.HasCheckConstraint("CK_fee_rules_calculation_method", EnumDbMapper.ToSqlCheckInClause<FeeCalculationMethod>("calculation_method"));
            tb.HasCheckConstraint("CK_fee_rules_conditions_json", "ISJSON([conditions]) = 1");
            tb.HasCheckConstraint("CK_fee_rules_dates", "[valid_to] IS NULL OR [valid_to] > [valid_from]");
            tb.HasCheckConstraint("CK_fee_rules_amount", "[amount] IS NULL OR [amount] >= 0");
            tb.HasCheckConstraint("CK_fee_rules_rate_percent", "[rate_percent] IS NULL OR ([rate_percent] > 0 AND [rate_percent] <= 1000)");
            tb.HasCheckConstraint("CK_fee_rules_grace_days", "[grace_days] >= 0");
            tb.HasCheckConstraint("CK_fee_rules_method_value", "([calculation_method] IN ('flat', 'per_day') AND [amount] IS NOT NULL) OR ([calculation_method] = 'percentage' AND [rate_percent] IS NOT NULL)");
        });

        entity.HasIndex(e => e.CreatedBy, "fee_rules_created_by_idx").HasFilter("([created_by] IS NOT NULL)");
        entity.HasIndex(e => e.FacilityId, "fee_rules_facility_id_idx").HasFilter("([facility_id] IS NOT NULL)");
        entity.HasIndex(e => new { e.FacilityId, e.Code, e.ValidFrom }, "fee_rules_scope_code_start_uidx")
            .IsUnique()
            .HasFilter(null);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.Amount).HasColumnType("numeric(14, 2)").HasColumnName("amount");
        entity.Property(e => e.CalculationMethod).HasMaxLength(255).HasColumnName("calculation_method");
        entity.Property(e => e.Code).HasMaxLength(255).HasColumnName("code");
        entity.Property(e => e.Conditions).HasDefaultValue("{}").HasColumnName("conditions");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.CreatedBy).HasColumnName("created_by");
        entity.Property(e => e.FacilityId).HasColumnName("facility_id");
        entity.Property(e => e.FeeType).HasMaxLength(255).HasColumnName("fee_type");
        entity.Property(e => e.GraceDays).HasColumnName("grace_days");
        entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
        entity.Property(e => e.RatePercent).HasColumnType("numeric(7, 4)").HasColumnName("rate_percent");
        entity.Property(e => e.ValidFrom).HasColumnName("valid_from");
        entity.Property(e => e.ValidTo).HasColumnName("valid_to");

        entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.FeeRules)
            .HasForeignKey(d => d.CreatedBy)
            .HasConstraintName("FK__fee_rules__creat__531856C7");

        entity.HasOne(d => d.Facility).WithMany(p => p.FeeRules)
            .HasForeignKey(d => d.FacilityId)
            .HasConstraintName("FK__fee_rules__facil__498EEC8D");
    }
}
