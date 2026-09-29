namespace SelfStorageSystem.Contracts.Customer.Rentals;

public class HandoverRecordDto
{
    public long HandoverId { get; set; }
    public long AgreementId { get; set; }
    public string AgreementNo { get; set; } = null!;
    public string HandoverType { get; set; } = null!;
    public string? CustomerSignatureRef { get; set; }
    public DateTimeOffset? CustomerSignedAt { get; set; }
    public string? StaffSignatureRef { get; set; }
    public DateTimeOffset? StaffSignedAt { get; set; }
    public string? HandledByName { get; set; }
    public string? HandoverNotes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public HandoverInspectionDto? Inspection { get; set; }
}

public class HandoverInspectionDto
{
    public long InspectionId { get; set; }
    public string InspectionType { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? OverallCondition { get; set; }
    public string? Summary { get; set; }
    public DateTimeOffset? InspectedAt { get; set; }
    public List<HandoverInspectionItemDto> Items { get; set; } = new();
}

public class HandoverInspectionItemDto
{
    public long ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public string Condition { get; set; } = null!;
    public string? Notes { get; set; }
    public string? PhotoUrl { get; set; }
    public decimal ChargeAmount { get; set; }
}
