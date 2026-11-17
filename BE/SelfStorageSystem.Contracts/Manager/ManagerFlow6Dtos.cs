using System.ComponentModel.DataAnnotations;

namespace SelfStorageSystem.Contracts.Manager;

public class ManagerLiquidateAgreementRequest
{
    [Required]
    public string Reason { get; set; } = string.Empty;

    public decimal? AuctionProceedsAmount { get; set; }

    public bool DeductDeposit { get; set; } = true;
}

public class ManagerLiquidateAgreementResultDto
{
    public long AgreementId { get; set; }
    public string AgreementNo { get; set; } = string.Empty;
    public string AgreementStatus { get; set; } = string.Empty;
    public long StorageUnitId { get; set; }
    public string UnitCode { get; set; } = string.Empty;
    public string UnitStatus { get; set; } = string.Empty;
    public int RevokedCredentialsCount { get; set; }
    public decimal DeductedDepositAmount { get; set; }
    public decimal SettledDebtAmount { get; set; }
    public decimal RemainingDebtAmount { get; set; }
    public DateTimeOffset LiquidatedAt { get; set; }
}
