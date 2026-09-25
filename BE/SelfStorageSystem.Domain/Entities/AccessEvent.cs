using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class AccessEvent
{
    public long Id { get; set; }

    public long? CredentialId { get; set; }

    public long AccessPointId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public string Result { get; set; } = null!;

    public string? Reason { get; set; }

    public string? ExternalEventId { get; set; }

    public string Metadata { get; set; } = null!;

    public virtual AccessPoint AccessPoint { get; set; } = null!;

    public virtual AccessCredential? Credential { get; set; }
}
