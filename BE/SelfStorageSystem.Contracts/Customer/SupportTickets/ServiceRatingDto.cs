namespace SelfStorageSystem.Contracts.Customer.SupportTickets;

public record ServiceRatingDto(
    long TicketId,
    long CustomerId,
    short Score,
    string? Comment,
    DateTimeOffset CreatedAt
);
