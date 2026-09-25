using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class UnitType
{
    public long Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public decimal WidthM { get; set; }

    public decimal LengthM { get; set; }

    public decimal HeightM { get; set; }

    public decimal? AreaM2 { get; set; }

    public decimal? VolumeM3 { get; set; }

    public bool ClimateControlled { get; set; }

    public decimal? MaxWeightKg { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public virtual ICollection<FacilityRate> FacilityRates { get; set; } = new List<FacilityRate>();

    public virtual ICollection<PriceRange> PriceRanges { get; set; } = new List<PriceRange>();

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public virtual ICollection<StorageUnit> StorageUnits { get; set; } = new List<StorageUnit>();

    public virtual ICollection<UnitTransferRequest> UnitTransferRequests { get; set; } = new List<UnitTransferRequest>();
}
