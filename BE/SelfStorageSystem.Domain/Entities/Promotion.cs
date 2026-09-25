using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class Promotion
{
    public long Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string DiscountType { get; set; } = null!;

    public decimal DiscountValue { get; set; }

    public decimal? MaxDiscountAmount { get; set; }

    public int? UsageLimit { get; set; }

    public int? PerCustomerLimit { get; set; }

    public DateTimeOffset ValidFrom { get; set; }

    public DateTimeOffset ValidTo { get; set; }

    public bool IsActive { get; set; }

    public long? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual User? CreatedByNavigation { get; set; }

    public virtual ICollection<PromotionRedemption> PromotionRedemptions { get; set; } = new List<PromotionRedemption>();

    public virtual ICollection<PromotionRule> PromotionRules { get; set; } = new List<PromotionRule>();
}
