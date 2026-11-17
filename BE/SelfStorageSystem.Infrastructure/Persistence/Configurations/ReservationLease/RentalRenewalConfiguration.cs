using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.ReservationLease;

public sealed class RentalRenewalConfiguration : IEntityTypeConfiguration<RentalRenewal>
{
    public void Configure(EntityTypeBuilder<RentalRenewal> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__rental_r__3213E83FD6FCC618");
        entity.ToTable("rental_renewals", "core", tb =>
        {
            tb.HasCheckConstraint("CK_rental_renewals_status", EnumDbMapper.ToSqlCheckInClause<RenewalStatus>("status"));
            tb.HasCheckConstraint("CK_rental_renewals_dates", "[requested_end_date] > [old_end_date] AND ([approved_end_date] IS NULL OR [approved_end_date] > [old_end_date])");
            tb.HasCheckConstraint("CK_rental_renewals_rates", "[old_monthly_rate] >= 0 AND ([new_monthly_rate] IS NULL OR [new_monthly_rate] >= 0)");
        });

        entity.HasIndex(e => new { e.AgreementId, e.CreatedAt }, "rental_renewals_agreement_idx").IsDescending(false, true);
        entity.HasIndex(e => e.AgreementId, "rental_renewals_one_open_uidx")
            .IsUnique()
            .HasFilter("([status] IN ('pending_payment', 'paid'))");
        entity.HasIndex(e => e.RequestedBy, "rental_renewals_requested_by_idx");
        entity.HasIndex(e => e.ReviewedBy, "rental_renewals_reviewed_by_idx").HasFilter("([reviewed_by] IS NOT NULL)");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
        entity.Property(e => e.ApprovedEndDate).HasColumnName("approved_end_date");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.NewMonthlyRate).HasColumnType("numeric(14, 2)").HasColumnName("new_monthly_rate");
        entity.Property(e => e.OldEndDate).HasColumnName("old_end_date");
        entity.Property(e => e.OldMonthlyRate).HasColumnType("numeric(14, 2)").HasColumnName("old_monthly_rate");
        entity.Property(e => e.RequestedBy).HasColumnName("requested_by");
        entity.Property(e => e.RequestedEndDate).HasColumnName("requested_end_date");
        entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
        entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("pending_payment").HasColumnName("status");

        entity.HasOne(d => d.Agreement).WithOne(p => p.RentalRenewal)
            .HasForeignKey<RentalRenewal>(d => d.AgreementId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__rental_re__agree__55BFB948");

        entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.RentalRenewalRequestedByNavigations)
            .HasForeignKey(d => d.RequestedBy)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__rental_re__reque__56B3DD81");

        entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.RentalRenewalReviewedByNavigations)
            .HasForeignKey(d => d.ReviewedBy)
            .HasConstraintName("FK__rental_re__revie__5B78929E");
    }
}
