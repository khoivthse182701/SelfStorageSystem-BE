namespace SelfStorageSystem.Contracts.Customer.SupportTickets;

public record TicketAttachmentDto(
    long Id,
    long TicketId,
    long? MessageId,
    long UploadedBy,
    string FileName,
    string MimeType,
    long FileSizeBytes,
    string ObjectUrl,
    DateTimeOffset CreatedAt
);
