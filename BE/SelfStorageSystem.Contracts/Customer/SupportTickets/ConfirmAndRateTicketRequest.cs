namespace SelfStorageSystem.Contracts.Customer.SupportTickets;

public record ConfirmAndRateTicketRequest(
    short Score,
    string? Comment = null
);
