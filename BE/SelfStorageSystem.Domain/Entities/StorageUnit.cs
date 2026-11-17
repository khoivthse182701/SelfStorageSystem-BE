using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Domain.Entities;

public partial class StorageUnit
{
    public long Id { get; set; }

    public long FacilityId { get; set; }

    public long UnitTypeId { get; set; }

    public long? AreaId { get; set; }

    public string UnitCode { get; set; } = null!;

    public string? FloorLabel { get; set; }

    public string? ZoneLabel { get; set; }

    public string PhysicalStatus { get; set; } = null!;

    public bool IsListed { get; set; }

    public string? Notes { get; set; }

    [Column("custom_width_m")]
    public decimal? CustomWidthM { get; set; }

    [Column("custom_length_m")]
    public decimal? CustomLengthM { get; set; }

    [Column("custom_height_m")]
    public decimal? CustomHeightM { get; set; }

    [Column("custom_area_m2")]
    public decimal? CustomAreaM2 { get; set; }

    [Column("storage_category")]
    public string? StorageCategory { get; set; }

    [Column("custom_monthly_rate")]
    public decimal? CustomMonthlyRate { get; set; }

    [Column("base_price_per_sqm")]
    public decimal? BasePricePerSqm { get; set; }

    [Column("category_surcharge")]
    public decimal? CategorySurcharge { get; set; }

    [Column("image_url")]
    public string? ImageUrl { get; set; }

    [Column("grid_row")]
    public int? GridRow { get; set; }

    [Column("grid_col")]
    public int? GridCol { get; set; }

    [Column("grid_row_span")]
    public int GridRowSpan { get; set; } = 1;

    [Column("grid_col_span")]
    public int GridColSpan { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    [NotMapped, JsonIgnore]
    public UnitStatus UnitStatus
    {
        get => EnumDbMapper.ParseUnitStatus(PhysicalStatus);
        set => PhysicalStatus = value.ToDbValue();
    }

    public bool CanTransitionTo(UnitStatus nextStatus) =>
        EnumDbMapper.TryParseUnitStatus(PhysicalStatus, out var current) &&
        DomainStateMachine.CanTransition(current, nextStatus);

    public void TransitionStatus(UnitStatus nextStatus, DateTimeOffset? utcNow = null)
    {
        if (!string.IsNullOrWhiteSpace(PhysicalStatus) && !CanTransitionTo(nextStatus))
        {
            throw new InvalidOperationException($"Invalid unit status transition from '{PhysicalStatus}' to '{nextStatus.ToDbValue()}'.");
        }

        PhysicalStatus = nextStatus.ToDbValue();
        UpdatedAt = utcNow ?? DateTimeOffset.UtcNow;
    }

    public virtual FacilityArea? Area { get; set; }

    public virtual Facility Facility { get; set; } = null!;

    public virtual ICollection<Inspection> Inspections { get; set; } = new List<Inspection>();

    public virtual ICollection<MaintenanceWorkOrder> MaintenanceWorkOrders { get; set; } = new List<MaintenanceWorkOrder>();

    public virtual ICollection<SupportTicket> SupportTicketStorageUnits { get; set; } = new List<SupportTicket>();

    public virtual ICollection<UnitAllocation> UnitAllocations { get; set; } = new List<UnitAllocation>();

    public virtual UnitMapPosition? UnitMapPosition { get; set; }

    public virtual ICollection<UnitStatusHistory> UnitStatusHistories { get; set; } = new List<UnitStatusHistory>();

    public virtual ICollection<UnitTransferRequest> UnitTransferRequestFromUnits { get; set; } = new List<UnitTransferRequest>();

    public virtual ICollection<UnitTransferRequest> UnitTransferRequestToUnits { get; set; } = new List<UnitTransferRequest>();

    public virtual UnitType UnitType { get; set; } = null!;
}
