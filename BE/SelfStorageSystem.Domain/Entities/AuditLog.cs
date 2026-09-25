using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class AuditLog
{
    public long Id { get; set; }

    public long? ActorUserId { get; set; }

    public string? ActorRole { get; set; }

    public string EntitySchema { get; set; } = null!;

    public string EntityType { get; set; } = null!;

    public string EntityId { get; set; } = null!;

    public string Action { get; set; } = null!;

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? RequestId { get; set; }

    public string? IpAddress { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public virtual User? ActorUser { get; set; }
}
