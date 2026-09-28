using System.ComponentModel.DataAnnotations;

namespace SelfStorageSystem.Contracts.Customer.Reservations;

public class CreateReservationRequest
{
    [Required(ErrorMessage = "Facility ID (facilityId) is required.")]
    public long FacilityId { get; set; }

    [Required(ErrorMessage = "Unit Type ID (unitTypeId) is required.")]
    public long UnitTypeId { get; set; }

    /// <summary>
    /// Specific storage unit ID selected from interactive map (optional).
    /// </summary>
    public long? StorageUnitId { get; set; }

    [Required(ErrorMessage = "Start date (startDate) is required.")]
    public DateOnly StartDate { get; set; }

    [Required(ErrorMessage = "Rental duration in months (durationMonths) is required.")]
    [Range(1, 12, ErrorMessage = "Rental duration must be between 1 and 12 months as per BR-RSV-02.")]
    public int DurationMonths { get; set; }

    /// <summary>
    /// Discount voucher or promotion code (optional - BR-FIN-04).
    /// </summary>
    public string? PromotionCode { get; set; }
}

public class CancelReservationRequest
{
    [MaxLength(255, ErrorMessage = "Cancellation reason cannot exceed 255 characters.")]
    public string? Reason { get; set; }
}
