namespace SelfStorageSystem.Contracts.Customer.SupportTickets;

public record CreateSupportTicketRequest(
    long FacilityId,
    long? AgreementId,
    long? StorageUnitId,
    string Category,
    string? Priority,
    string Subject,
    string Description,
    List<CreateTicketAttachmentDto>? Attachments = null
);
