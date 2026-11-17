namespace SelfStorageSystem.Contracts.Customer.Rentals;

public class AccessCredentialDto
{
    public long CredentialId { get; set; }
    public string CredentialType { get; set; } = "pin";
    public string? DisplayHint { get; set; }
    public long AgreementId { get; set; }
    public string AgreementNo { get; set; } = null!;
    public string UnitCode { get; set; } = null!;
    public string FacilityName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? KeypadPin { get; set; }
    public string? GateQrToken { get; set; }
    public int QrExpiresInSeconds { get; set; }
    public DateTimeOffset? QrExpiresAt { get; set; }
    public string? SuspendedReason { get; set; }
}
