using System;

namespace SelfStorageSystem.Contracts.Customer.Rentals;

public class UnlockStorageUnitResponseDto
{
    public bool Success { get; set; }
    public bool Unlocked => Success;
    public long AgreementId { get; set; }
    public string UnitCode { get; set; } = null!;
    public string FacilityName { get; set; } = null!;
    public DateTimeOffset UnlockedAt { get; set; }
    public DateTimeOffset OccurredAt => UnlockedAt;
    public long? AccessEventId { get; set; }
    public int RelockAfterSeconds { get; set; }
    public string Message { get; set; } = null!;
}
