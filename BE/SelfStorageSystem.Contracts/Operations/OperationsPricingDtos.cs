using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using SelfStorageSystem.Domain.Constants;

namespace SelfStorageSystem.Contracts.Operations;

public class OperationsRateDto
{
    public long Id { get; set; }
    public long FacilityId { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public long UnitTypeId { get; set; }
    public string UnitTypeName { get; set; } = string.Empty;
    public decimal MonthlyRate { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal BookingFee { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public long? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class OperationsUpsertRateRequest : IValidatableObject
{
    [Range(OperationsConstants.MinEntityId, long.MaxValue)]
    public long? Id { get; set; }

    [Range(OperationsConstants.MinEntityId, long.MaxValue)]
    public long FacilityId { get; set; }

    [Range(OperationsConstants.MinEntityId, long.MaxValue)]
    public long UnitTypeId { get; set; }

    [Range(typeof(decimal), OperationsConstants.MinMoneyText, OperationsConstants.MaxMoneyText, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal MonthlyRate { get; set; }

    /// <summary>Defaults to MonthlyRate when omitted, in accordance with BR-FIN-01.</summary>
    [Range(typeof(decimal), OperationsConstants.MinMoneyText, OperationsConstants.MaxMoneyText, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? DepositAmount { get; set; }

    [Range(typeof(decimal), OperationsConstants.MinMoneyText, OperationsConstants.MaxMoneyText, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal BookingFee { get; set; }

    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ValidFrom == default)
            yield return new ValidationResult("ValidFrom is required.", new[] { nameof(ValidFrom) });
        if (ValidTo.HasValue && ValidTo.Value <= ValidFrom)
            yield return new ValidationResult("ValidTo must be later than ValidFrom.", new[] { nameof(ValidTo) });
        if (decimal.Round(MonthlyRate, OperationsConstants.MoneyDecimalPlaces) != MonthlyRate)
            yield return new ValidationResult("MonthlyRate supports at most two decimal places.", new[] { nameof(MonthlyRate) });
        if (DepositAmount.HasValue && decimal.Round(DepositAmount.Value, OperationsConstants.MoneyDecimalPlaces) != DepositAmount.Value)
            yield return new ValidationResult("DepositAmount supports at most two decimal places.", new[] { nameof(DepositAmount) });
        if (decimal.Round(BookingFee, OperationsConstants.MoneyDecimalPlaces) != BookingFee)
            yield return new ValidationResult("BookingFee supports at most two decimal places.", new[] { nameof(BookingFee) });
    }
}

public class OperationsFeeDto
{
    public long Id { get; set; }
    public long? FacilityId { get; set; }
    public string? FacilityName { get; set; }
    public string Code { get; set; } = string.Empty;
    public string FeeType { get; set; } = string.Empty;
    public string CalculationMethod { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public decimal? RatePercent { get; set; }
    public int GraceDays { get; set; }
    public string Conditions { get; set; } = OperationsConstants.DefaultConditions;
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public bool IsActive { get; set; }
    public long? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class OperationsUpsertFeeRequest : IValidatableObject
{
    [Range(OperationsConstants.MinEntityId, long.MaxValue)]
    public long? Id { get; set; }

    /// <summary>Null applies the rule globally.</summary>
    [Range(OperationsConstants.MinEntityId, long.MaxValue)]
    public long? FacilityId { get; set; }

    [Required, MaxLength(OperationsConstants.MaxTextLength)]
    public string Code { get; set; } = string.Empty;

    [Required, RegularExpression(OperationsConstants.FeeTypeRegex)]
    public string FeeType { get; set; } = string.Empty;

    [Required, RegularExpression(OperationsConstants.CalculationMethodRegex)]
    public string CalculationMethod { get; set; } = string.Empty;

    [Range(typeof(decimal), OperationsConstants.MinMoneyText, OperationsConstants.MaxMoneyText, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? Amount { get; set; }

    [Range(typeof(decimal), OperationsConstants.MinRatePercentText, OperationsConstants.MaxRatePercentText, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? RatePercent { get; set; }

    [Range(OperationsConstants.MinGraceDays, int.MaxValue)]
    public int GraceDays { get; set; }

    [Required]
    public string Conditions { get; set; } = OperationsConstants.DefaultConditions;

    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ValidFrom == default)
            yield return new ValidationResult("ValidFrom is required.", new[] { nameof(ValidFrom) });
        if (ValidTo.HasValue && ValidTo.Value <= ValidFrom)
            yield return new ValidationResult("ValidTo must be later than ValidFrom.", new[] { nameof(ValidTo) });
        if (CalculationMethod == OperationsConstants.PercentageCalculationMethod && !RatePercent.HasValue)
            yield return new ValidationResult("RatePercent is required for percentage rules.", new[] { nameof(RatePercent) });
        if ((CalculationMethod == OperationsConstants.FlatCalculationMethod || CalculationMethod == OperationsConstants.PerDayCalculationMethod) && !Amount.HasValue)
            yield return new ValidationResult("Amount is required for flat and per_day rules.", new[] { nameof(Amount) });
        if (Amount.HasValue && decimal.Round(Amount.Value, OperationsConstants.MoneyDecimalPlaces) != Amount.Value)
            yield return new ValidationResult("Amount supports at most two decimal places.", new[] { nameof(Amount) });
        if (RatePercent.HasValue && decimal.Round(RatePercent.Value, OperationsConstants.RatePercentDecimalPlaces) != RatePercent.Value)
            yield return new ValidationResult("RatePercent supports at most four decimal places.", new[] { nameof(RatePercent) });
        if (!IsJsonObject(Conditions))
            yield return new ValidationResult("Conditions must be a valid JSON object.", new[] { nameof(Conditions) });
    }

    private static bool IsJsonObject(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException) { return false; }
    }
}
