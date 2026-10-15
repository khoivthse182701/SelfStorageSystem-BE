using System.ComponentModel.DataAnnotations;
using SelfStorageSystem.Domain.Constants;

namespace SelfStorageSystem.Contracts.Operations;

public class OperationsPagedQueryParameters
{
    [Range(1, int.MaxValue)]
    public int PageNumber { get; set; } = OperationsConstants.DefaultPageNumber;

    [Range(1, OperationsConstants.MaxPageSize)]
    public int PageSize { get; set; } = OperationsConstants.DefaultPageSize;
}

public class OperationsRateQueryParameters : OperationsPagedQueryParameters
{
    [Range(1, long.MaxValue)]
    public long? FacilityId { get; set; }

    [Range(1, long.MaxValue)]
    public long? UnitTypeId { get; set; }

    public DateOnly? EffectiveOn { get; set; }
}

public class OperationsFeeQueryParameters : OperationsPagedQueryParameters, IValidatableObject
{
    [Range(1, long.MaxValue)]
    public long? FacilityId { get; set; }

    public bool GlobalOnly { get; set; }

    [MaxLength(OperationsConstants.MaxTextLength)]
    public string? Search { get; set; }

    [MaxLength(OperationsConstants.MaxTextLength)]
    public string? FeeType { get; set; }

    public bool? IsActive { get; set; }
    public DateOnly? EffectiveOn { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (GlobalOnly && FacilityId.HasValue)
            yield return new ValidationResult("FacilityId and GlobalOnly cannot be combined.", new[] { nameof(FacilityId), nameof(GlobalOnly) });
    }
}

public class OperationsPromotionQueryParameters : OperationsPagedQueryParameters
{
    [MaxLength(OperationsConstants.MaxTextLength)]
    public string? Search { get; set; }

    [RegularExpression("^(percentage|fixed)$")]
    public string? DiscountType { get; set; }

    public bool? IsActive { get; set; }
    public DateTimeOffset? EffectiveAt { get; set; }
}
