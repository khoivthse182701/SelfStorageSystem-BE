using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class UnitTypeHaTdt
{
    public long UnitTypeHaTdtid { get; set; }

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
}
