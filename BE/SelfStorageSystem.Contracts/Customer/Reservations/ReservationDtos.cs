namespace SelfStorageSystem.Contracts.Customer.Reservations;

public class CreateReservationResponse
{
    public long ReservationId { get; set; }
    public string ReservationCode { get; set; } = string.Empty;
    public DateTimeOffset HoldUntil { get; set; }
    public int ExpiresInSeconds { get; set; }
    public string Status { get; set; } = string.Empty;
    public long InvoiceId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public decimal FirstPaymentAmount { get; set; }
    public decimal MonthlyRate { get; set; }
    public decimal SecurityDeposit { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? UnitCode { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public string UnitTypeName { get; set; } = string.Empty;
}

public class ReservationSummaryDto
{
    public long Id { get; set; }
    public string ReservationCode { get; set; } = string.Empty;
    public long FacilityId { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public string FacilityAddress { get; set; } = string.Empty;
    public long UnitTypeId { get; set; }
    public string UnitTypeName { get; set; } = string.Empty;
    public string? UnitCode { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal MonthlyRateSnapshot { get; set; }
    public decimal DepositSnapshot { get; set; }
    public decimal QuotedTotal { get; set; }
    public DateTimeOffset HoldUntil { get; set; }
    public int ExpiresInSeconds { get; set; }
    public string Status { get; set; } = string.Empty;
    public string DisplayStatus { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public long? InvoiceId { get; set; }
    public string? InvoiceStatus { get; set; }
    public decimal FirstPaymentTotal { get; set; }
}

public class ReservationDetailDto : ReservationSummaryDto
{
    public decimal BookingFeeSnapshot { get; set; }
    public decimal DiscountSnapshot { get; set; }
    public string? PromotionCode { get; set; }
    public string? CheckInQrToken { get; set; }
    public string? CheckInInstructions { get; set; }
    public List<InvoiceSummaryDto> Invoices { get; set; } = new();
}

public class InvoiceSummaryDto
{
    public long Id { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<InvoiceLineItemDto> Lines { get; set; } = new();
}

public class InvoiceLineItemDto
{
    public long Id { get; set; }
    public string LineType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineAmount { get; set; }
}
