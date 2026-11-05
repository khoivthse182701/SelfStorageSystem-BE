using System.ComponentModel.DataAnnotations;

namespace SelfStorageSystem.Contracts.Staff;

public class StaffSubmitTicketReportRequest
{
    private string _reportNotes = string.Empty;

    public string ReportNotes
    {
        get => _reportNotes;
        set => _reportNotes = value ?? string.Empty;
    }

    /// <summary>
    /// Alias for ReportNotes to safely support payload sending "note"
    /// </summary>
    public string? Note
    {
        get => _reportNotes;
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(_reportNotes))
            {
                _reportNotes = value.Trim();
            }
        }
    }

    private string _assessmentLevel = "resolved";

    /// <summary>
    /// "resolved" if incident is fixed/cleaned, or "severe_need_relocation" if unit is damaged and needs customer relocation.
    /// </summary>
    public string AssessmentLevel
    {
        get => _assessmentLevel;
        set => _assessmentLevel = value ?? "resolved";
    }

    /// <summary>
    /// Alias for AssessmentLevel to safely support payload sending "result"
    /// </summary>
    public string? Result
    {
        get => _assessmentLevel;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                var val = value.Trim().ToLowerInvariant();
                _assessmentLevel = val.Contains("severe") ? "severe_need_relocation" : "resolved";
            }
        }
    }

    public List<string>? EvidenceImageUrls { get; set; }

    /// <summary>
    /// Alias for EvidenceImageUrls to safely support payload sending "evidenceUrls"
    /// </summary>
    public List<string>? EvidenceUrls
    {
        get => EvidenceImageUrls;
        set => EvidenceImageUrls = value;
    }
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

public class StaffInspectionDamageItemDto
{
    public string ItemName { get; set; } = string.Empty;
    public string Condition { get; set; } = "damaged";
    public decimal Cost { get; set; } = 0m;
    public decimal ChargeAmount { get; set; } = 0m;
    public string? Notes { get; set; }
}

public class StaffSubmitInspectionReportRequest
{
    [Range(0, 1000000000)]
    public decimal DamageCost { get; set; } = 0m;

    public string OverallCondition { get; set; } = "Good";

    public string? Condition
    {
        get => OverallCondition;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                OverallCondition = value;
            }
        }
    }

    public string? Summary { get; set; }

    public string? Notes
    {
        get => Summary;
        set => Summary = value;
    }

    public bool? HasDamage { get; set; }

    public decimal? RefundAmount { get; set; }

    public decimal? PenaltyAmount { get; set; }

    public List<StaffInspectionDamageItemDto>? DamageItems { get; set; }

    public List<string>? DamageItemNames { get; set; }

    public List<string>? PhotoUrls { get; set; }

    public List<string>? EvidenceUrls
    {
        get => PhotoUrls;
        set => PhotoUrls = value;
    }
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
