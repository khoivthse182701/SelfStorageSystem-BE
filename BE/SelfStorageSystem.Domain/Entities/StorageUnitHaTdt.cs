using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class StorageUnitHaTdt
{
    public long StorageUnitHaTdtid { get; set; }

    public long FacilityId { get; set; }

    public long UnitTypeHaTdtid { get; set; }

    public long? AreaId { get; set; }

    public string UnitCode { get; set; } = null!;

    public string? FloorLabel { get; set; }

    public string? ZoneLabel { get; set; }

    public string PhysicalStatus { get; set; } = null!;

    public bool IsListed { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
