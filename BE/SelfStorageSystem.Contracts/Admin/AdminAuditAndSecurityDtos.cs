using SelfStorageSystem.Contracts.Common;

namespace SelfStorageSystem.Contracts.Admin;

public class AdminAuditLogDto
{
    public long Id { get; set; }
    public long? ActorUserId { get; set; }
    public string? ActorEmail { get; set; }
    public string? ActorRole { get; set; }
    public string EntitySchema { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? RequestId { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public class AdminAuditLogQueryParameters
{
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Action { get; set; }
    public long? ActorUserId { get; set; }
    public DateTimeOffset? FromDate { get; set; }
    public DateTimeOffset? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class AdminLoginHistoryDto
{
    public long Id { get; set; }
    public long? UserId { get; set; }
    public string? AttemptedEmail { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public bool IsSuspicious { get; set; }
    public string? SuspiciousReason { get; set; }
}

public class AdminLoginHistoryQueryParameters
{
    public long? UserId { get; set; }
    public string? AttemptedEmail { get; set; }
    public string? Result { get; set; }
    public bool? IsSuspiciousOnly { get; set; }
    public DateTimeOffset? FromDate { get; set; }
    public DateTimeOffset? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class AdminLoginSecuritySummaryDto
{
    public int TotalAttempts { get; set; }
    public int SucceededAttempts { get; set; }
    public int FailedAttempts { get; set; }
    public int LockedAttempts { get; set; }
    public int BlockedAttempts { get; set; }
    public int SuspiciousAttemptsCount { get; set; }
}

public class AdminLoginHistoryReportDto
{
    public PagedResult<AdminLoginHistoryDto> Histories { get; set; } = new();
    public AdminLoginSecuritySummaryDto Summary { get; set; } = new();
}

public class AdminDashboardOverviewDto
{
    public AdminRevenueKpiDto RevenueKpi { get; set; } = new();
    public AdminOccupancyKpiDto OccupancyKpi { get; set; } = new();
    public AdminUserKpiDto UserKpi { get; set; } = new();
    public List<AdminFacilityOccupancyBreakdownDto> FacilityBreakdown { get; set; } = new();
}

public class AdminRevenueKpiDto
{
    public decimal TotalRevenue { get; set; }
    public decimal ThisMonthRevenue { get; set; }
    public decimal LastMonthRevenue { get; set; }
    public int TotalInvoices { get; set; }
    public int PaidInvoices { get; set; }
    public int OverdueInvoices { get; set; }
    public int OpenInvoices { get; set; }
}

public class AdminOccupancyKpiDto
{
    public int TotalFacilities { get; set; }
    public int TotalStorageUnits { get; set; }
    public int OccupiedUnits { get; set; }
    public int ReservedUnits { get; set; }
    public int AvailableUnits { get; set; }
    public int MaintenanceUnits { get; set; }
    public double SystemOccupancyRate { get; set; }
}

public class AdminUserKpiDto
{
    public int TotalUsers { get; set; }
    public int TotalCustomers { get; set; }
    public int TotalStaff { get; set; }
    public int ActiveRentals { get; set; }
}

public class AdminFacilityOccupancyBreakdownDto
{
    public long FacilityId { get; set; }
    public string FacilityCode { get; set; } = string.Empty;
    public string FacilityName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalUnits { get; set; }
    public int OccupiedUnits { get; set; }
    public int ReservedUnits { get; set; }
    public int AvailableUnits { get; set; }
    public double OccupancyRate { get; set; }
}
