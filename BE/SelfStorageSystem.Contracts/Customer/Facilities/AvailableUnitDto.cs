namespace SelfStorageSystem.Contracts.Customer.Facilities;

public class AvailableUnitDto
{
    public long Id { get; set; }
    public long FacilityId { get; set; }
    public long UnitTypeId { get; set; }
    public long? FacilityAreaId { get; set; }
    public string UnitCode { get; set; } = string.Empty;
    public string? FloorLabel { get; set; }
    public string? ZoneLabel { get; set; }
    public decimal MonthlyRate { get; set; }
}
