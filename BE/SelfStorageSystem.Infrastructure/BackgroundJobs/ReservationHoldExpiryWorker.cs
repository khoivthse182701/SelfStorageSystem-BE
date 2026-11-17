using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;

namespace SelfStorageSystem.Infrastructure.BackgroundJobs;

public class ReservationHoldExpiryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ReservationSettings> options,
    ILogger<ReservationHoldExpiryWorker> logger) : BackgroundService
{
    private readonly ReservationSettings _settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("ReservationHoldExpiryWorker started (Interval: {Interval}s, Max hold: {Hold}m per BR-RSV-01)",
                _settings.ExpiryCheckIntervalSeconds, _settings.HoldDurationMinutes);

            var interval = TimeSpan.FromSeconds(Math.Max(10, _settings.ExpiryCheckIntervalSeconds));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var expiryService = scope.ServiceProvider.GetRequiredService<IReservationHoldExpiryService>();
                    await expiryService.ReleaseExpiredHoldsAsync(respectGracePeriod: true, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error occurred while sweeping expired reservations: {Message}", ex.Message);
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

            logger.LogInformation("ReservationHoldExpiryWorker stopped.");
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("ReservationHoldExpiryWorker canceled.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "FATAL unhandled exception in ReservationHoldExpiryWorker: {Message}", ex.Message);
        }
    }
}
