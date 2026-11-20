namespace SelfStorageSystem.Contracts.Auth;

public class AuthUserDto
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? IdentityNumber { get; set; }
    public string? Address { get; set; }
    public int EditsUsedThisMonth { get; set; }
    public int RemainingEditsThisMonth { get; set; } = 2;
    public bool HasNegativeDepositDebt { get; set; }
    public decimal TotalNegativeDebtAmount { get; set; }
    public List<NegativeDebtInvoiceItemDto> NegativeDebtInvoices { get; set; } = new();
    public bool HasOverdueRentDebt { get; set; }
    public decimal TotalOverdueRentAmount { get; set; }
    public int OverdueAgreementCount { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
    public List<long> AssignedFacilityIds { get; set; } = new();
    public string Status { get; set; } = string.Empty;
}

public sealed record NegativeDebtInvoiceItemDto(
    long AgreementId,
    string AgreementNo,
    string UnitCode,
    string FacilityName,
    decimal NegativeBalance,
    long? DeficitInvoiceId,
    string? DeficitInvoiceNo,
    decimal OutstandingAmount);

public sealed record CustomerProfileDto(
    long UserId,
    string Email,
    string FullName,
    string? PhoneNumber,
    string? IdentityNumber,
    string? Address,
    bool IdentityNumberLocked,
    string CurrentEditMonth,
    int EditsUsedThisMonth,
    int MaxEditsPerMonth,
    int RemainingEditsThisMonth,
    bool HasNegativeDepositDebt,
    decimal TotalNegativeDebtAmount,
    List<NegativeDebtInvoiceItemDto> NegativeDebtInvoices,
    string? EmployeeCode = null,
    string? AssignedFacilityName = null,
    long? AssignedFacilityId = null,
    string? EmergencyContactName = null,
    string? EmergencyContactPhone = null,
    string? CompanyName = null,
    string? TaxId = null,
    string? InvoiceAddress = null,
    DateTimeOffset? LastLoginAt = null,
    string? LastLoginIp = null);

public sealed class UpdateCustomerProfileRequest
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? IdentityNumber { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? CompanyName { get; set; }
    public string? TaxId { get; set; }
    public string? InvoiceAddress { get; set; }
}

public sealed record ResendOtpResponse(int CooldownSeconds);
