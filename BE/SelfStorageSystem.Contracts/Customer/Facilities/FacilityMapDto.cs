namespace SelfStorageSystem.Contracts.Customer.Facilities;

public class FacilityMapDto
{
    public long FacilityId { get; set; }
    public List<FacilityMapUnitDto> Units { get; set; } = new();
}
