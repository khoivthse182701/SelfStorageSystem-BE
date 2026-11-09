namespace SelfStorageSystem.Contracts.Customer.Rentals;

/// <summary>
/// Khoản hoàn cọc đang chờ khách hàng nhập thông tin tài khoản ngân hàng.
/// </summary>
public class CustomerPendingRefundDto
{
    public long RefundId { get; set; }
    public long? AgreementId { get; set; }
    public string? AgreementNo { get; set; }
    public string? UnitCode { get; set; }
    public string? FacilityName { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}
