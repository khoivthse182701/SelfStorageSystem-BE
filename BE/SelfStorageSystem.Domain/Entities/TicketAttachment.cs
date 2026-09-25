using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class TicketAttachment
{
    public long Id { get; set; }

    public long TicketId { get; set; }

    public long? MessageId { get; set; }

    public long UploadedBy { get; set; }

    public string FileName { get; set; } = null!;

    public string MimeType { get; set; } = null!;

    public long FileSizeBytes { get; set; }

    public string ObjectUrl { get; set; } = null!;

    public string? Sha256 { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual TicketMessage? Message { get; set; }

    public virtual SupportTicket Ticket { get; set; } = null!;

    public virtual TicketMessage? TicketMessage { get; set; }

    public virtual User UploadedByNavigation { get; set; } = null!;
}
