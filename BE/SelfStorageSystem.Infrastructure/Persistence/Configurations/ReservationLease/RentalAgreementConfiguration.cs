using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.ReservationLease;

public sealed class RentalAgreementConfiguration : IEntityTypeConfiguration<RentalAgreement>
{
    public void Configure(EntityTypeBuilder<RentalAgreement> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__rental_a__3213E83F74E7BFF0");
        entity.ToTable("rental_agreements", "core", tb =>
        {
            tb.HasTrigger("trg_agreement_status_transition");
            tb.HasTrigger("trg_rental_agreements_validate_reservation");
            tb.HasCheckConstraint("CK_rental_agreements_status", EnumDbMapper.ToSqlCheckInClause<ContractStatus>("status"));
            tb.HasCheckConstraint("CK_rental_agreements_dates", "[end_date] > [start_date]");
            tb.HasCheckConstraint("CK_rental_agreements_amounts", "[monthly_rate_snapshot] >= 0 AND [deposit_snapshot] >= 0");
            tb.HasCheckConstraint("CK_rental_agreements_checkout", "([checked_out_at] IS NULL) OR ([checked_in_at] IS NOT NULL AND [checked_out_at] >= [checked_in_at])");
        });

        entity.HasIndex(e => e.ReservationId, "UQ__rental_a__31384C28E1114D31").IsUnique();
        entity.HasIndex(e => e.AgreementNo, "UQ__rental_a__A476927E4078718C").IsUnique();
        entity.HasIndex(e => new { e.Id, e.CustomerId, e.FacilityId }, "UQ__rental_a__D1775C6C828F19B8").IsUnique();
        entity.HasIndex(e => new { e.CustomerId, e.Status, e.EndDate }, "rental_agreements_customer_status_idx");
        entity.HasIndex(e => new { e.FacilityId, e.Status, e.EndDate }, "rental_agreements_facility_status_idx");
        entity.HasIndex(e => e.PolicyVersionId, "rental_agreements_policy_version_id_idx");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.ActualEndDate).HasColumnName("actual_end_date");
        entity.Property(e => e.AgreementNo).HasMaxLength(255).HasColumnName("agreement_no");
        entity.Property(e => e.CheckedInAt).HasColumnName("checked_in_at");
        entity.Property(e => e.CheckedOutAt).HasColumnName("checked_out_at");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.CustomerId).HasColumnName("customer_id");
        entity.Property(e => e.DepositBalance).HasColumnType("numeric(14, 2)").HasColumnName("deposit_balance");
        entity.Property(e => e.DepositSnapshot).HasColumnType("numeric(14, 2)").HasColumnName("deposit_snapshot");
        entity.Property(e => e.EndDate).HasColumnName("end_date");
        entity.Property(e => e.FacilityId).HasColumnName("facility_id");
        entity.Property(e => e.MonthlyRateSnapshot).HasColumnType("numeric(14, 2)").HasColumnName("monthly_rate_snapshot");
        entity.Property(e => e.NegativeDebtNotifiedAt).HasColumnName("negative_debt_notified_at");
        entity.Property(e => e.OverdueBankReminderSentAt).HasColumnName("overdue_bank_reminder_sent_at");
        entity.Property(e => e.PolicyVersionId).HasColumnName("policy_version_id");
        entity.Property(e => e.ReservationId).HasColumnName("reservation_id");
        entity.Property(e => e.SignedAt).HasColumnName("signed_at");
        entity.Property(e => e.StartDate).HasColumnName("start_date");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("draft").HasColumnName("status");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("updated_at");

        entity.HasOne(d => d.Customer).WithMany(p => p.RentalAgreements)
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__rental_ag__custo__04AFB25B");

        entity.HasOne(d => d.Facility).WithMany(p => p.RentalAgreements)
            .HasForeignKey(d => d.FacilityId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__rental_ag__facil__05A3D694");

        entity.HasOne(d => d.PolicyVersion).WithMany(p => p.RentalAgreements)
            .HasForeignKey(d => d.PolicyVersionId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__rental_ag__polic__0697FACD");

        entity.HasOne(d => d.Reservation).WithOne(p => p.RentalAgreement)
            .HasForeignKey<RentalAgreement>(d => d.ReservationId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__rental_ag__reser__03BB8E22");
    }
}
