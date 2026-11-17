using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Pricing;

public sealed class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__promotio__3213E83F39C628D1");
        entity.ToTable("promotions", "core", tb =>
        {
            tb.HasCheckConstraint("CK_promotions_discount_type", EnumDbMapper.ToSqlCheckInClause<PromotionDiscountType>("discount_type"));
            tb.HasCheckConstraint("CK_promotions_values", "[discount_value] > 0 AND ([max_discount_amount] IS NULL OR [max_discount_amount] > 0)");
            tb.HasCheckConstraint("CK_promotions_percentage_cap", "[discount_type] <> 'percentage' OR [discount_value] <= 100");
            tb.HasCheckConstraint("CK_promotions_limits", "([usage_limit] IS NULL OR [usage_limit] > 0) AND ([per_customer_limit] IS NULL OR [per_customer_limit] > 0)");
            tb.HasCheckConstraint("CK_promotions_dates", "[valid_to] > [valid_from]");
        });

        entity.HasIndex(e => e.Code, "UQ__promotio__357D4CF9D56E4DD4").IsUnique();
        entity.HasIndex(e => new { e.ValidFrom, e.ValidTo }, "promotions_active_period_idx").HasFilter("([is_active]=(1))");
        entity.HasIndex(e => e.CreatedBy, "promotions_created_by_idx").HasFilter("([created_by] IS NOT NULL)");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.Code).HasMaxLength(255).HasColumnName("code");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.CreatedBy).HasColumnName("created_by");
        entity.Property(e => e.Description).HasMaxLength(255).HasColumnName("description");
        entity.Property(e => e.DiscountType).HasMaxLength(255).HasColumnName("discount_type");
        entity.Property(e => e.DiscountValue).HasColumnType("numeric(14, 2)").HasColumnName("discount_value");
        entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
        entity.Property(e => e.MaxDiscountAmount).HasColumnType("numeric(14, 2)").HasColumnName("max_discount_amount");
        entity.Property(e => e.Name).HasMaxLength(255).HasColumnName("name");
        entity.Property(e => e.PerCustomerLimit).HasColumnName("per_customer_limit");
        entity.Property(e => e.UsageLimit).HasColumnName("usage_limit");
        entity.Property(e => e.ValidFrom).HasColumnName("valid_from");
        entity.Property(e => e.ValidTo).HasColumnName("valid_to");

        entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Promotions)
            .HasForeignKey(d => d.CreatedBy)
            .HasConstraintName("FK__promotion__creat__5F7E2DAC");
    }
}
