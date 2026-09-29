namespace SelfStorageSystem.Contracts.Customer.Facilities;

public class PricingCalculationDto
{
    public decimal BaseMonthlyRate { get; set; }
    public int DurationMonths { get; set; }
    public decimal RentAmount { get; set; }
    public decimal SecurityDeposit { get; set; }
    public decimal BookingFee { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? AppliedVoucherCode { get; set; }
}
