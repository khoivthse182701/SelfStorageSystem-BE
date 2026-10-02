namespace SelfStorageSystem.Contracts.Customer.SupportTickets;

public record SupportTicketSummaryDto(
    long Id,
    string TicketNo,
    long FacilityId,
    string FacilityName,
    string Category,
    string Priority,
    string Subject,
    string Status,
    string DisplayStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    short? RatingScore
);
