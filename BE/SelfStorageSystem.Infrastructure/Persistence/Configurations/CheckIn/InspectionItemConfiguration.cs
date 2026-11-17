using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.CheckIn;

public sealed class InspectionItemConfiguration : IEntityTypeConfiguration<InspectionItem>
{
    public void Configure(EntityTypeBuilder<InspectionItem> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__inspecti__3213E83F1C247A95");
        entity.ToTable("inspection_items", "core", tb =>
        {
            tb.HasCheckConstraint("CK_inspection_items_condition", EnumDbMapper.ToSqlCheckInClause<InspectionItemCondition>("condition"));
            tb.HasCheckConstraint("CK_inspection_items_charge", "[charge_amount] >= 0");
        });

        entity.HasIndex(e => e.InspectionId, "inspection_items_inspection_id_idx");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.ChargeAmount).HasColumnType("numeric(14, 2)").HasColumnName("charge_amount");
        entity.Property(e => e.Condition).HasMaxLength(255).HasColumnName("condition");
        entity.Property(e => e.InspectionId).HasColumnName("inspection_id");
        entity.Property(e => e.ItemName).HasMaxLength(255).HasColumnName("item_name");
        entity.Property(e => e.Notes).HasMaxLength(255).HasColumnName("notes");
        entity.Property(e => e.PhotoUrl).HasMaxLength(255).HasColumnName("photo_url");

        entity.HasOne(d => d.Inspection).WithMany(p => p.InspectionItems)
            .HasForeignKey(d => d.InspectionId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__inspectio__inspe__3FD07829");
    }
}
