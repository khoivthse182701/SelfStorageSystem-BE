using System;

namespace SelfStorageSystem.Domain.Entities;

public partial class StoredItem
{
    public long Id { get; set; }

    public long AgreementId { get; set; }

    public string ItemName { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string? Description { get; set; }

    public int Quantity { get; set; } = 1;

    public decimal? EstimatedValue { get; set; }

    public string RiskClassification { get; set; } = "standard";

    public string? PhotoUrl { get; set; }

    public DateTimeOffset DeclaredAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public virtual RentalAgreement RentalAgreement { get; set; } = null!;
}
