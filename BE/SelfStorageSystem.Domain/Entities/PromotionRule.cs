using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class PromotionRule
{
    public long Id { get; set; }

    public long PromotionId { get; set; }

    public string RuleType { get; set; } = null!;

    public string Operator { get; set; } = null!;

    public string RuleValue { get; set; } = null!;

    public virtual Promotion Promotion { get; set; } = null!;
}
