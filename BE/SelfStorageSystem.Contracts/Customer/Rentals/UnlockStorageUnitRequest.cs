using System.ComponentModel.DataAnnotations;

namespace SelfStorageSystem.Contracts.Customer.Rentals;

public class UnlockStorageUnitRequest
{
    [Required(ErrorMessage = "PIN is required.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "PIN must be exactly 6 numeric digits.")]
    public string Pin { get; set; } = null!;
}
