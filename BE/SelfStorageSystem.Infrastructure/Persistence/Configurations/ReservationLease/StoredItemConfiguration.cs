using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.ReservationLease;

public sealed class StoredItemConfiguration : IEntityTypeConfiguration<StoredItem>
{
    public void Configure(EntityTypeBuilder<StoredItem> entity)
    {
        entity.HasKey(e => e.Id);
        entity.ToTable("stored_items", "core", tb =>
        {
            tb.HasCheckConstraint("CK_stored_items_quantity", "[quantity] > 0");
            tb.HasCheckConstraint("CK_stored_items_estimated_value", "[estimated_value] IS NULL OR [estimated_value] >= 0");
        });

        entity.HasIndex(e => new { e.AgreementId, e.Category }, "stored_items_agreement_id_idx");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
        entity.Property(e => e.ItemName).HasMaxLength(255).HasColumnName("item_name");
        entity.Property(e => e.Category).HasMaxLength(100).IsRequired(false).HasColumnName("category");
        entity.Property(e => e.Description).HasColumnName("description");
        entity.Property(e => e.Quantity).HasDefaultValue(1).HasColumnName("quantity");
        entity.Property(e => e.EstimatedValue).HasColumnType("numeric(14, 2)").HasColumnName("estimated_value");
        entity.Property(e => e.RiskClassification).HasMaxLength(50).HasDefaultValue("standard").HasColumnName("risk_classification");
        entity.Property(e => e.PhotoUrl).HasMaxLength(500).HasColumnName("photo_url");
        entity.Property(e => e.LocationNote).HasMaxLength(255).HasColumnName("location_note");
        entity.Property(e => e.DeclaredAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("declared_at");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("updated_at");

        entity.HasOne(d => d.RentalAgreement)
            .WithMany(p => p.StoredItems)
            .HasForeignKey(d => d.AgreementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
