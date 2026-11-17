namespace SelfStorageSystem.Contracts.Customer.Facilities;

public class PricingQuoteRequest
{
    public long FacilityId { get; set; }
    public long UnitTypeId { get; set; }
    public DateOnly StartDate { get; set; }
    public int DurationMonths { get; set; }
    public string? PromotionCode { get; set; }
}

public class PricingQuoteDto
{
    public decimal BaseMonthlyRate { get; set; }
    public int DurationMonths { get; set; }
    public decimal RentSubtotal { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal BookingFee { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal VatRatePercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalContractAmount { get; set; }
    public decimal FirstPaymentAmount { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public string? AppliedPromotionCode { get; set; }
    public long? AppliedPromotionId { get; set; }
}
