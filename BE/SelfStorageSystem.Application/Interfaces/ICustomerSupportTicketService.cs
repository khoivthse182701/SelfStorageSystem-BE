using SelfStorageSystem.Contracts.Customer.SupportTickets;

namespace SelfStorageSystem.Application.Interfaces;

public interface ICustomerSupportTicketService
{
    Task<SupportTicketSummaryDto> CreateTicketAsync(
        long customerId, 
        CreateSupportTicketRequest request, 
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupportTicketSummaryDto>> GetCustomerTicketsAsync(
        long customerId, 
        string? status = null, 
        CancellationToken cancellationToken = default);

    Task<SupportTicketDetailDto> GetTicketDetailAsync(
        long customerId, 
        long ticketId, 
        CancellationToken cancellationToken = default);

    Task<TicketMessageDto> AddMessageAsync(
        long customerId, 
        long ticketId, 
        AddTicketMessageRequest request, 
        CancellationToken cancellationToken = default);

    Task ConfirmAndRateTicketAsync(
        long customerId, 
        long ticketId, 
        ConfirmAndRateTicketRequest request, 
        CancellationToken cancellationToken = default);
}
