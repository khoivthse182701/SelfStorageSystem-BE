using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Identity;

public sealed class EmployeeProfileConfiguration : IEntityTypeConfiguration<EmployeeProfile>
{
    public void Configure(EntityTypeBuilder<EmployeeProfile> entity)
    {
        entity.HasKey(e => e.UserId).HasName("PK__employee__B9BE370FE5C68310");
        entity.ToTable("employee_profiles", "core", tb =>
            tb.HasCheckConstraint("CK_employee_profiles_employment_status", EnumDbMapper.ToSqlCheckInClause<EmploymentStatus>("employment_status")));

        entity.HasIndex(e => e.EmployeeCode, "UQ__employee__B0AA73450E145A67").IsUnique();

        entity.Property(e => e.UserId).ValueGeneratedNever().HasColumnName("user_id");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.EmployeeCode).HasMaxLength(255).HasColumnName("employee_code");
        entity.Property(e => e.EmploymentStatus).HasMaxLength(255).HasDefaultValue("active").HasColumnName("employment_status");
        entity.Property(e => e.FullName).HasMaxLength(255).HasColumnName("full_name");
        entity.Property(e => e.HireDate).HasColumnName("hire_date");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("updated_at");

        entity.HasOne(d => d.User).WithOne(p => p.EmployeeProfile)
            .HasForeignKey<EmployeeProfile>(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__employee___user___66603565");
    }
}
