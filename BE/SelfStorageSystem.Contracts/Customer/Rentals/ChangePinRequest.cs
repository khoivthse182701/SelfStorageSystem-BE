using System.ComponentModel.DataAnnotations;

namespace SelfStorageSystem.Contracts.Customer.Rentals;

public class ChangePinRequest
{
    public string? CurrentPin { get; set; }

    [Required]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "PIN must be exactly 6 numeric digits.")]
    public string NewPin { get; set; } = null!;
}
