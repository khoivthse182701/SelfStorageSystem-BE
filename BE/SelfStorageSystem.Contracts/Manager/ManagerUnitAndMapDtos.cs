using System.ComponentModel.DataAnnotations;

namespace SelfStorageSystem.Contracts.Manager;

public class ManagerStorageUnitDto
{
    public long Id { get; set; }
    public long FacilityId { get; set; }
    public string UnitCode { get; set; } = string.Empty;
    public long UnitTypeId { get; set; }
    public string UnitTypeCode { get; set; } = string.Empty;
    public string UnitTypeName { get; set; } = string.Empty;
    public decimal WidthM { get; set; }
    public decimal LengthM { get; set; }
    public decimal HeightM { get; set; }
    public decimal? AreaM2 { get; set; }
    public decimal? VolumeM3 { get; set; }
    public long? AreaId { get; set; }
    public string? AreaName { get; set; }
    public string? FloorLabel { get; set; }
    public string? ZoneLabel { get; set; }
    public string PhysicalStatus { get; set; } = string.Empty;
    public bool IsListed { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class ManagerCreateStorageUnitRequest
{
    [Required]
    public string UnitCode { get; set; } = string.Empty;

    [Required]
    public long UnitTypeId { get; set; }

    public long? AreaId { get; set; }

    public string? FloorLabel { get; set; }

    public string? ZoneLabel { get; set; }

    public string? PhysicalStatus { get; set; }

    public bool IsListed { get; set; } = true;

    public string? Notes { get; set; }
}

public class ManagerUpdateStorageUnitRequest
{
    [Required]
    public string UnitCode { get; set; } = string.Empty;

    [Required]
    public long UnitTypeId { get; set; }

    public long? AreaId { get; set; }

    public string? FloorLabel { get; set; }

    public string? ZoneLabel { get; set; }

    public string? PhysicalStatus { get; set; }

    public bool IsListed { get; set; } = true;

    public string? Notes { get; set; }
}

public class ManagerAreaLayoutDto
{
    public long AreaId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AreaType { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string MapMetadata { get; set; } = "{}";
}

public class ManagerUnitMapPositionDto
{
    public long UnitId { get; set; }
    public string UnitCode { get; set; } = string.Empty;
    public long AreaId { get; set; }
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public decimal RotationDegrees { get; set; }
    public string PhysicalStatus { get; set; } = string.Empty;
    public string Metadata { get; set; } = "{}";
}

public class ManagerMapLayoutDto
{
    public long FacilityId { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public List<ManagerAreaLayoutDto> Areas { get; set; } = new();
    public List<ManagerUnitMapPositionDto> UnitPositions { get; set; } = new();
}

public class ManagerUpdateMapLayoutRequest
{
    [Required]
    public List<ManagerUnitMapPositionDto> Positions { get; set; } = new();
}

public class ManagerTransferRequestDto
{
    public long Id { get; set; }
    public long AgreementId { get; set; }
    public string AgreementNo { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public long FromUnitId { get; set; }
    public string FromUnitCode { get; set; } = string.Empty;
    public long RequestedUnitTypeId { get; set; }
    public string RequestedUnitTypeName { get; set; } = string.Empty;
    public long RequestedFacilityId { get; set; }
    public DateOnly MoveOutDate { get; set; }
    public DateOnly MoveInDate { get; set; }
    public decimal DiffRentAmount { get; set; }
    public decimal DiffDepositAmount { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public int RescheduleCount { get; set; }
    public long? ToUnitId { get; set; }
    public string? ToUnitCode { get; set; }
    public DateOnly RequestedEffectiveDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long RequestedBy { get; set; }
    public long? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class ManagerDecideTransferRequest
{
    [Required]
    public string Decision { get; set; } = string.Empty; // approved or rejected

    public long? TargetUnitId { get; set; }

    public string? ReviewNotes { get; set; }
}
