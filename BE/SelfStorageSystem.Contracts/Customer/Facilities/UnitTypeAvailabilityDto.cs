namespace SelfStorageSystem.Contracts.Customer.Facilities;

public class UnitTypeAvailabilityDto
{
    public long UnitTypeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal WidthM { get; set; }
    public decimal LengthM { get; set; }
    public decimal HeightM { get; set; }
    public decimal? AreaM2 { get; set; }
    public decimal? VolumeM3 { get; set; }
    public decimal? MonthlyRate { get; set; }
    public int AvailableUnitCount { get; set; }
}
