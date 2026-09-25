using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class EmployeeProfile
{
    public long UserId { get; set; }

    public string EmployeeCode { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public DateOnly HireDate { get; set; }

    public string EmploymentStatus { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public virtual ICollection<AccessCredential> AccessCredentials { get; set; } = new List<AccessCredential>();

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<HandoverRecord> HandoverRecords { get; set; } = new List<HandoverRecord>();

    public virtual ICollection<IdentityVerification> IdentityVerifications { get; set; } = new List<IdentityVerification>();

    public virtual ICollection<Inspection> Inspections { get; set; } = new List<Inspection>();

    public virtual ICollection<MaintenanceWorkOrder> MaintenanceWorkOrderAssignedEmployees { get; set; } = new List<MaintenanceWorkOrder>();

    public virtual ICollection<MaintenanceWorkOrder> MaintenanceWorkOrderVerifiedByNavigations { get; set; } = new List<MaintenanceWorkOrder>();

    public virtual ICollection<RefundApproval> RefundApprovals { get; set; } = new List<RefundApproval>();

    public virtual ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();

    public virtual ICollection<StaffFacilityAssignment> StaffFacilityAssignments { get; set; } = new List<StaffFacilityAssignment>();

    public virtual ICollection<StaffTask> StaffTasks { get; set; } = new List<StaffTask>();

    public virtual ICollection<TicketAssignment> TicketAssignments { get; set; } = new List<TicketAssignment>();

    public virtual ICollection<TicketChargeApproval> TicketChargeApprovals { get; set; } = new List<TicketChargeApproval>();

    public virtual ICollection<TicketChargeProposal> TicketChargeProposals { get; set; } = new List<TicketChargeProposal>();

    public virtual User User { get; set; } = null!;
}
