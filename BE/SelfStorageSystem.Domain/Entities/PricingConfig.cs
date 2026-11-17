using System.ComponentModel.DataAnnotations.Schema;

namespace SelfStorageSystem.Domain.Entities;

[Table("pricing_configs", Schema = "core")]
public class PricingConfig
{
    [Column("id")]
    public long Id { get; set; }

    [Column("price_per_square_meter")]
    public decimal PricePerSquareMeter { get; set; } = 100m;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

[Table("storage_categories", Schema = "core")]
public class StorageCategory
{
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("surcharge_amount")]
    public decimal SurchargeAmount { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
