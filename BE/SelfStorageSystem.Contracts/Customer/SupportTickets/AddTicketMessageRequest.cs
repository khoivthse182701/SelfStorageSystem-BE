namespace SelfStorageSystem.Contracts.Customer.SupportTickets;

public record AddTicketMessageRequest(
    string Body,
    List<CreateTicketAttachmentDto>? Attachments = null
);
