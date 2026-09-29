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
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public long Id { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("gateway")]
    public string Gateway { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("transactionDate")]
    public string TransactionDate { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("accountNumber")]
    public string AccountNumber { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("code")]
    public string? Code { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("transferType")]
    public string TransferType { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("transfer_type")]
    public string? TransferTypeSnakeCase
    {
        set { if (!string.IsNullOrEmpty(value) && string.IsNullOrEmpty(TransferType)) TransferType = value; }
    }

    [System.Text.Json.Serialization.JsonPropertyName("transferAmount")]
    public decimal TransferAmount { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("transfer_amount")]
    public decimal? TransferAmountSnakeCase
    {
        set { if (value.HasValue && TransferAmount == 0) TransferAmount = value.Value; }
    }

    [System.Text.Json.Serialization.JsonPropertyName("accumulated")]
    public decimal Accumulated { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("referenceCode")]
    public string? ReferenceCode { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("reference_code")]
    public string? ReferenceCodeSnakeCase
    {
        set { if (!string.IsNullOrEmpty(value) && string.IsNullOrEmpty(ReferenceCode)) ReferenceCode = value; }
    }

    [System.Text.Json.Serialization.JsonPropertyName("subAccount")]
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
