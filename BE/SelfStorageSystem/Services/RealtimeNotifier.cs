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
            await _hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveNotification", payload);
            _logger.LogInformation("Dispatched SignalR notification to user_{UserId}: {Title}", userId, title);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch SignalR notification to user_{UserId}", userId);
        }
    }

    public async Task SendTicketUpdateAsync(long facilityId, object ticketData)
    {
        try
        {
            await _hubContext.Clients.Group($"facility_{facilityId}").SendAsync("TicketUpdated", ticketData);
            await _hubContext.Clients.Group("role_facility_manager").SendAsync("TicketUpdated", ticketData);
            await _hubContext.Clients.Group("role_facility_staff").SendAsync("TicketUpdated", ticketData);
            _logger.LogInformation("Dispatched TicketUpdated to facility_{FacilityId}", facilityId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch TicketUpdated to facility_{FacilityId}", facilityId);
        }
    }

    public async Task SendTransferRequestUpdateAsync(long facilityId, object transferData)
    {
        try
        {
            await _hubContext.Clients.Group($"facility_{facilityId}").SendAsync("TransferRequestUpdated", transferData);
            await _hubContext.Clients.Group("role_facility_manager").SendAsync("TransferRequestUpdated", transferData);
            _logger.LogInformation("Dispatched TransferRequestUpdated to facility_{FacilityId}", facilityId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch TransferRequestUpdated to facility_{FacilityId}", facilityId);
        }
    }

    public async Task SendTaskAssignmentAsync(long staffId, object taskData)
    {
        try
        {
            await _hubContext.Clients.Group($"user_{staffId}").SendAsync("TaskAssigned", taskData);
            _logger.LogInformation("Dispatched TaskAssigned to user_{StaffId}", staffId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch TaskAssigned to user_{StaffId}", staffId);
        }
    }

    public async Task SendRefundUpdateAsync(long customerId, object refundData)
    {
        try
        {
            await _hubContext.Clients.Group($"user_{customerId}").SendAsync("RefundUpdated", refundData);
            await _hubContext.Clients.Group("role_facility_manager").SendAsync("RefundUpdated", refundData);
            _logger.LogInformation("Dispatched RefundUpdated to user_{CustomerId}", customerId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch RefundUpdated to user_{CustomerId}", customerId);
        }
    }

    public async Task SendUnitStatusChangeAsync(long facilityId, object unitData)
    {
        try
        {
            await _hubContext.Clients.Group($"facility_{facilityId}").SendAsync("UnitStatusChanged", unitData);
            await _hubContext.Clients.Group("role_facility_manager").SendAsync("UnitStatusChanged", unitData);
            _logger.LogInformation("Dispatched UnitStatusChanged to facility_{FacilityId}", facilityId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch UnitStatusChanged to facility_{FacilityId}", facilityId);
        }
    }

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
}
