using System.ComponentModel.DataAnnotations;

namespace SelfStorageSystem.Contracts.Manager;

public class ManagerRefundItemDto
{
    public long RefundId { get; set; }
    public long? AgreementId { get; set; }
    public string? AgreementNo { get; set; }
    public long FacilityId { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public decimal DepositAmount { get; set; }
    public decimal DamageCost { get; set; }
    public decimal RefundAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = string.Empty;
    public string PayoutMethod { get; set; } = string.Empty;
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankAccountHolder { get; set; }
    public string? TransactionReference { get; set; }
    public string? ProofImageUrl { get; set; }
    public long? ProcessedByUserId { get; set; }
    public string? ProcessedByUserName { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RefundedAt { get; set; }
}

public class ManagerProcessRefundPayoutRequest
{
    public string? TransactionReference { get; set; }

    public string? TransactionRef
    {
        get => TransactionReference;
        set => TransactionReference = value;
    }

    public string? ProofImageUrl { get; set; }

    public string? ProofUrl
    {
        get => ProofImageUrl;
        set => ProofImageUrl = value;
    }

    public string? Note { get; set; }

    public string? Notes
    {
        get => Note;
        set => Note = value;
    }

    public decimal? Amount { get; set; }
}

public class ManagerProcessRefundPayoutResultDto
{
    public long RefundId { get; set; }
    public long? AgreementId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PayoutMethod { get; set; } = "bank_transfer";
    public string TransactionReference { get; set; } = string.Empty;
    public string? ProofImageUrl { get; set; }
    public long ProcessedByUserId { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
