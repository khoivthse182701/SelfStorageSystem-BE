namespace SelfStorageSystem.Application.Interfaces;

public interface IStorageLifecycleService
{
    Task RunCycleAsync(CancellationToken cancellationToken = default);
}
