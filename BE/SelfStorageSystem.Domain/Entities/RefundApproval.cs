using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class RefundApproval
{
    public long RefundId { get; set; }

    public string Decision { get; set; } = null!;

    public long DecidedBy { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset DecidedAt { get; set; }

    public virtual EmployeeProfile DecidedByNavigation { get; set; } = null!;

    public virtual Refund Refund { get; set; } = null!;
}
