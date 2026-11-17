using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Pricing;

public sealed class PolicyVersionConfiguration : IEntityTypeConfiguration<PolicyVersion>
{
    public void Configure(EntityTypeBuilder<PolicyVersion> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__policy_v__3213E83FC5850671");
        entity.ToTable("policy_versions", "core", tb =>
        {
            tb.HasTrigger("trg_policy_version_no_overlap");
            tb.HasCheckConstraint("CK_policy_versions_policy_type", EnumDbMapper.ToSqlCheckInClause<PolicyType>("policy_type"));
            tb.HasCheckConstraint("CK_policy_versions_content_json", "ISJSON([content]) = 1");
            tb.HasCheckConstraint("CK_policy_versions_dates", "[valid_to] IS NULL OR [valid_to] > [valid_from]");
        });

        entity.HasIndex(e => new { e.PolicyType, e.Version }, "UQ__policy_v__913F32FAF5C65EB9").IsUnique();
        entity.HasIndex(e => e.CreatedBy, "policy_versions_created_by_idx").HasFilter("([created_by] IS NOT NULL)");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.Content).HasColumnName("content");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.CreatedBy).HasColumnName("created_by");
        entity.Property(e => e.PolicyType).HasMaxLength(255).HasColumnName("policy_type");
        entity.Property(e => e.ValidFrom).HasColumnName("valid_from");
        entity.Property(e => e.ValidTo).HasColumnName("valid_to");
        entity.Property(e => e.Version).HasMaxLength(255).HasColumnName("version");

        entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PolicyVersions)
            .HasForeignKey(d => d.CreatedBy)
            .HasConstraintName("FK__policy_ve__creat__44CA3770");
    }
}
