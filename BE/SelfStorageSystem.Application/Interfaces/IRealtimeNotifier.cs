using System.Threading.Tasks;

namespace SelfStorageSystem.Application.Interfaces;

public interface IRealtimeNotifier
{
    Task SendNotificationToUserAsync(long userId, string title, string message, object? extra = null);
    Task SendTicketUpdateAsync(long facilityId, object ticketData);
    Task SendTransferRequestUpdateAsync(long facilityId, object transferData);
    Task SendTaskAssignmentAsync(long staffId, object taskData);
    Task SendRefundUpdateAsync(long customerId, object refundData);
    Task SendUnitStatusChangeAsync(long facilityId, object unitData);
    Task BroadcastAsync(string eventName, object payload);
}
