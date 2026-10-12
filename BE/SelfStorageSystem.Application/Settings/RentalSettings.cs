using SelfStorageSystem.Domain.Constants;

namespace SelfStorageSystem.Application.Settings;

public class RentalSettings
{
    public const string SectionName = "RentalSettings";

    public int MaxFailedPinAttempts { get; set; } = RentalPolicyConstants.DefaultMaxFailedPinAttempts;
    public int PinLockoutDurationMinutes { get; set; } = RentalPolicyConstants.DefaultPinLockoutDurationMinutes;
    public int QrTtlSeconds { get; set; } = RentalPolicyConstants.DefaultQrTtlSeconds;
    public int ClockSkewLeewaySeconds { get; set; } = RentalPolicyConstants.DefaultClockSkewLeewaySeconds;
    public int EstimatedPinSyncSeconds { get; set; } = RentalPolicyConstants.DefaultEstimatedPinSyncSeconds;
    public int MinRenewalMonths { get; set; } = RentalPolicyConstants.MinRenewalMonths;
    public int MaxRenewalMonths { get; set; } = RentalPolicyConstants.MaxRenewalMonths;
    public string DefaultTimezone { get; set; } = RentalDefaults.DefaultTimezone;
    public string DefaultRefundPreviewNote { get; set; } = RentalDefaults.DefaultRefundPreviewNote;

    /// <summary>
    /// When true, rejects access credential requests outside facility business hours (07:00 - 21:00).
    /// Default is false to allow 24/7 credential retrieval and smooth testing at night.
    /// </summary>
    public bool EnforceOperatingHours { get; set; } = false;
}
