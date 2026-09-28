namespace SelfStorageSystem.Application.Settings;

public class ReservationSettings
{
    public const string SectionName = "ReservationSettings";

    /// <summary>
    /// Temporary unit reservation hold duration in minutes (Default: 15 minutes as per BR-RSV-01).
    /// </summary>
    public int HoldDurationMinutes { get; set; } = 15;

    /// <summary>
    /// Minimum rental duration in months (BR-RSV-02).
    /// </summary>
    public int MinDurationMonths { get; set; } = 1;

    /// <summary>
    /// Maximum rental duration in months per reservation (BR-RSV-02).
    /// </summary>
    public int MaxDurationMonths { get; set; } = 12;

    /// <summary>
    /// Buffer grace period in minutes to prevent race conditions during late payments around the 15-minute mark.
    /// </summary>
    public int HoldGracePeriodMinutes { get; set; } = 3;

    /// <summary>
    /// Background worker execution interval in seconds for cleaning expired reservations.
    /// </summary>
    public int ExpiryCheckIntervalSeconds { get; set; } = 30;
}
