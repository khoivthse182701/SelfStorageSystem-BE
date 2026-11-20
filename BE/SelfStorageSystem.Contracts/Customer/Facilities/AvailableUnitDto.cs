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
    public string? CategoryName { get; set; }
    public string? StorageCategory { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? Area { get; set; }
    public decimal? AreaM2 { get; set; }
    public decimal? CustomMonthlyRate { get; set; }
    public string? ImageUrl { get; set; }
    public UnitMapPositionDto? MapPosition { get; set; }
}

public sealed record UnitMapPositionDto(decimal X, decimal Y, decimal Width, decimal Height);
