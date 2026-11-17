using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Hubs;

[Authorize]
public class NotificationHub(SelfStorageDbContext dbContext, ILogger<NotificationHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var userId = ResolveUserId();

        if (userId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId.Value}");
            logger.LogInformation("SignalR client {ConnectionId} registered to group user_{UserId}", Context.ConnectionId, userId.Value);
        }

        if (httpContext?.Request.Query.TryGetValue("facilityId", out var facilityIdVal) == true &&
            !string.IsNullOrWhiteSpace(facilityIdVal))
        {
            if (!long.TryParse(facilityIdVal, out var facilityId))
                throw new HubException("Invalid facilityId parameter.");

            await EnsureFacilityAccessAsync(facilityId);
            await Groups.AddToGroupAsync(Context.ConnectionId, $"facility_{facilityId}");
            logger.LogInformation("SignalR client {ConnectionId} joined group facility_{FacilityId}", Context.ConnectionId, facilityId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogDebug("SignalR client {ConnectionId} disconnected.", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinFacility(long facilityId)
    {
        await EnsureFacilityAccessAsync(facilityId);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"facility_{facilityId}");
        logger.LogInformation("SignalR client {ConnectionId} joined facility_{FacilityId}", Context.ConnectionId, facilityId);
    }

    public async Task LeaveFacility(long facilityId)
    {
        if (facilityId <= 0) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"facility_{facilityId}");
        logger.LogInformation("SignalR client {ConnectionId} left facility_{FacilityId}", Context.ConnectionId, facilityId);
    }

    public Task<string> Ping() => Task.FromResult("pong");

    private long? ResolveUserId()
    {
        var raw = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? Context.User?.FindFirst("sub")?.Value
                  ?? Context.User?.FindFirst("userId")?.Value;
        return long.TryParse(raw, out var id) ? id : null;
    }

    private async Task EnsureFacilityAccessAsync(long facilityId)
    {
        if (facilityId <= 0)
            throw new HubException("Facility ID must be positive.");

        var user = Context.User;
        var userId = ResolveUserId();
        var isOperational = user != null && (
            user.IsInRole("Staff") ||
            user.IsInRole("Manager") ||
            user.IsInRole(RoleConstants.FacilityStaff) ||
            user.IsInRole(RoleConstants.FacilityManager));

        if (!userId.HasValue || !isOperational)
            throw new HubException("Only assigned Staff or Manager users can subscribe to facility updates.");

        var now = DateTimeOffset.UtcNow;
        var hasAssignment = await dbContext.StaffFacilityAssignments
            .AsNoTracking()
            .AnyAsync(a => a.EmployeeId == userId.Value
                        && a.FacilityId == facilityId
                        && a.StartsAt <= now
                        && (a.EndsAt == null || a.EndsAt > now),
                Context.ConnectionAborted);

        if (!hasAssignment)
            throw new HubException($"User is not actively assigned to facility {facilityId}.");
    }
}
