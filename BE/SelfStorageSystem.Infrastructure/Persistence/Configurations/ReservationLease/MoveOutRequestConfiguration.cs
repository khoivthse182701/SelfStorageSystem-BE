using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.ReservationLease;

public sealed class MoveOutRequestConfiguration : IEntityTypeConfiguration<MoveOutRequest>
{
    public void Configure(EntityTypeBuilder<MoveOutRequest> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__move_out__3213E83F18F51BD9");
        entity.ToTable("move_out_requests", "core", tb =>
        {
            tb.HasCheckConstraint("CK_move_out_requests_status", EnumDbMapper.ToSqlCheckInClause<MoveOutStatus>("status"));
            tb.HasCheckConstraint("CK_move_out_requests_finalized_at", "([status] IN ('completed', 'cancelled') AND [finalized_at] IS NOT NULL) OR ([status] NOT IN ('completed', 'cancelled') AND [finalized_at] IS NULL)");
        });

        entity.HasIndex(e => new { e.AgreementId, e.Status, e.CreatedAt }, "move_out_requests_agreement_idx").IsDescending(false, false, true);
        entity.HasIndex(e => e.AppointmentId, "move_out_requests_appointment_id_idx").HasFilter("([appointment_id] IS NOT NULL)");
        entity.HasIndex(e => e.AgreementId, "move_out_requests_one_open_uidx")
            .IsUnique()
            .HasFilter("([status]<>'completed' AND [status]<>'cancelled')");
        entity.HasIndex(e => e.RequestedBy, "move_out_requests_requested_by_idx");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
        entity.Property(e => e.AppointmentId).HasColumnName("appointment_id");
        entity.Property(e => e.BankName).HasMaxLength(128).HasColumnName("bank_name");
        entity.Property(e => e.BankAccountNo).HasMaxLength(64).HasColumnName("bank_account_no");
        entity.Property(e => e.BankAccountHolder).HasMaxLength(128).HasColumnName("bank_account_holder");
        entity.Property(e => e.BankSubmittedAt).HasColumnName("bank_submitted_at");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.DamageDeductionAmount).HasColumnType("numeric(14, 2)").HasDefaultValue(0m).HasColumnName("damage_deduction_amount");
        entity.Property(e => e.FinalizedAt).HasColumnName("finalized_at");
        entity.Property(e => e.NetRefundAmount).HasColumnType("numeric(14, 2)").HasColumnName("net_refund_amount");
        entity.Property(e => e.OverdueDeductionAmount).HasColumnType("numeric(14, 2)").HasDefaultValue(0m).HasColumnName("overdue_deduction_amount");
        entity.Property(e => e.Reason).HasMaxLength(255).HasColumnName("reason");
        entity.Property(e => e.RequestedBy).HasColumnName("requested_by");
        entity.Property(e => e.RequestedMoveOutDate).HasColumnName("requested_move_out_date");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("requested").HasColumnName("status");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("updated_at");

        entity.HasOne(d => d.Agreement).WithOne(p => p.MoveOutRequest)
            .HasForeignKey<MoveOutRequest>(d => d.AgreementId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__move_out___agree__6BAEFA67");

        entity.HasOne(d => d.Appointment).WithMany(p => p.MoveOutRequests)
            .HasForeignKey(d => d.AppointmentId)
            .HasConstraintName("FK__move_out___appoi__6D9742D9");

        entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.MoveOutRequests)
            .HasForeignKey(d => d.RequestedBy)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__move_out___reque__6CA31EA0");
    }
}
