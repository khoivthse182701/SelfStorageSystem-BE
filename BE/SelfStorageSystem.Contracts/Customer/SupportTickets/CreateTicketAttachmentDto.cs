namespace SelfStorageSystem.Contracts.Customer.SupportTickets;

public record CreateTicketAttachmentDto(
    string FileName,
    string MimeType,
    long FileSizeBytes,
    string ObjectUrl,
    string? Sha256 = null
);
