using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class HandoverRecord
{
    public long Id { get; set; }

    public long AgreementId { get; set; }

    public long UnitAllocationId { get; set; }

    public long InspectionId { get; set; }

    public long HandledBy { get; set; }

    public string HandoverType { get; set; } = null!;

    public string? CustomerSignatureRef { get; set; }

    public string? StaffSignatureRef { get; set; }

    public DateTimeOffset? CustomerSignedAt { get; set; }

    public DateTimeOffset? StaffSignedAt { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual RentalAgreement Agreement { get; set; } = null!;

    public virtual EmployeeProfile HandledByNavigation { get; set; } = null!;

    public virtual Inspection Inspection { get; set; } = null!;

    public virtual UnitAllocation UnitAllocation { get; set; } = null!;
}
