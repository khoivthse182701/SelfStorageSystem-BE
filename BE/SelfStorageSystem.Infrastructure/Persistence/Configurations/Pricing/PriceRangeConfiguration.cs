using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Pricing;

public sealed class PriceRangeConfiguration : IEntityTypeConfiguration<PriceRange>
{
    public void Configure(EntityTypeBuilder<PriceRange> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__price_ra__3213E83F2173EE04");
        entity.ToTable("price_ranges", "core", tb =>
        {
            tb.HasTrigger("trg_price_range_no_overlap");
            tb.HasCheckConstraint("CK_price_ranges_rates", "[min_monthly_rate] >= 0 AND [max_monthly_rate] >= [min_monthly_rate]");
            tb.HasCheckConstraint("CK_price_ranges_dates", "[valid_to] IS NULL OR [valid_to] > [valid_from]");
        });

        entity.HasIndex(e => e.CreatedBy, "price_ranges_created_by_idx").HasFilter("([created_by] IS NOT NULL)");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.CreatedBy).HasColumnName("created_by");
        entity.Property(e => e.MaxMonthlyRate).HasColumnType("numeric(14, 2)").HasColumnName("max_monthly_rate");
        entity.Property(e => e.MinMonthlyRate).HasColumnType("numeric(14, 2)").HasColumnName("min_monthly_rate");
        entity.Property(e => e.UnitTypeId).HasColumnName("unit_type_id");
        entity.Property(e => e.ValidFrom).HasColumnName("valid_from");
        entity.Property(e => e.ValidTo).HasColumnName("valid_to");

        entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PriceRanges)
            .HasForeignKey(d => d.CreatedBy)
            .HasConstraintName("FK__price_ran__creat__31B762FC");

        entity.HasOne(d => d.UnitType).WithMany(p => p.PriceRanges)
            .HasForeignKey(d => d.UnitTypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__price_ran__unit___2FCF1A8A");
    }
}
