using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class TicketChargeApproval
{
    public long ProposalId { get; set; }

    public string Decision { get; set; } = null!;

    public long DecidedBy { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset DecidedAt { get; set; }

    public virtual EmployeeProfile DecidedByNavigation { get; set; } = null!;

    public virtual TicketChargeProposal Proposal { get; set; } = null!;
}
