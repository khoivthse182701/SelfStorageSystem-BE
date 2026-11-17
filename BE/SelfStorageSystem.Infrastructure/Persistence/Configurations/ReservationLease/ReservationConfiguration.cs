using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.ReservationLease;

public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__reservat__3213E83FB121B783");
        entity.ToTable("reservations", "core", tb =>
        {
            tb.HasTrigger("trg_reservation_status_transition");
            tb.HasTrigger("trg_reservations_validate_rate");
            tb.HasCheckConstraint("CK_reservations_status", EnumDbMapper.ToSqlCheckInClause<ReservationStatus>("status"));
            tb.HasCheckConstraint("CK_reservations_dates", "[end_date] > [start_date]");
            tb.HasCheckConstraint("reservations_rental_term_ck", "[end_date] >= DATEADD(month, 1, [start_date]) AND [end_date] <= DATEADD(month, 12, [start_date])");
            tb.HasCheckConstraint("reservations_hold_max_15m_ck", "[hold_until] > [created_at] AND [hold_until] <= DATEADD(minute, 15, [created_at])");
            tb.HasCheckConstraint("CK_reservations_amounts", "[monthly_rate_snapshot] >= 0 AND [deposit_snapshot] >= 0 AND [booking_fee_snapshot] >= 0 AND [discount_snapshot] >= 0 AND [quoted_total] >= 0");
            tb.HasCheckConstraint("CK_reservations_cancelled_at", "([status] = 'cancelled' AND [cancelled_at] IS NOT NULL) OR [status] <> 'cancelled'");
        });

        entity.HasIndex(e => new { e.Id, e.CustomerId, e.FacilityId, e.UnitTypeId }, "UQ__reservat__6DF00CF31E5AFD72").IsUnique();
        entity.HasIndex(e => e.ReservationCode, "UQ__reservat__FA8FADE4BD1ADEE8").IsUnique();
        entity.HasIndex(e => new { e.CustomerId, e.CreatedAt }, "reservations_customer_created_idx").IsDescending(false, true);
        entity.HasIndex(e => e.FacilityRateId, "reservations_facility_rate_id_idx");
        entity.HasIndex(e => new { e.FacilityId, e.Status, e.StartDate }, "reservations_facility_status_start_idx");
        entity.HasIndex(e => e.HoldUntil, "reservations_open_hold_idx").HasFilter("([status] IN ('pending', 'awaiting_deposit'))");
        entity.HasIndex(e => e.UnitTypeId, "reservations_unit_type_id_idx");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.BookingFeeSnapshot).HasColumnType("numeric(14, 2)").HasColumnName("booking_fee_snapshot");
        entity.Property(e => e.CancellationReason).HasMaxLength(255).HasColumnName("cancellation_reason");
        entity.Property(e => e.CancelledAt).HasColumnName("cancelled_at");
        entity.Property(e => e.ConfirmedAt).HasColumnName("confirmed_at");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.CustomerId).HasColumnName("customer_id");
        entity.Property(e => e.DepositSnapshot).HasColumnType("numeric(14, 2)").HasColumnName("deposit_snapshot");
        entity.Property(e => e.DiscountSnapshot).HasColumnType("numeric(14, 2)").HasColumnName("discount_snapshot");
        entity.Property(e => e.EndDate).HasColumnName("end_date");
        entity.Property(e => e.FacilityId).HasColumnName("facility_id");
        entity.Property(e => e.FacilityRateId).HasColumnName("facility_rate_id");
        entity.Property(e => e.HoldUntil).HasColumnName("hold_until");
        entity.Property(e => e.MonthlyRateSnapshot).HasColumnType("numeric(14, 2)").HasColumnName("monthly_rate_snapshot");
        entity.Property(e => e.QuotedTotal).HasColumnType("numeric(14, 2)").HasColumnName("quoted_total");
        entity.Property(e => e.ReservationCode).HasMaxLength(255).HasColumnName("reservation_code");
        entity.Property(e => e.StartDate).HasColumnName("start_date");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("pending").HasColumnName("status");
        entity.Property(e => e.UnitTypeId).HasColumnName("unit_type_id");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("updated_at");

        entity.HasOne(d => d.Customer).WithMany(p => p.Reservations)
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__reservati__custo__6DCC4D03");

        entity.HasOne(d => d.Facility).WithMany(p => p.Reservations)
            .HasForeignKey(d => d.FacilityId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__reservati__facil__6EC0713C");

        entity.HasOne(d => d.FacilityRate).WithMany(p => p.Reservations)
            .HasForeignKey(d => d.FacilityRateId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__reservati__facil__70A8B9AE");

        entity.HasOne(d => d.UnitType).WithMany(p => p.Reservations)
            .HasForeignKey(d => d.UnitTypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__reservati__unit___6FB49575");
    }
}
