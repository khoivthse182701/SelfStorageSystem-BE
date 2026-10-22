using System.ComponentModel.DataAnnotations;
using SelfStorageSystem.Domain.Constants;

namespace SelfStorageSystem.Contracts.Admin;

public class AdminFacilityQueryParameters
{
    /// <summary>Tìm theo mã, tên hoặc địa chỉ cơ sở.</summary>
    public string? Search { get; set; }

    /// <summary>active | inactive | maintenance</summary>
    public string? Status { get; set; }

    public string? City { get; set; }

    public int PageNumber { get; set; } = AdminConstants.DefaultPageNumber;
    public int PageSize { get; set; } = AdminConstants.DefaultPageSize;
}

public class AdminFacilitySummaryDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string? District { get; set; }
    public string City { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public TimeOnly? OpeningTime { get; set; }
    public TimeOnly? ClosingTime { get; set; }
    public int TotalUnits { get; set; }
    public int ActiveStaffCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class AdminFacilityUnitStatsDto
{
    public int TotalUnits { get; set; }
    public int Available { get; set; }
    public int Reserved { get; set; }
    public int Occupied { get; set; }
    public int Maintenance { get; set; }
    public double OccupancyRate { get; set; }
}

public class AdminFacilityDetailDto
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
    public AdminFacilityUnitStatsDto UnitStats { get; set; } = new();
    public IReadOnlyList<AdminStaffFacilityAssignmentDto> AssignedStaff { get; set; } = Array.Empty<AdminStaffFacilityAssignmentDto>();
}

public class AdminCreateFacilityRequest : IValidatableObject
{
    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string AddressLine { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Ward { get; set; }

    [MaxLength(100)]
    public string? District { get; set; }

    [Required, MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Range(typeof(decimal), "-90", "90")]
    public decimal? Latitude { get; set; }

    [Range(typeof(decimal), "-180", "180")]
    public decimal? Longitude { get; set; }

    [Required, MaxLength(100)]
    public string Timezone { get; set; } = TimezoneConstants.DefaultVietnam;

    public TimeOnly? OpeningTime { get; set; }
    public TimeOnly? ClosingTime { get; set; }

    [Required]
    public string Status { get; set; } = FacilityStatusConstants.Active;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Latitude.HasValue != Longitude.HasValue)
            yield return new ValidationResult("Provide both Latitude and Longitude, or omit both.", new[] { nameof(Latitude), nameof(Longitude) });
        if ((Latitude.HasValue && decimal.Round(Latitude.Value, 6) != Latitude.Value) ||
            (Longitude.HasValue && decimal.Round(Longitude.Value, 6) != Longitude.Value))
            yield return new ValidationResult("Coordinates support at most six decimal places.", new[] { nameof(Latitude), nameof(Longitude) });
        if (OpeningTime.HasValue != ClosingTime.HasValue)
            yield return new ValidationResult("Provide both OpeningTime and ClosingTime, or omit both.", new[] { nameof(OpeningTime), nameof(ClosingTime) });
        if (OpeningTime.HasValue && ClosingTime.HasValue && OpeningTime.Value == ClosingTime.Value)
            yield return new ValidationResult("OpeningTime and ClosingTime must differ; omit both for unrestricted hours.", new[] { nameof(OpeningTime), nameof(ClosingTime) });
        if (!string.IsNullOrWhiteSpace(Timezone) && !AdminFacilityValidation.IsValidTimezone(Timezone))
            yield return new ValidationResult("Timezone must be a valid time zone identifier.", new[] { nameof(Timezone) });
        if (!string.IsNullOrWhiteSpace(Status) && !AdminFacilityValidation.IsValidStatus(Status))
            yield return new ValidationResult("Status must be one of: active, inactive, maintenance.", new[] { nameof(Status) });
    }
}

public class AdminUpdateFacilityRequest : AdminCreateFacilityRequest
{
}

public class AdminUpdateFacilityStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Reason { get; set; }
}

public static class AdminFacilityValidation
{
    public static readonly string[] ValidStatuses =
    {
        FacilityStatusConstants.Active,
        FacilityStatusConstants.Inactive,
        FacilityStatusConstants.Maintenance
    };

    public static bool IsValidStatus(string value) =>
        ValidStatuses.Contains(value.Trim().ToLowerInvariant());

    public static bool IsValidTimezone(string value)
    {
        try { TimeZoneInfo.FindSystemTimeZoneById(value.Trim()); return true; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }
}
