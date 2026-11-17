using System.ComponentModel.DataAnnotations;

namespace SelfStorageSystem.Contracts.Customer;

public class CustomerSubmitBankInfoRequest
{
    [Required]
    [MinLength(2)]
    public string BankName { get; set; } = string.Empty;

    [Required]
    [MinLength(4)]
    public string BankAccountNumber { get; set; } = string.Empty;

    [Required]
    [MinLength(2)]
    public string BankAccountHolder { get; set; } = string.Empty;

    public string? Note { get; set; }
}

public class CustomerSubmitRefundBankInfoRequest : CustomerSubmitBankInfoRequest
{
    public string? BankCode
    {
        get => BankName;
        set { if (!string.IsNullOrWhiteSpace(value)) BankName = value; }
    }

    public string AccountNumber
    {
        get => BankAccountNumber;
        set => BankAccountNumber = value;
    }

    public string? BankAccountNo
    {
        get => BankAccountNumber;
        set { if (!string.IsNullOrWhiteSpace(value)) BankAccountNumber = value; }
    }

    public string AccountHolderName
    {
        get => BankAccountHolder;
        set => BankAccountHolder = value;
    }

    public string? BankAccountName
    {
        get => BankAccountHolder;
        set { if (!string.IsNullOrWhiteSpace(value)) BankAccountHolder = value; }
    }
}

public class CustomerRefundBankInfoDto
{
    public long RefundId { get; set; }
    public long? AgreementId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PayoutMethod { get; set; } = "bank_transfer";
    public string BankName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankAccountHolder { get; set; } = string.Empty;
    public DateTimeOffset SubmittedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
