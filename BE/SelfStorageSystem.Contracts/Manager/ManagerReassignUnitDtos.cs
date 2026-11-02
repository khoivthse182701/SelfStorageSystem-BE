using System.ComponentModel.DataAnnotations;

namespace SelfStorageSystem.Contracts.Manager;

public class ManagerReassignUnitRequest
{
    public long NewStorageUnitId { get; set; }

    /// <summary>
    /// Alias for NewStorageUnitId to support payloads sending newUnitId
    /// </summary>
    public long NewUnitId
    {
        get => NewStorageUnitId;
        set => NewStorageUnitId = value;
    }

    [Required]
    [MinLength(3, ErrorMessage = "Reason must be at least 3 characters long.")]
    public string Reason { get; set; } = string.Empty;

    public bool SetOldUnitUnderMaintenance { get; set; } = true;
}

public class ManagerReassignUnitResponseDto
{
    public long AgreementId { get; set; }
    public string AgreementNo { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public long FacilityId { get; set; }

    public long OldStorageUnitId { get; set; }
    public string OldUnitCode { get; set; } = string.Empty;
    public string OldUnitStatus { get; set; } = string.Empty;

    public long NewStorageUnitId { get; set; }
    public string NewUnitCode { get; set; } = string.Empty;
    public string NewUnitStatus { get; set; } = string.Empty;
    public string NewUnitTypeName { get; set; } = string.Empty;

    public string? NewPinCode { get; set; }
    public string? NewPin => NewPinCode;

    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset ReassignedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
