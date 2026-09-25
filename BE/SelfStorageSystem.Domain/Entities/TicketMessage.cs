using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class TicketMessage
{
    public long Id { get; set; }

    public long TicketId { get; set; }

    public long? AuthorUserId { get; set; }

    public string Body { get; set; } = null!;

    public bool IsInternal { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual User? AuthorUser { get; set; }

    public virtual SupportTicket Ticket { get; set; } = null!;

    public virtual ICollection<TicketAttachment> TicketAttachmentMessages { get; set; } = new List<TicketAttachment>();

    public virtual ICollection<TicketAttachment> TicketAttachmentTicketMessages { get; set; } = new List<TicketAttachment>();
}
