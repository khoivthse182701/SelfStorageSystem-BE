using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class Role
{
    public short Id { get; set; }

    public string Code { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
