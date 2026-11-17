using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.ReservationLease;

public sealed class UnitTransferRequestConfiguration : IEntityTypeConfiguration<UnitTransferRequest>
{
    public void Configure(EntityTypeBuilder<UnitTransferRequest> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__unit_tra__3213E83F997BA96F");
        entity.ToTable("unit_transfer_requests", "core", tb =>
        {
            tb.HasCheckConstraint("CK_unit_transfer_requests_status", EnumDbMapper.ToSqlCheckInClause<TransferRequestStatus>("status"));
            tb.HasCheckConstraint("CK_unit_transfer_requests_type", "[transfer_type] IN ('upgrade', 'downgrade', 'same_tier', 'relocation')");
        });

        entity.HasIndex(e => new { e.AgreementId, e.Status, e.CreatedAt }, "unit_transfer_requests_agreement_idx").IsDescending(false, false, true);
        entity.HasIndex(e => e.FromUnitId, "unit_transfer_requests_from_unit_id_idx");
        entity.HasIndex(e => e.AgreementId, "unit_transfer_requests_one_open_uidx")
            .IsUnique()
            .HasFilter("([status] IN ('pending_payment', 'paid', 'moved_out_in_transit', 'pending', 'approved', 'scheduled'))");
        entity.HasIndex(e => e.RequestedBy, "unit_transfer_requests_requested_by_idx");
        entity.HasIndex(e => e.RequestedFacilityId, "unit_transfer_requests_requested_facility_id_idx");
        entity.HasIndex(e => e.RequestedUnitTypeId, "unit_transfer_requests_requested_unit_type_id_idx");
        entity.HasIndex(e => e.ReviewedBy, "unit_transfer_requests_reviewed_by_idx").HasFilter("([reviewed_by] IS NOT NULL)");
        entity.HasIndex(e => e.ToUnitId, "unit_transfer_requests_to_unit_id_idx").HasFilter("([to_unit_id] IS NOT NULL)");
        entity.HasIndex(e => e.TargetInvoiceId, "unit_transfer_requests_target_invoice_id_idx").HasFilter("([target_invoice_id] IS NOT NULL)");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
        entity.Property(e => e.CleaningFee).HasColumnType("decimal(18, 2)").HasDefaultValue(0m).HasColumnName("cleaning_fee");
        entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
        entity.Property(e => e.CreditAmount).HasColumnType("decimal(18, 2)").HasColumnName("credit_amount");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.DiffDepositAmount).HasColumnType("decimal(18, 2)").HasColumnName("diff_deposit_amount");
        entity.Property(e => e.DiffRentAmount).HasColumnType("decimal(18, 2)").HasColumnName("diff_rent_amount");
        entity.Property(e => e.FromUnitId).HasColumnName("from_unit_id");
        entity.Property(e => e.MoveInDate).HasColumnName("move_in_date");
        entity.Property(e => e.MoveOutDate).HasColumnName("move_out_date");
        entity.Property(e => e.PenaltyAmount).HasColumnType("decimal(18, 2)").HasColumnName("penalty_amount");
        entity.Property(e => e.Reason).HasMaxLength(255).HasColumnName("reason");
        entity.Property(e => e.RequestedBy).HasColumnName("requested_by");
        entity.Property(e => e.RequestedEffectiveDate).HasColumnName("requested_effective_date");
        entity.Property(e => e.RequestedFacilityId).HasColumnName("requested_facility_id");
        entity.Property(e => e.RequestedUnitTypeId).HasColumnName("requested_unit_type_id");
        entity.Property(e => e.RescheduleCount).HasColumnName("reschedule_count");
        entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
        entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("pending_payment").HasColumnName("status");
        entity.Property(e => e.TargetInvoiceId).HasColumnName("target_invoice_id");
        entity.Property(e => e.ToUnitId).HasColumnName("to_unit_id");
        entity.Property(e => e.TotalPaidAmount).HasColumnType("decimal(18, 2)").HasColumnName("total_paid_amount");
        entity.Property(e => e.TransferType).HasMaxLength(32).HasDefaultValue("upgrade").HasColumnName("transfer_type");

        entity.HasOne(d => d.Agreement).WithMany(p => p.UnitTransferRequests)
            .HasForeignKey(d => d.AgreementId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__unit_tran__agree__61316BF4");

        entity.HasOne(d => d.FromUnit).WithMany(p => p.UnitTransferRequestFromUnits)
            .HasForeignKey(d => d.FromUnitId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__unit_tran__from___6319B466");

        entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.UnitTransferRequestRequestedByNavigations)
            .HasForeignKey(d => d.RequestedBy)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__unit_tran__reque__66EA454A");

        entity.HasOne(d => d.RequestedFacility).WithMany(p => p.UnitTransferRequests)
            .HasForeignKey(d => d.RequestedFacilityId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_unit_transfer_requests_requested_facility");

        entity.HasOne(d => d.RequestedUnitType).WithMany(p => p.UnitTransferRequests)
            .HasForeignKey(d => d.RequestedUnitTypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__unit_tran__reque__6225902D");

        entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.UnitTransferRequestReviewedByNavigations)
            .HasForeignKey(d => d.ReviewedBy)
            .HasConstraintName("FK__unit_tran__revie__67DE6983");

        entity.HasOne(d => d.ToUnit).WithMany(p => p.UnitTransferRequestToUnits)
            .HasForeignKey(d => d.ToUnitId)
            .HasConstraintName("FK__unit_tran__to_un__640DD89F");
    }
}
