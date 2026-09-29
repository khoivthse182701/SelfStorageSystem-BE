using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.BackgroundJobs;

public class ReservationHoldExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReservationSettings _settings;
    private readonly ILogger<ReservationHoldExpiryWorker> _logger;

    public ReservationHoldExpiryWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ReservationSettings> options,
        ILogger<ReservationHoldExpiryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReservationHoldExpiryWorker started (Interval: {Interval}s, Max hold: {Hold}m per BR-RSV-01)",
            _settings.ExpiryCheckIntervalSeconds, _settings.HoldDurationMinutes);

        var interval = TimeSpan.FromSeconds(Math.Max(10, _settings.ExpiryCheckIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReleaseExpiredReservationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while sweeping expired reservations: {Message}", ex.Message);
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

        _logger.LogInformation("ReservationHoldExpiryWorker stopped.");
    }

    private async Task ReleaseExpiredReservationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();

        var now = DateTimeOffset.UtcNow;
        var graceCutoff = now.AddMinutes(-Math.Max(0, _settings.HoldGracePeriodMinutes));

        var expiredReservations = await dbContext.Reservations
            .AsSplitQuery()
            .Include(r => r.UnitAllocation)
                .ThenInclude(ua => ua!.StorageUnit)
            .Include(r => r.Invoices)
            .Include(r => r.PromotionRedemptions)
            .Where(r => (r.Status == ReservationStatusConstants.Pending || r.Status == ReservationStatusConstants.AwaitingDeposit) && r.HoldUntil < graceCutoff)
            .OrderBy(r => r.HoldUntil)
            .Take(50) // Batch 50 items per check
            .ToListAsync(cancellationToken);

        if (expiredReservations.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Detected {Count} reservations exceeding hold period + {Grace}m grace period. Releasing units to 'available' per BR-RSV-01...", 
            expiredReservations.Count, _settings.HoldGracePeriodMinutes);

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        if (dbContext.Database.IsSqlServer())
        {
            transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        try
        {
            foreach (var r in expiredReservations)
            {
                // Atomic status check: if already confirmed by payment webhook on another node, skip
                var latestStatus = await dbContext.Reservations
                    .Where(x => x.Id == r.Id)
                    .Select(x => x.Status)
                    .FirstOrDefaultAsync(cancellationToken);

                if (latestStatus != ReservationStatusConstants.Pending && latestStatus != ReservationStatusConstants.AwaitingDeposit)
                {
                    _logger.LogInformation("Reservation {ReservationCode} transitioned to {Status}; skipping auto-expiration.", r.ReservationCode, latestStatus);
                    continue;
                }

                r.Status = ReservationStatusConstants.Expired;
                r.UpdatedAt = now;

                if (r.UnitAllocation != null)
                {
                    r.UnitAllocation.Status = AllocationStatusConstants.Expired;
                    r.UnitAllocation.EndedAt = now;

                    if (r.UnitAllocation.StorageUnit != null)
                    {
                        r.UnitAllocation.StorageUnit.PhysicalStatus = StorageUnitStatusConstants.Available;
                        r.UnitAllocation.StorageUnit.UpdatedAt = now;
                    }
                }

                foreach (var inv in r.Invoices.Where(i => i.Status == InvoiceStatusConstants.Open || i.Status == InvoiceStatusConstants.Draft))
                {
                    inv.Status = InvoiceStatusConstants.Voided;
                    inv.VoidedAt = now;
                    inv.UpdatedAt = now;
                }

                foreach (var red in r.PromotionRedemptions.Where(p => p.Status == PromotionRedemptionStatusConstants.Reserved))
                {
                    red.Status = PromotionRedemptionStatusConstants.Released;
                }

                _logger.LogInformation("Reservation {ReservationCode} has expired. Storage unit unlocked and released.", r.ReservationCode);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            _logger.LogError(ex, "Error releasing expired reservations in transaction: {Message}", ex.Message);
            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }
}
