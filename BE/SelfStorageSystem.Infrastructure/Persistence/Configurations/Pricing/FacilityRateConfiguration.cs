using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Pricing;

public sealed class FacilityRateConfiguration : IEntityTypeConfiguration<FacilityRate>
{
    public void Configure(EntityTypeBuilder<FacilityRate> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__facility__3213E83F925384A1");
        entity.ToTable("facility_rates", "core", tb =>
        {
            tb.HasTrigger("trg_facility_rate_no_overlap");
            tb.HasTrigger("trg_facility_rates_validate_price_range");
            tb.HasCheckConstraint("CK_facility_rates_amounts", "[monthly_rate] >= 0 AND [deposit_amount] >= 0 AND [booking_fee] >= 0");
            tb.HasCheckConstraint("CK_facility_rates_dates", "[valid_to] IS NULL OR [valid_to] > [valid_from]");
        });

        entity.HasIndex(e => e.CreatedBy, "facility_rates_created_by_idx").HasFilter("([created_by] IS NOT NULL)");
        entity.HasIndex(e => e.UnitTypeId, "facility_rates_unit_type_id_idx");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.BookingFee).HasColumnType("numeric(14, 2)").HasColumnName("booking_fee");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.CreatedBy).HasColumnName("created_by");
        entity.Property(e => e.DepositAmount).HasColumnType("numeric(14, 2)").HasColumnName("deposit_amount");
        entity.Property(e => e.FacilityId).HasColumnName("facility_id");
        entity.Property(e => e.MonthlyRate).HasColumnType("numeric(14, 2)").HasColumnName("monthly_rate");
        entity.Property(e => e.UnitTypeId).HasColumnName("unit_type_id");
        entity.Property(e => e.ValidFrom).HasColumnName("valid_from");
        entity.Property(e => e.ValidTo).HasColumnName("valid_to");

        entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.FacilityRates)
            .HasForeignKey(d => d.CreatedBy)
            .HasConstraintName("FK__facility___creat__3D2915A8");

        entity.HasOne(d => d.Facility).WithMany(p => p.FacilityRates)
            .HasForeignKey(d => d.FacilityId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__facility___facil__37703C52");

        entity.HasOne(d => d.UnitType).WithMany(p => p.FacilityRates)
            .HasForeignKey(d => d.UnitTypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__facility___unit___3864608B");
    }
}
