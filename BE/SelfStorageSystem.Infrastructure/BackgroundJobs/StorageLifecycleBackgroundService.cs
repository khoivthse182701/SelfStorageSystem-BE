using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;

namespace SelfStorageSystem.Infrastructure.BackgroundJobs;

/// <summary>
/// Background worker automatically handling storage lifecycle events by delegating each cycle to IStorageLifecycleService.
/// </summary>
public sealed class StorageLifecycleBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StorageLifecycleBackgroundService> _logger;

    public StorageLifecycleBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<StorageLifecycleBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool>("StorageLifecycleWorker:Enabled", true);
        if (!enabled)
        {
            _logger.LogInformation("StorageLifecycleBackgroundService is disabled via configuration.");
            return;
        }

        var intervalSeconds = _configuration.GetValue<int>("StorageLifecycleWorker:IntervalSeconds", 60);
        var interval = TimeSpan.FromSeconds(Math.Max(15, intervalSeconds));

        _logger.LogInformation("StorageLifecycleBackgroundService started (Interval: {IntervalSeconds}s).", interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessLifecycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during StorageLifecycleBackgroundService execution: {Message}", ex.Message);
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("StorageLifecycleBackgroundService stopped.");
    }

    private async Task ProcessLifecycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var lifecycleService = scope.ServiceProvider.GetService<IStorageLifecycleService>();
        if (lifecycleService == null)
        {
            var db = scope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
            var holdExpiryService = scope.ServiceProvider.GetService<IReservationHoldExpiryService>()
                ?? new ReservationHoldExpiryService(db);
            var notifier = scope.ServiceProvider.GetService<IRealtimeNotifier>();
            lifecycleService = new StorageLifecycleService(db, holdExpiryService, notifier);
        }

        await lifecycleService.RunCycleAsync(cancellationToken);
    }
}
