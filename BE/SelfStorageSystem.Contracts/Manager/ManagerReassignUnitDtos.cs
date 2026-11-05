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

    public long? TicketId { get; set; }

    public string? Notes { get; set; }
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

public class ManagerEmergencyCancelRefundRequest
{
    [Required]
    [MinLength(3, ErrorMessage = "Reason must be at least 3 characters long.")]
    public string Reason { get; set; } = "Hủy và hoàn tiền 100% do kho gặp sự cố nặng và cơ sở hết kho thay thế";

    public long? TicketId { get; set; }

    public string? Notes { get; set; }
}

public class ManagerEmergencyCancelRefundResponseDto
{
    public long AgreementId { get; set; }
    public string AgreementNo { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public long StorageUnitId { get; set; }
    public string UnitCode { get; set; } = string.Empty;
    public decimal TotalRefundAmount { get; set; }
    public long? RefundId { get; set; }
    public string AgreementStatus { get; set; } = string.Empty;
    public string UnitStatus { get; set; } = string.Empty;
    public DateTimeOffset CancelledAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
