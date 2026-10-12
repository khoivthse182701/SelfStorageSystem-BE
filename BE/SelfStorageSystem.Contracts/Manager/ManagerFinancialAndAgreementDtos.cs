using System.ComponentModel.DataAnnotations;
using SelfStorageSystem.Contracts.Customer.Rentals;

namespace SelfStorageSystem.Contracts.Manager;

public class ManagerChargeProposalDto
{
    public long Id { get; set; }
    public long TicketId { get; set; }
    public string TicketTitle { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public long? AgreementId { get; set; }
    public string? AgreementNo { get; set; }
    public long ProposedBy { get; set; }
    public string ProposedByStaffName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string? Decision { get; set; }
    public string? DecisionReason { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}

public class ManagerDecideChargeProposalRequest
{
    [Required]
    public string Decision { get; set; } = string.Empty; // approved or rejected

    public string? Reason { get; set; }
}

public class ManagerChargeProposalDecisionDto
{
    public long ProposalId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string? GeneratedInvoiceNo { get; set; }
    public long? GeneratedInvoiceId { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
}

public class ManagerRefundApprovalDto
{
    public long RefundId { get; set; }
    public long PaymentId { get; set; }
    public long? AgreementId { get; set; }
    public string? AgreementNo { get; set; }
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string? Decision { get; set; }
    public string? DecisionReason { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public decimal OriginalDeposit { get; set; }
    public decimal EstimatedDeductions { get; set; }
    public decimal RecommendedRefundAmount { get; set; }
}

public class ManagerDecideRefundApprovalRequest
{
    [Required]
    public string Decision { get; set; } = string.Empty; // approved or rejected

    public string? Reason { get; set; }

    public decimal? OverrideAmount { get; set; }
}

public class ManagerRefundDecisionDto
{
    public long RefundId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public decimal ApprovedAmount { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
}

public class ManagerAgreementSummaryDto
{
    public long Id { get; set; }
    public string AgreementNo { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public long StorageUnitId { get; set; }
    public string UnitCode { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal DepositAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal OutstandingBalance { get; set; }
    public int UnpaidInvoicesCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class ManagerAgreementInvoiceItemDto
{
    public long Id { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ManagerAgreementDetailDto
{
    public long Id { get; set; }
    public string AgreementNo { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public long StorageUnitId { get; set; }
    public string UnitCode { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal DepositAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalDebt { get; set; }
    public List<ManagerAgreementInvoiceItemDto> Invoices { get; set; } = new();
    public List<StoredItemDto> StoredItems { get; set; } = new();
}

public class ManagerDelinquencyActionDto
{
    public long Id { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public long? PerformedBy { get; set; }
    public string? PerformedByName { get; set; }
    public string Details { get; set; } = "{}";
    public DateTimeOffset OccurredAt { get; set; }
}

public class ManagerDelinquencyCaseDto
{
    public long Id { get; set; }
    public long AgreementId { get; set; }
    public string AgreementNo { get; set; } = string.Empty;
    public string UnitCode { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal OutstandingSnapshot { get; set; }
    public decimal CurrentDebt { get; set; }
    public int OverdueDays { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset GraceEndsAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? Notes { get; set; }
    public List<ManagerDelinquencyActionDto> Actions { get; set; } = new();
}

public class ManagerAddDelinquencyActionRequest
{
    [Required]
    public string ActionType { get; set; } = string.Empty;

    public string? Note { get; set; }

    public string? UpdateCaseStatus { get; set; }
}

public class ManagerDailyScheduleDto
{
    public int TodayCheckInsCount { get; set; }
    public int TodayCheckOutsCount { get; set; }
    public int TodayInspectionsCount { get; set; }
}

public class ManagerFacilityDashboardDto
{
    public long FacilityId { get; set; }
    public string FacilityCode { get; set; } = string.Empty;
    public string FacilityName { get; set; } = string.Empty;
    public int TotalUnits { get; set; }
    public int OccupiedUnits { get; set; }
    public int ReservedUnits { get; set; }
    public int AvailableUnits { get; set; }
    public int MaintenanceUnits { get; set; }
    public double OccupancyRate { get; set; }
    public ManagerDailyScheduleDto DailySchedule { get; set; } = new();
    public int OpenTicketsCount { get; set; }
    public int UrgentTicketsCount { get; set; }
    public decimal ThisMonthRevenue { get; set; }
    public decimal TotalOutstandingDebt { get; set; }
}
