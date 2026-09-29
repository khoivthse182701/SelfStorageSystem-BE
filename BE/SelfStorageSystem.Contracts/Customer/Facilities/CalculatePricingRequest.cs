namespace SelfStorageSystem.Contracts.Customer.Facilities;

public class CalculatePricingRequest
{
    public long FacilityId { get; set; }
    public long UnitTypeId { get; set; }
    public int DurationMonths { get; set; }
    public string? VoucherCode { get; set; }
}
