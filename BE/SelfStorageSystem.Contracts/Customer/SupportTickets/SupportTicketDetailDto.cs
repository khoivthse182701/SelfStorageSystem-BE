namespace SelfStorageSystem.Contracts.Customer.SupportTickets;

public record SupportTicketDetailDto(
    long Id,
    string TicketNo,
    long FacilityId,
    string FacilityName,
    long? AgreementId,
    long? StorageUnitId,
    string? UnitName,
    string Category,
    string Priority,
    string Subject,
    string Description,
    string Status,
    string DisplayStatus,
    string? Resolution,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    List<TicketAttachmentDto> InitialAttachments,
    List<TicketMessageDto> Messages,
    ServiceRatingDto? Rating
);
