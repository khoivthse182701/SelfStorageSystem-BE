using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class IntegrationEvent
{
    public long Id { get; set; }

    public string Source { get; set; } = null!;

    public string ExternalEventId { get; set; } = null!;

    public string EventType { get; set; } = null!;

    public string Payload { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? ErrorMessage { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }
}
