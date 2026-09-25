using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class FacilityArea
{
    public long Id { get; set; }

    public long FacilityId { get; set; }

    public long? ParentAreaId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string AreaType { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public string MapMetadata { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual Facility Facility { get; set; } = null!;

    public virtual FacilityArea? FacilityAreaNavigation { get; set; }

    public virtual ICollection<FacilityArea> InverseFacilityAreaNavigation { get; set; } = new List<FacilityArea>();

    public virtual ICollection<FacilityArea> InverseParentArea { get; set; } = new List<FacilityArea>();

    public virtual FacilityArea? ParentArea { get; set; }

    public virtual ICollection<StorageUnit> StorageUnits { get; set; } = new List<StorageUnit>();

    public virtual ICollection<UnitMapPosition> UnitMapPositions { get; set; } = new List<UnitMapPosition>();
}
