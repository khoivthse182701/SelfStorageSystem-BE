using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class ServiceRating
{
    public long TicketId { get; set; }

    public long CustomerId { get; set; }

    public short Score { get; set; }

    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual CustomerProfile Customer { get; set; } = null!;

    public virtual SupportTicket Ticket { get; set; } = null!;
}
