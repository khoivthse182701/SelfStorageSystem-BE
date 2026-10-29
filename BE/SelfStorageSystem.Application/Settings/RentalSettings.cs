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

    public int TransferUnpaidHoldMinutes { get; set; } = RentalPolicyConstants.DefaultTransferUnpaidHoldMinutes;
    public int TransferExpiryCheckIntervalMinutes { get; set; } = RentalPolicyConstants.DefaultTransferExpiryCheckIntervalMinutes;
    public decimal TransferOverduePenaltyRate { get; set; } = RentalPolicyConstants.DefaultTransferOverduePenaltyRate;
    public int TransferRescheduleCutoffHours { get; set; } = RentalPolicyConstants.DefaultTransferRescheduleCutoffHours;
    public decimal DowngradeVoucherDiscountPercent { get; set; } = RentalPolicyConstants.DefaultDowngradeVoucherDiscountPercent;
    public decimal DowngradeVoucherMaxAmount { get; set; } = RentalPolicyConstants.DefaultDowngradeVoucherMaxAmount;
    public int DowngradeVoucherValidityDays { get; set; } = RentalPolicyConstants.DefaultDowngradeVoucherValidityDays;
    public int RenewalHoldMinutes { get; set; } = RentalPolicyConstants.DefaultRenewalHoldMinutes;
    public int CheckoutExpirySeconds { get; set; } = RentalPolicyConstants.DefaultCheckoutExpirySeconds;

    /// <summary>
    /// When true, rejects access credential requests outside facility business hours (07:00 - 21:00).
    /// Default is false to allow 24/7 credential retrieval and smooth testing at night.
    /// </summary>
    public bool EnforceOperatingHours { get; set; } = false;
}
