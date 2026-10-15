using System.ComponentModel.DataAnnotations;
using SelfStorageSystem.Domain.Constants;

namespace SelfStorageSystem.Contracts.Operations;

public class OperationsFacilityDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string? Ward { get; set; }
    public string? District { get; set; }
    public string City { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string Timezone { get; set; } = string.Empty;
    public TimeOnly? OpeningTime { get; set; }
    public TimeOnly? ClosingTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class OperationsCreateFacilityRequest : IValidatableObject
{
    [Required, MaxLength(OperationsConstants.MaxTextLength)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(OperationsConstants.MaxTextLength)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(OperationsConstants.MaxTextLength)]
    public string AddressLine { get; set; } = string.Empty;

    [MaxLength(OperationsConstants.MaxTextLength)]
    public string? Ward { get; set; }

    [MaxLength(OperationsConstants.MaxTextLength)]
    public string? District { get; set; }

    [Required, MaxLength(OperationsConstants.MaxTextLength)]
    public string City { get; set; } = string.Empty;

    [Range(typeof(decimal), "-90", "90")]
    public decimal? Latitude { get; set; }

    [Range(typeof(decimal), "-180", "180")]
    public decimal? Longitude { get; set; }

    [Required, MaxLength(OperationsConstants.MaxTextLength)]
    public string Timezone { get; set; } = TimezoneConstants.DefaultVietnam;

    public TimeOnly? OpeningTime { get; set; }
    public TimeOnly? ClosingTime { get; set; }

    [Required, RegularExpression("^(active|inactive|maintenance)$")]
    public string Status { get; set; } = FacilityStatusConstants.Active;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Latitude.HasValue != Longitude.HasValue)
            yield return new ValidationResult("Provide both Latitude and Longitude, or omit both.", new[] { nameof(Latitude), nameof(Longitude) });
        if ((Latitude.HasValue && decimal.Round(Latitude.Value, 6) != Latitude.Value) || (Longitude.HasValue && decimal.Round(Longitude.Value, 6) != Longitude.Value))
            yield return new ValidationResult("Coordinates support at most six decimal places.", new[] { nameof(Latitude), nameof(Longitude) });
        if (OpeningTime.HasValue != ClosingTime.HasValue)
            yield return new ValidationResult("Provide both OpeningTime and ClosingTime, or omit both.", new[] { nameof(OpeningTime), nameof(ClosingTime) });
        if (OpeningTime.HasValue && ClosingTime.HasValue && OpeningTime.Value == ClosingTime.Value)
            yield return new ValidationResult("OpeningTime and ClosingTime must differ; omit both for unrestricted hours.", new[] { nameof(OpeningTime), nameof(ClosingTime) });
        if (!string.IsNullOrWhiteSpace(Timezone) && !IsValidTimezone(Timezone))
            yield return new ValidationResult("Timezone must be a valid time zone identifier.", new[] { nameof(Timezone) });
    }

    private static bool IsValidTimezone(string value)
    {
        try { TimeZoneInfo.FindSystemTimeZoneById(value); return true; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }
}

public class OperationsUpdateFacilityRequest : OperationsCreateFacilityRequest
{
    [Range(1, long.MaxValue)]
    public long Id { get; set; }
}
