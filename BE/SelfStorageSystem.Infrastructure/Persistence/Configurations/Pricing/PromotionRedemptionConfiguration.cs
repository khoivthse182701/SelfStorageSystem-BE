using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Pricing;

public sealed class PromotionRedemptionConfiguration : IEntityTypeConfiguration<PromotionRedemption>
{
    public void Configure(EntityTypeBuilder<PromotionRedemption> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__promotio__3213E83F4B06451A");
        entity.ToTable("promotion_redemptions", "core", tb =>
        {
            tb.HasTrigger("trg_promotion_redemptions_validate_scope");
            tb.HasCheckConstraint("CK_promotion_redemptions_status", EnumDbMapper.ToSqlCheckInClause<PromotionRedemptionStatus>("status"));
            tb.HasCheckConstraint("CK_promotion_redemptions_amount", "[discount_amount] >= 0");
            tb.HasCheckConstraint("CK_promotion_redemptions_target", "(CASE WHEN [reservation_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [invoice_id] IS NULL THEN 0 ELSE 1 END) >= 1");
        });

        entity.HasIndex(e => new { e.CustomerId, e.PromotionId, e.Status }, "promotion_redemptions_customer_idx");
        entity.HasIndex(e => e.InvoiceId, "promotion_redemptions_invoice_id_idx").HasFilter("([invoice_id] IS NOT NULL)");
        entity.HasIndex(e => new { e.PromotionId, e.Status, e.RedeemedAt }, "promotion_redemptions_promotion_idx");
        entity.HasIndex(e => e.ReservationId, "promotion_redemptions_reservation_id_idx").HasFilter("([reservation_id] IS NOT NULL)");
        entity.HasIndex(e => new { e.PromotionId, e.ReservationId }, "promotion_redemptions_reservation_uidx")
            .IsUnique()
            .HasFilter("([reservation_id] IS NOT NULL AND [status]<>'released')");
        entity.HasIndex(e => e.ReservationId, "promotion_redemptions_one_per_reservation_uidx")
            .IsUnique()
            .HasFilter("([reservation_id] IS NOT NULL AND [status]<>'released')");
        entity.HasIndex(e => e.InvoiceId, "promotion_redemptions_one_per_invoice_uidx")
            .IsUnique()
            .HasFilter("([invoice_id] IS NOT NULL AND [status]<>'released')");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.CustomerId).HasColumnName("customer_id");
        entity.Property(e => e.DiscountAmount).HasColumnType("numeric(14, 2)").HasColumnName("discount_amount");
        entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
        entity.Property(e => e.PromotionId).HasColumnName("promotion_id");
        entity.Property(e => e.RedeemedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("redeemed_at");
        entity.Property(e => e.ReservationId).HasColumnName("reservation_id");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("reserved").HasColumnName("status");

        entity.HasOne(d => d.Customer).WithMany(p => p.PromotionRedemptions)
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__promotion__custo__0FB750B3");

        entity.HasOne(d => d.Invoice).WithMany(p => p.PromotionRedemptions)
            .HasForeignKey(d => d.InvoiceId)
            .HasConstraintName("FK__promotion__invoi__119F9925");

        entity.HasOne(d => d.Promotion).WithMany(p => p.PromotionRedemptions)
            .HasForeignKey(d => d.PromotionId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__promotion__promo__0EC32C7A");

        entity.HasOne(d => d.Reservation).WithMany(p => p.PromotionRedemptions)
            .HasForeignKey(d => d.ReservationId)
            .HasConstraintName("FK__promotion__reser__10AB74EC");
    }
}
