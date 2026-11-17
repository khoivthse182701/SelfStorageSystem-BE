using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.ReservationLease;

public sealed class AuthorizedAccessMemberConfiguration : IEntityTypeConfiguration<AuthorizedAccessMember>
{
    public void Configure(EntityTypeBuilder<AuthorizedAccessMember> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__authoriz__3213E83F51D983B0");
        entity.ToTable("authorized_access_members", "core", tb =>
        {
            tb.HasCheckConstraint("CK_authorized_access_members_status", EnumDbMapper.ToSqlCheckInClause<AuthorizedMemberStatus>("status"));
            tb.HasCheckConstraint("CK_authorized_access_members_dates", "[valid_to] IS NULL OR [valid_to] > [valid_from]");
        });

        entity.HasIndex(e => new { e.AgreementId, e.Status }, "authorized_access_members_agreement_idx");
        entity.HasIndex(e => e.CreatedBy, "authorized_access_members_created_by_idx");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.CreatedBy).HasColumnName("created_by");
        entity.Property(e => e.FullName).HasMaxLength(255).HasColumnName("full_name");
        entity.Property(e => e.IdentityFingerprint).HasMaxLength(255).HasColumnName("identity_fingerprint");
        entity.Property(e => e.RelationshipToCustomer).HasMaxLength(255).HasColumnName("relationship_to_customer");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("active").HasColumnName("status");
        entity.Property(e => e.ValidFrom).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("valid_from");
        entity.Property(e => e.ValidTo).HasColumnName("valid_to");

        entity.HasOne(d => d.Agreement).WithMany(p => p.AuthorizedAccessMembers)
            .HasForeignKey(d => d.AgreementId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__authorize__agree__4D2A7347");

        entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.AuthorizedAccessMembers)
            .HasForeignKey(d => d.CreatedBy)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__authorize__creat__50FB042B");
    }
}
