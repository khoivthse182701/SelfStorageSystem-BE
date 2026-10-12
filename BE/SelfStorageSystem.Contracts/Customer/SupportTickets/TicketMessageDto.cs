namespace SelfStorageSystem.Contracts.Customer.SupportTickets;

public record TicketMessageDto(
    long Id,
    long TicketId,
    long? AuthorUserId,
    string? AuthorName,
    string? AuthorRole,
    string Body,
    DateTimeOffset CreatedAt,
    List<TicketAttachmentDto>? Attachments = null
);
