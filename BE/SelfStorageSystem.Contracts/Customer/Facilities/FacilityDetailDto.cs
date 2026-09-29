namespace SelfStorageSystem.Contracts.Customer.Facilities;

public class FacilityDetailDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public TimeOnly? OpeningTime { get; set; }
    public TimeOnly? ClosingTime { get; set; }
    public string? Description { get; set; }
    public List<string> Amenities { get; set; } = new();
    public List<UnitTypeAvailabilityDto> UnitTypes { get; set; } = new();
}
