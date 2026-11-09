using System.Globalization;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Domain.Constants;

namespace SelfStorageSystem.Infrastructure.Common;

public static class VietQrUrlBuilder
{
    public static string Build(VietQrConfig config, decimal amount, string? content)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? SePayConstants.DefaultBaseUrl : config.BaseUrl.TrimEnd('/');
        var bank = string.IsNullOrWhiteSpace(config.BankCode) ? SePayConstants.DefaultBankCode : config.BankCode;
        var template = string.IsNullOrWhiteSpace(config.Template) ? SePayConstants.DefaultTemplate : config.Template;
        // SePay / vietqr.app format: https://vietqr.app/img?bank=MBBank&acc=0817495759&template=qronly&showinfo=false&holder=VO%20THAI%20HOANG%20KHOI
        var url = $"{baseUrl}?bank={Uri.EscapeDataString(bank)}&acc={Uri.EscapeDataString(config.AccountNo ?? string.Empty)}&template={Uri.EscapeDataString(template)}&showinfo={(config.ShowInfo ? "true" : "false")}&holder={Uri.EscapeDataString(config.AccountName ?? string.Empty)}";
        if (amount > 0)
        {
            url += $"&amount={((long)amount).ToString(CultureInfo.InvariantCulture)}";
        }
        if (!string.IsNullOrWhiteSpace(content))
        {
            url += $"&des={Uri.EscapeDataString(content)}";
        }
        return url;
    }
}
