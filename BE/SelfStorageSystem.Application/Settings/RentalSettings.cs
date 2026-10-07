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
}
