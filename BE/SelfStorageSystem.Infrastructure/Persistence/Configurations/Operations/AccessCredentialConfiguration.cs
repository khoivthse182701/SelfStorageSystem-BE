using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Operations;

public sealed class AccessCredentialConfiguration : IEntityTypeConfiguration<AccessCredential>
{
    public void Configure(EntityTypeBuilder<AccessCredential> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__access_c__3213E83FDE7FE5B7");
        entity.ToTable("access_credentials", "core", tb =>
        {
            tb.HasTrigger("trg_access_credentials_validate_scope");
            tb.HasCheckConstraint("CK_access_credentials_type", EnumDbMapper.ToSqlCheckInClause<CredentialType>("credential_type"));
            tb.HasCheckConstraint("CK_access_credentials_status", EnumDbMapper.ToSqlCheckInClause<CredentialStatus>("status"));
            tb.HasCheckConstraint("CK_access_credentials_expiry", "[expires_at] IS NULL OR [expires_at] > [issued_at]");
            tb.HasCheckConstraint("CK_access_credentials_revoked", "([status] = 'revoked' AND [revoked_at] IS NOT NULL) OR [status] <> 'revoked'");
        });

        entity.HasIndex(e => new { e.AgreementId, e.Status }, "access_credentials_agreement_idx");
        entity.HasIndex(e => e.AuthorizedMemberId, "access_credentials_authorized_member_id_idx").HasFilter("([authorized_member_id] IS NOT NULL)");
        entity.HasIndex(e => e.IssuedBy, "access_credentials_issued_by_idx").HasFilter("([issued_by] IS NOT NULL)");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
        entity.Property(e => e.AuthorizedMemberId).HasColumnName("authorized_member_id");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.CredentialType).HasMaxLength(255).HasColumnName("credential_type");
        entity.Property(e => e.DisplayHint).HasMaxLength(255).HasColumnName("display_hint");
        entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
        entity.Property(e => e.ExternalSecretRef).HasMaxLength(255).HasColumnName("external_secret_ref");
        entity.Property(e => e.IssuedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("issued_at");
        entity.Property(e => e.IssuedBy).HasColumnName("issued_by");
        entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");
        entity.Property(e => e.SecretDigest).HasMaxLength(255).HasColumnName("secret_digest");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("pending").HasColumnName("status");

        entity.HasOne(d => d.Agreement).WithMany(p => p.AccessCredentials)
            .HasForeignKey(d => d.AgreementId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__access_cr__agree__442B18F2");

        entity.HasOne(d => d.AuthorizedMember).WithMany(p => p.AccessCredentials)
            .HasForeignKey(d => d.AuthorizedMemberId)
            .HasConstraintName("FK__access_cr__autho__451F3D2B");

        entity.HasOne(d => d.IssuedByNavigation).WithMany(p => p.AccessCredentials)
            .HasForeignKey(d => d.IssuedBy)
            .HasConstraintName("FK__access_cr__issue__49E3F248");
    }
}
