using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.CheckIn;

public sealed class IdentityVerificationConfiguration : IEntityTypeConfiguration<IdentityVerification>
{
    public void Configure(EntityTypeBuilder<IdentityVerification> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__identity__3213E83FFD6D658F");
        entity.ToTable("identity_verifications", "core", tb =>
        {
            tb.HasCheckConstraint("CK_identity_verifications_method", EnumDbMapper.ToSqlCheckInClause<IdentityVerificationMethod>("verification_method"));
            tb.HasCheckConstraint("CK_identity_verifications_result", EnumDbMapper.ToSqlCheckInClause<IdentityVerificationResult>("result"));
        });

        entity.HasIndex(e => new { e.ReservationId, e.VerifiedAt }, "identity_verifications_reservation_idx").IsDescending(false, true);
        entity.HasIndex(e => e.VerifiedBy, "identity_verifications_verified_by_idx");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.DocumentFingerprint).HasMaxLength(255).HasColumnName("document_fingerprint");
        entity.Property(e => e.Notes).HasMaxLength(255).HasColumnName("notes");
        entity.Property(e => e.ReservationId).HasColumnName("reservation_id");
        entity.Property(e => e.Result).HasMaxLength(255).HasColumnName("result");
        entity.Property(e => e.VerificationMethod).HasMaxLength(255).HasColumnName("verification_method");
        entity.Property(e => e.VerifiedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("verified_at");
        entity.Property(e => e.VerifiedBy).HasColumnName("verified_by");

        entity.HasOne(d => d.Reservation).WithMany(p => p.IdentityVerifications)
            .HasForeignKey(d => d.ReservationId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__identity___reser__2BC97F7C");

        entity.HasOne(d => d.VerifiedByNavigation).WithMany(p => p.IdentityVerifications)
            .HasForeignKey(d => d.VerifiedBy)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__identity___verif__2CBDA3B5");
    }
}
