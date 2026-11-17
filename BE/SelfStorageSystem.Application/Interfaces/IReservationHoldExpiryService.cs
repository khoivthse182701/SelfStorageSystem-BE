namespace SelfStorageSystem.Application.Interfaces;

public interface IReservationHoldExpiryService
{
    Task<int> ReleaseExpiredHoldsAsync(bool respectGracePeriod = true, CancellationToken cancellationToken = default);
}
