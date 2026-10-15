using System.ComponentModel.DataAnnotations;
using System.Globalization;
using SelfStorageSystem.Domain.Constants;

namespace SelfStorageSystem.Contracts.Operations;

public class OperationsPromotionDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public int? UsageLimit { get; set; }
    public int? PerCustomerLimit { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidTo { get; set; }
    public bool IsActive { get; set; }
    public long? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<OperationsPromotionRuleDto> Rules { get; set; } = new();
}

public class OperationsPromotionRuleDto
{
    public long Id { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string RuleValue { get; set; } = string.Empty;
}

public class OperationsCreatePromotionRequest : IValidatableObject
{
    [Required, MaxLength(OperationsConstants.MaxTextLength)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(OperationsConstants.MaxTextLength)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(OperationsConstants.MaxTextLength)]
    public string? Description { get; set; }

    [Required, RegularExpression("^(percentage|fixed)$")]
    public string DiscountType { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", OperationsConstants.MaxMoneyText)]
    public decimal DiscountValue { get; set; }

    [Range(typeof(decimal), "0.01", OperationsConstants.MaxMoneyText)]
    public decimal? MaxDiscountAmount { get; set; }

    [Range(1, int.MaxValue)]
    public int? UsageLimit { get; set; }

    [Range(1, int.MaxValue)]
    public int? PerCustomerLimit { get; set; }

    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidTo { get; set; }
    public bool IsActive { get; set; } = true;

    [MaxLength(3)]
    public List<OperationsCreatePromotionRuleRequest> Rules { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ValidFrom == default)
            yield return new ValidationResult("ValidFrom is required.", new[] { nameof(ValidFrom) });
        if (ValidTo <= ValidFrom)
            yield return new ValidationResult("ValidTo must be later than ValidFrom.", new[] { nameof(ValidTo) });
        if (DiscountType == PromotionDiscountTypeConstants.Percentage && DiscountValue > 100)
            yield return new ValidationResult("Percentage discounts cannot exceed 100.", new[] { nameof(DiscountValue) });
        if (decimal.Round(DiscountValue, 2) != DiscountValue)
            yield return new ValidationResult("DiscountValue supports at most two decimal places.", new[] { nameof(DiscountValue) });
        if (MaxDiscountAmount.HasValue && decimal.Round(MaxDiscountAmount.Value, 2) != MaxDiscountAmount.Value)
            yield return new ValidationResult("MaxDiscountAmount supports at most two decimal places.", new[] { nameof(MaxDiscountAmount) });
        if (UsageLimit.HasValue && PerCustomerLimit.HasValue && PerCustomerLimit > UsageLimit)
            yield return new ValidationResult("PerCustomerLimit cannot exceed UsageLimit.", new[] { nameof(PerCustomerLimit) });
        if (Rules is null)
        {
            yield return new ValidationResult("Rules cannot be null; omit the property or provide an empty array.", new[] { nameof(Rules) });
        }
        else if (Rules.Any(rule => rule is null) || Rules.GroupBy(rule => rule.RuleType, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            yield return new ValidationResult("Provide at most one non-null rule of each type.", new[] { nameof(Rules) });
        }
    }
}

public class OperationsCreatePromotionRuleRequest : IValidatableObject
{
    [Required, RegularExpression("^(facility|unit_type|minimum_months)$")]
    public string RuleType { get; set; } = string.Empty;

    [Required, RegularExpression("^(eq|gte)$")]
    public string Operator { get; set; } = OperationsConstants.EqualOperator;

    [Required, MaxLength(OperationsConstants.MaxTextLength)]
    public string RuleValue { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var isMinimumMonths = RuleType == OperationsConstants.MinMonthsRuleType;
        if (isMinimumMonths && Operator != OperationsConstants.GreaterThanOrEqualOperator)
            yield return new ValidationResult("minimum_months rules require the gte operator.", new[] { nameof(Operator) });
        if ((RuleType == PromotionRuleTypeConstants.Facility || RuleType == PromotionRuleTypeConstants.UnitType) && Operator != OperationsConstants.EqualOperator)
            yield return new ValidationResult("Facility and unit_type rules require the eq operator.", new[] { nameof(Operator) });
        if (!long.TryParse(RuleValue, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 1 || (isMinimumMonths && value > 12))
            yield return new ValidationResult("RuleValue must be a positive integer within the supported range.", new[] { nameof(RuleValue) });
    }
}

public class OperationsUpdatePromotionStatusRequest
{
    [Required]
    public bool? IsActive { get; set; }
}
