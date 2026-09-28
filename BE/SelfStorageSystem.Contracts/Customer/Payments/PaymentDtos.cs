using System.ComponentModel.DataAnnotations;

namespace SelfStorageSystem.Contracts.Customer.Payments;

public class CreateCheckoutRequest
{
    [Required(ErrorMessage = "Reservation ID (reservationId) is required.")]
    public long ReservationId { get; set; }

    /// <summary>
    /// Payment method: "bank_transfer" or "sepay_vietqr".
    /// </summary>
    public string PaymentMethod { get; set; } = "bank_transfer";
}

public class CheckoutResponse
{
    public long PaymentId { get; set; }
    public long ReservationId { get; set; }
    public long InvoiceId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string PaymentMethod { get; set; } = "bank_transfer";
    public int ExpiresInSeconds { get; set; }

    /// <summary>
    /// Dynamic VietQR payment details processed via SePay.
    /// </summary>
    public VietQrPaymentInfo? VietQr { get; set; }
}

public class VietQrPaymentInfo
{
    public string BankCode { get; set; } = string.Empty;
    public string AccountNo { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string TransferContent { get; set; } = string.Empty;
    public string QrImageUrl { get; set; } = string.Empty;
}

public class SePayWebhookPayload
{
    public long Id { get; set; }
    public string Gateway { get; set; } = string.Empty;
    public string TransactionDate { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string Content { get; set; } = string.Empty;
    public string TransferType { get; set; } = string.Empty;
    public decimal TransferAmount { get; set; }
    public decimal Accumulated { get; set; }
    public string? ReferenceCode { get; set; }
    public string? SubAccount { get; set; }
}

public class PaymentHistoryDto
{
    public long PaymentId { get; set; }
    public long InvoiceId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public long? ReservationId { get; set; }
    public string? ReservationCode { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Method { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? ProviderTransactionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? FailureReason { get; set; }
}
