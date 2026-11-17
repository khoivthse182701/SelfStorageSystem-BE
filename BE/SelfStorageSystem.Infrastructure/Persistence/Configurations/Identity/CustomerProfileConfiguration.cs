using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Identity;

public sealed class CustomerProfileConfiguration : IEntityTypeConfiguration<CustomerProfile>
{
    public void Configure(EntityTypeBuilder<CustomerProfile> entity)
    {
        entity.HasKey(e => e.UserId).HasName("PK__customer__B9BE370F6AA710AD");
        entity.ToTable("customer_profiles", "core", tb =>
        {
            tb.HasCheckConstraint("CK_customer_profiles_edit_count", "[profile_edit_count] >= 0 AND [profile_edit_count] <= 2");
        });

        entity.HasIndex(e => e.IdentityNumber, "customer_profiles_identity_number_uidx")
            .IsUnique()
            .HasFilter("([identity_number] IS NOT NULL)");

        entity.Property(e => e.UserId).ValueGeneratedNever().HasColumnName("user_id");
        entity.Property(e => e.Address).HasMaxLength(255).HasColumnName("address");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.DateOfBirth).HasColumnName("date_of_birth");
        entity.Property(e => e.EmergencyContactName).HasMaxLength(255).HasColumnName("emergency_contact_name");
        entity.Property(e => e.EmergencyContactPhone).HasMaxLength(255).HasColumnName("emergency_contact_phone");
        entity.Property(e => e.FullName).HasMaxLength(255).HasColumnName("full_name");
        entity.Property(e => e.IdentityNumber).HasMaxLength(255).HasColumnName("identity_number");
        entity.Property(e => e.ProfileEditMonth).HasMaxLength(7).IsUnicode(false).IsFixedLength().HasColumnName("profile_edit_month");
        entity.Property(e => e.ProfileEditCount).HasDefaultValue(0).HasColumnName("profile_edit_count");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("updated_at");

        entity.HasOne(d => d.User).WithOne(p => p.CustomerProfile)
            .HasForeignKey<CustomerProfile>(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__customer___user___60A75C0F");
    }
}
