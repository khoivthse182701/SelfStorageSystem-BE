using Microsoft.AspNetCore.SignalR;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Hubs;

namespace SelfStorageSystem.Services;

public class RealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<RealtimeNotifier> _logger;

    public RealtimeNotifier(IHubContext<NotificationHub> hubContext, ILogger<RealtimeNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendNotificationToUserAsync(long userId, string title, string message, object? extra = null)
    {
        try
        {
            var payload = new
            {
                title,
                message,
                extra,
                timestamp = DateTimeOffset.UtcNow
            };
            await _hubContext.Clients.Group($"user_{userId}").SendAsync("NotificationReceived", payload);
            _logger.LogInformation("Dispatched SignalR notification to user_{UserId}: {Title}", userId, title);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch SignalR notification to user_{UserId}", userId);
        }
    }

    public async Task SendPaymentPartialReceivedAsync(long userId, object payload) =>
        await SafeSendAsync($"user_{userId}", "PaymentPartialReceived", payload);

    public async Task SendPaymentSucceededAsync(long userId, object payload) =>
        await SafeSendAsync($"user_{userId}", "PaymentSucceeded", payload);

    public async Task SendReservationConfirmedAsync(long userId, long facilityId, object payload)
    {
        await SafeSendAsync($"user_{userId}", "ReservationConfirmed", payload);
        await SafeSendAsync($"facility_{facilityId}", "ReservationConfirmed", payload);
    }

    public async Task SendTicketUpdateAsync(long facilityId, object ticketData) =>
        await SafeSendAsync($"facility_{facilityId}", "TicketUpdated", ticketData);

    public async Task SendTransferRequestUpdateAsync(long facilityId, object transferData) =>
        await SafeSendAsync($"facility_{facilityId}", "TransferRequestUpdated", transferData);

    public async Task SendTaskAssignmentAsync(long staffId, object taskData) =>
        await SafeSendAsync($"user_{staffId}", "TaskAssigned", taskData);

    public async Task SendRefundUpdateAsync(long customerId, object refundData) =>
        await SafeSendAsync($"user_{customerId}", "RefundUpdated", refundData);

    public async Task SendUnitStatusChangeAsync(long facilityId, object unitData) =>
        await SafeSendAsync($"facility_{facilityId}", "UnitStatusChanged", unitData);

    public async Task BroadcastAsync(string eventName, object payload)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync(eventName, payload);
            _logger.LogInformation("Dispatched broadcast SignalR event {EventName}", eventName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch broadcast SignalR event {EventName}", eventName);
        }
    }

    private async Task SafeSendAsync(string groupName, string eventName, object payload)
    {
        try
        {
            await _hubContext.Clients.Group(groupName).SendAsync(eventName, payload);
            _logger.LogInformation("Dispatched SignalR {EventName} to {GroupName}", eventName, groupName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch SignalR {EventName} to {GroupName}", eventName, groupName);
        }
    }
}
