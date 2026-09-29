namespace SelfStorageSystem.Contracts.Customer.Facilities;

public class FacilityMapUnitDto
{
    public long UnitId { get; set; }
    public string UnitCode { get; set; } = string.Empty;
    public long UnitTypeId { get; set; }
    public long AreaId { get; set; }
    public string? Layer { get; set; }
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public decimal RotationDegrees { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Metadata { get; set; }
}
