using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class UserRole
{
    public long UserId { get; set; }

    public short RoleId { get; set; }

    public long? GrantedBy { get; set; }

    public DateTimeOffset GrantedAt { get; set; }

    public virtual User? GrantedByNavigation { get; set; }

    public virtual Role Role { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
