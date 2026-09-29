namespace SelfStorageSystem.Domain.Constants;

public static class PaymentConstants
{
    public const string CurrencyVnd = "VND";

    public const string MethodBankTransfer = "bank_transfer";
    public const string MethodCreditCard = "credit_card";
    public const string MethodCash = "cash";

    public const string ProviderSePay = "SePay";
    public const string ProviderVnPay = "VNPay";

    public const string StatusPending = "pending";
    public const string StatusSucceeded = "succeeded";
    public const string StatusFailed = "failed";
    public const string StatusCancelled = "cancelled";
    public const string StatusPartiallyRefunded = "partially_refunded";
    public const string StatusRefunded = "refunded";
}

public static class SePayConstants
{
    public const string DefaultInvoicePrefix = "DH";
    public const string TransferTypeIn = "in";
    public const string TransferTypeOut = "out";
    public const string DefaultBankCode = "MBBank";
    public const string DefaultTemplate = "compact";
    public const string DefaultBaseUrl = "https://vietqr.app/img";
}
