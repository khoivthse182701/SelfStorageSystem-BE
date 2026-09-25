using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class LoginHistory
{
    public long Id { get; set; }

    public long? UserId { get; set; }

    public string? AttemptedEmail { get; set; }

    public string Result { get; set; } = null!;

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public virtual User? User { get; set; }
}
