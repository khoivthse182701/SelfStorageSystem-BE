using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class AccessPoint
{
    public long Id { get; set; }

    public long FacilityId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string AccessPointType { get; set; } = null!;

    public string? ExternalDeviceCode { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<AccessEvent> AccessEvents { get; set; } = new List<AccessEvent>();

    public virtual Facility Facility { get; set; } = null!;
}
