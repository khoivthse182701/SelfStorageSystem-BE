namespace SelfStorageSystem.Contracts.Customer.Rentals;

public sealed class RentalDetailDto
{
    public long AgreementId { get; set; }
    public string AgreementNo { get; set; } = string.Empty;
    public long ReservationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }
    public decimal MonthlyRate { get; set; }
    public decimal DepositSnapshot { get; set; }
    public decimal DepositBalance { get; set; }
    public long PolicyVersionId { get; set; }
    public DateTimeOffset? SignedAt { get; set; }
    public DateTimeOffset? CheckedInAt { get; set; }
    public DateTimeOffset? CheckedOutAt { get; set; }
    public int DaysUntilExpiry { get; set; }
    public bool IsExpiringSoon { get; set; }
    public bool HasOverdueDebt { get; set; }
    public decimal OutstandingDebtAmount { get; set; }
    public RentalDetailFacilityDto Facility { get; set; } = new();
    public RentalDetailUnitDto Unit { get; set; } = new();
    public HandoverRecordDto? Handover { get; set; }
    public List<RentalDetailInvoiceDto> LatestInvoices { get; set; } = new();
    public RentalDetailRenewalDto? PendingRenewal { get; set; }
    public MoveOutResponseDto? PendingMoveOut { get; set; }
    public UnitTransferRequestDto? ActiveTransfer { get; set; }
}

public sealed class RentalDetailFacilityDto
{
    public long FacilityId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
    public TimeOnly? OpeningTime { get; set; }
    public TimeOnly? ClosingTime { get; set; }
}

public sealed class RentalDetailUnitDto
{
    public long StorageUnitId { get; set; }
    public string UnitCode { get; set; } = string.Empty;
    public string UnitTypeName { get; set; } = string.Empty;
    public string? FloorLabel { get; set; }
    public string? ZoneLabel { get; set; }
    public string Dimensions { get; set; } = string.Empty;
    public decimal? AreaM2 { get; set; }
    public decimal? VolumeM3 { get; set; }
    public string PhysicalStatus { get; set; } = string.Empty;
    public bool ClimateControlled { get; set; }
}

public sealed class RentalDetailInvoiceDto
{
    public long InvoiceId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public string? BillingPeriod { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount => Math.Max(0m, TotalAmount - PaidAmount);
    public string Status { get; set; } = string.Empty;
}

public sealed class RentalDetailRenewalDto
{
    public long RenewalId { get; set; }
    public DateOnly OldEndDate { get; set; }
    public DateOnly RequestedEndDate { get; set; }
    public decimal MonthlyRate { get; set; }
    public string Status { get; set; } = string.Empty;
    public long? InvoiceId { get; set; }
    public string? InvoiceNo { get; set; }
    public decimal TotalDueAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed record AccessEventDto(
    long Id,
    DateTimeOffset OccurredAt,
    string AccessPointName,
    string AccessPointType,
    string ActorName,
    string Result,
    string? Reason
);

public sealed record RevokeAuthorizedMemberResponseDto(
    long MemberId,
    string Status,
    DateTimeOffset RevokedAt
);
