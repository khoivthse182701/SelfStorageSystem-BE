using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace SelfStorageSystem.Hubs;

public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? Context.User?.FindFirst("sub")?.Value
                     ?? Context.User?.FindFirst("userId")?.Value;

        var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value
                   ?? Context.User?.FindFirst("role")?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation("SignalR client {ConnectionId} registered to group user_{UserId}", Context.ConnectionId, userId);
        }

        if (!string.IsNullOrEmpty(role))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"role_{role.ToLowerInvariant()}");
            _logger.LogInformation("SignalR client {ConnectionId} registered to group role_{Role}", Context.ConnectionId, role);
        }

        // Allow query param facilityId upon initial connection
        if (httpContext?.Request.Query.TryGetValue("facilityId", out var facilityIdVal) == true && !string.IsNullOrWhiteSpace(facilityIdVal))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"facility_{facilityIdVal}");
            _logger.LogInformation("SignalR client {ConnectionId} auto-joined group facility_{FacilityId}", Context.ConnectionId, facilityIdVal);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("SignalR client {ConnectionId} disconnected.", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinFacility(string facilityId)
    {
        if (!string.IsNullOrWhiteSpace(facilityId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"facility_{facilityId}");
            _logger.LogInformation("SignalR client {ConnectionId} joined facility_{FacilityId}", Context.ConnectionId, facilityId);
        }
    }

    public async Task LeaveFacility(string facilityId)
    {
        if (!string.IsNullOrWhiteSpace(facilityId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"facility_{facilityId}");
            _logger.LogInformation("SignalR client {ConnectionId} left facility_{FacilityId}", Context.ConnectionId, facilityId);
        }
    }

    public Task<string> Ping()
    {
        return Task.FromResult("pong");
    }
}
