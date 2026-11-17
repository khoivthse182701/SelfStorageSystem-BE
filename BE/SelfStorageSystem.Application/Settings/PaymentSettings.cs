namespace SelfStorageSystem.Application.Settings;

public class PaymentSettings
{
    public const string SectionName = "PaymentSettings";

    public VietQrConfig VietQr { get; set; } = new();
    public SePayConfig SePay { get; set; } = new();
}

public class SePayConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string InvoicePrefix { get; set; } = "DH";
}

public class VietQrConfig
{
    public string BaseUrl { get; set; } = "https://vietqr.app/img";
    public string BankCode { get; set; } = string.Empty;
    public string AccountNo { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Template { get; set; } = "qronly";
    public bool ShowInfo { get; set; } = false;
}

