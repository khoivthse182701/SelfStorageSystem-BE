using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class DelinquencyAction
{
    public long Id { get; set; }

    public long DelinquencyCaseId { get; set; }

    public string ActionType { get; set; } = null!;

    public long? PerformedBy { get; set; }

    public string Details { get; set; } = null!;

    public DateTimeOffset OccurredAt { get; set; }

    public virtual DelinquencyCase DelinquencyCase { get; set; } = null!;

    public virtual User? PerformedByNavigation { get; set; }
}
