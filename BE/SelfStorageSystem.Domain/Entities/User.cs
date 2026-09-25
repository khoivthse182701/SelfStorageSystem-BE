using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class User
{
    public long Id { get; set; }

    public string Email { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string PasswordHash { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTimeOffset? LastLoginAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<AuthorizedAccessMember> AuthorizedAccessMembers { get; set; } = new List<AuthorizedAccessMember>();

    public virtual CustomerProfile? CustomerProfile { get; set; }

    public virtual ICollection<DelinquencyAction> DelinquencyActions { get; set; } = new List<DelinquencyAction>();

    public virtual EmployeeProfile? EmployeeProfile { get; set; }

    public virtual ICollection<FacilityRate> FacilityRates { get; set; } = new List<FacilityRate>();

    public virtual ICollection<FeeRule> FeeRules { get; set; } = new List<FeeRule>();

    public virtual ICollection<LoginHistory> LoginHistories { get; set; } = new List<LoginHistory>();

    public virtual ICollection<MaintenanceWorkOrder> MaintenanceWorkOrders { get; set; } = new List<MaintenanceWorkOrder>();

    public virtual ICollection<MoveOutRequest> MoveOutRequests { get; set; } = new List<MoveOutRequest>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<PolicyVersion> PolicyVersions { get; set; } = new List<PolicyVersion>();

    public virtual ICollection<PriceRange> PriceRanges { get; set; } = new List<PriceRange>();

    public virtual ICollection<Promotion> Promotions { get; set; } = new List<Promotion>();

    public virtual ICollection<Refund> Refunds { get; set; } = new List<Refund>();

    public virtual ICollection<RentalRenewal> RentalRenewalRequestedByNavigations { get; set; } = new List<RentalRenewal>();

    public virtual ICollection<RentalRenewal> RentalRenewalReviewedByNavigations { get; set; } = new List<RentalRenewal>();

    public virtual ICollection<StaffFacilityAssignment> StaffFacilityAssignments { get; set; } = new List<StaffFacilityAssignment>();

    public virtual ICollection<StaffShift> StaffShifts { get; set; } = new List<StaffShift>();

    public virtual ICollection<StaffTask> StaffTasks { get; set; } = new List<StaffTask>();

    public virtual ICollection<TicketAssignment> TicketAssignments { get; set; } = new List<TicketAssignment>();

    public virtual ICollection<TicketAttachment> TicketAttachments { get; set; } = new List<TicketAttachment>();

    public virtual ICollection<TicketMessage> TicketMessages { get; set; } = new List<TicketMessage>();

    public virtual ICollection<UnitAllocation> UnitAllocations { get; set; } = new List<UnitAllocation>();

    public virtual ICollection<UnitStatusHistory> UnitStatusHistories { get; set; } = new List<UnitStatusHistory>();

    public virtual ICollection<UnitTransferRequest> UnitTransferRequestRequestedByNavigations { get; set; } = new List<UnitTransferRequest>();

    public virtual ICollection<UnitTransferRequest> UnitTransferRequestReviewedByNavigations { get; set; } = new List<UnitTransferRequest>();

    public virtual ICollection<UserRole> UserRoleGrantedByNavigations { get; set; } = new List<UserRole>();

    public virtual ICollection<UserRole> UserRoleUsers { get; set; } = new List<UserRole>();
}
