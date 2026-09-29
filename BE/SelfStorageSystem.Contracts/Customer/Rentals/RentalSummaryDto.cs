namespace SelfStorageSystem.Contracts.Customer.Rentals;

public class RentalSummaryDto
{
    public long AgreementId { get; set; }
    public string AgreementNo { get; set; } = null!;
    public long FacilityId { get; set; }
    public string FacilityName { get; set; } = null!;
    public string FacilityAddress { get; set; } = null!;
    public string FacilityCity { get; set; } = null!;
    public long StorageUnitId { get; set; }
    public string UnitCode { get; set; } = null!;
    public string? FloorLabel { get; set; }
    public string? ZoneLabel { get; set; }
    public string UnitTypeName { get; set; } = null!;
    public string Dimensions { get; set; } = null!;
    public decimal? AreaM2 { get; set; }
    public decimal? VolumeM3 { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }
    public decimal MonthlyRate { get; set; }
    public decimal DepositBalance { get; set; }
    public string Status { get; set; } = null!;
    public DateTimeOffset? CheckedInAt { get; set; }
    public bool HasOverdueDebt { get; set; }
    public int DaysUntilExpiry { get; set; }
}
