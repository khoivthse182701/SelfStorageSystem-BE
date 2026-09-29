namespace SelfStorageSystem.Domain.Constants;

public static class ReservationLogMessages
{
    public const string HoldCreated = "Reservation hold created: {ReservationCode} for customer {CustomerId}, unit {UnitId}, holds until {HoldUntil}";
    public const string CreateFailed = "Failed to create reservation for customer {CustomerId}: {Message}";
    public const string CancelledByCustomer = "Reservation {ReservationCode} successfully cancelled by customer {CustomerId}. Unit released.";
    public const string FallbackReservationSeq = "Failed to fetch from core.reservation_code_seq. Using fallback sequence generator.";
    public const string FallbackInvoiceSeq = "Failed to fetch from core.invoice_no_seq. Using fallback sequence generator.";
}

public static class PaymentLogMessages
{
    public const string NonPositiveTransfer = "SePay Webhook: Ignored non-positive transfer amount {Amount}";
    public const string OutgoingDebitIgnored = "SePay Webhook: Ignored outgoing account debit (transferType = out).";
    public const string PatternMismatch = "SePay Webhook: Content does not match pattern {Prefix}(invoiceId). Content: {Content}";
    public const string TransactionReplay = "SePay Webhook: Transaction {Ref} already processed successfully. Skipping replay.";
    public const string InvoiceNotFound = "SePay Webhook: Invoice ID {InvoiceId} not found.";
    public const string InvoiceAlreadyPaid = "SePay Webhook: Invoice ID {InvoiceId} already paid previously.";
    public const string PartialPayment = "SePay Webhook: Received amount {Amount} is less than required balance {Remaining} (Invoice {InvoiceId}). Recording partial payment without confirming reservation.";
    public const string LatePaymentState = "Late Payment: Reservation {ReservationCode} is in {Status} state. Recording payment for manual refund or unit change.";
    public const string LatePaymentEmailFailed = "Failed to send late payment email notification to {Email}";
    public const string LatePaymentFailed = "Error processing late payment for Payment ID {PaymentId}: {Message}";
    public const string PaymentCompleted = "Payment completed successfully for Payment ID {PaymentId}, Invoice ID {InvoiceId}, Reservation {ReservationCode}";
    public const string ConfirmationEmailFailed = "Failed to send reservation confirmation email to {Email}";
    public const string PaymentCompletionError = "Error completing payment for Payment ID {PaymentId}: {Message}";
}

public static class EmailLogMessages
{
    public const string SentOtpSuccess = "Sent OTP email successfully to {Email}";
    public const string SentOtpFailed = "Failed to send OTP email to {Email}: {Message}";
    public const string SentEmailSuccess = "Sent email successfully to {Email} with subject {Subject}";
    public const string SentEmailFailed = "Failed to send email to {Email}: {Message}";
}

public static class AuthLogMessages
{
    public const string InvalidGoogleToken = "Invalid Google ID Token: {Message}";
    public const string VerifyGoogleTokenError = "Error verifying Google ID Token: {Message}";
}
