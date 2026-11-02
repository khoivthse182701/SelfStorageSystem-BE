using System.ComponentModel.DataAnnotations;

namespace SelfStorageSystem.Contracts.Staff;

public class StaffSubmitTicketReportRequest
{
    [Required]
    [MinLength(3)]
    public string ReportNotes { get; set; } = string.Empty;

    /// <summary>
    /// "resolved" if incident is fixed/cleaned, or "severe_need_relocation" if unit is damaged and needs customer relocation.
    /// </summary>
    [Required]
    public string AssessmentLevel { get; set; } = "resolved";

    public List<string>? EvidenceImageUrls { get; set; }
}

public class StaffTicketReportResultDto
{
    public long TicketId { get; set; }
    public string TicketNo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string AssessmentLevel { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public DateTimeOffset? ResolvedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class StaffSubmitInspectionReportRequest
{
    [Range(0, 1000000000)]
    public decimal DamageCost { get; set; } = 0m;

    [Required]
    public string OverallCondition { get; set; } = "Good";

    public string? Summary { get; set; }

    public List<string>? DamageItemNames { get; set; }

    public List<string>? PhotoUrls { get; set; }
}

public class StaffInspectionResultDto
{
    public long InspectionId { get; set; }
    public long AgreementId { get; set; }
    public string AgreementNo { get; set; } = string.Empty;
    public decimal OriginalDeposit { get; set; }
    public decimal DamageCost { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal ExtraPenalty { get; set; }
    public long? PenaltyInvoiceId { get; set; }
    public string NextUnitStatus { get; set; } = string.Empty;
    public long? RefundId { get; set; }
    public string SettlementSummary { get; set; } = string.Empty;
    public DateTimeOffset CompletedAt { get; set; }
}

public class StaffConfirmRefundPaymentRequest
{
    [Required]
    public long RefundId { get; set; }

    /// <summary>
    /// "cash" or "bank_transfer"
    /// </summary>
    [Required]
    public string PaymentMethod { get; set; } = "cash";

    [Range(0, 1000000000)]
    public decimal AmountPaid { get; set; }

    public string? ReceiptPhotoUrl { get; set; }

    public string? Note { get; set; }
}

public class StaffRefundQueueItemDto
{
    public long RefundId { get; set; }
    public long? AgreementId { get; set; }
    public string? AgreementNo { get; set; }
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankAccountHolder { get; set; }
    public string? ReceiptPhotoUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RefundedAt { get; set; }
}


public class StaffCounterCashPayoutRequest
{
    public string? ProofImageUrl { get; set; }

    public string? Note { get; set; }
}

public class StaffCounterCashPayoutResultDto
{
    public long RefundId { get; set; }
    public long? AgreementId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PayoutMethod { get; set; } = "cash";
    public string? ProofImageUrl { get; set; }
    public long ProcessedByUserId { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
