using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class Notification
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string Channel { get; set; } = null!;

    public string TemplateCode { get; set; } = null!;

    public string? Subject { get; set; }

    public string Payload { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTimeOffset ScheduledAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public short Attempts { get; set; }

    public string? LastError { get; set; }

    public string? DeduplicationKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
