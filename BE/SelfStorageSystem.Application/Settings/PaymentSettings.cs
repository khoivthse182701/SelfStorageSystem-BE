namespace SelfStorageSystem.Application.Settings;

public class PaymentSettings
{
    public const string SectionName = "PaymentSettings";

    public VietQrConfig VietQr { get; set; } = new();
    public SePayConfig SePay { get; set; } = new();
}

public class SePayConfig
{
    public string ApiKey { get; set; } = "Admin@123";
    public string InvoicePrefix { get; set; } = "DH";
}

public class VietQrConfig
{
    public string BaseUrl { get; set; } = "https://vietqr.app/img";
    public string BankCode { get; set; } = "MBBank";
    public string AccountNo { get; set; } = "0817495759";
    public string AccountName { get; set; } = "VO THAI HOANG KHOI";
    public string Template { get; set; } = "compact";
    public bool ShowInfo { get; set; } = true;
}
