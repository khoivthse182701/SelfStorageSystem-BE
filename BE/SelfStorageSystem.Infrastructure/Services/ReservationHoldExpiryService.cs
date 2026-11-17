using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public sealed class ReservationHoldExpiryService(
    SelfStorageDbContext dbContext,
    IOptions<ReservationSettings>? reservationOptions = null,
    ILogger<ReservationHoldExpiryService>? logger = null) : IReservationHoldExpiryService
{
    private readonly ReservationSettings _settings = reservationOptions?.Value ?? new ReservationSettings();

    public async Task<int> ReleaseExpiredHoldsAsync(bool respectGracePeriod = true, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = respectGracePeriod
            ? now.AddMinutes(-Math.Max(0, _settings.HoldGracePeriodMinutes))
            : now;

        var expiredReservations = await dbContext.Reservations
            .AsSplitQuery()
            .Include(r => r.UnitAllocation)
                .ThenInclude(ua => ua!.StorageUnit)
            .Include(r => r.Invoices)
            .Include(r => r.PromotionRedemptions)
            .Where(r => (r.Status == ReservationStatusConstants.Pending || r.Status == ReservationStatusConstants.AwaitingDeposit)
                     && r.HoldUntil < cutoff)
            .OrderBy(r => r.HoldUntil)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (expiredReservations.Count == 0)
            return 0;

        IDbContextTransaction? transaction = dbContext.Database.IsSqlServer()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var releasedCount = 0;

            foreach (var r in expiredReservations)
            {
                var latestStatus = await dbContext.Reservations
                    .Where(x => x.Id == r.Id)
                    .Select(x => x.Status)
                    .FirstOrDefaultAsync(cancellationToken);

                if (latestStatus != ReservationStatusConstants.Pending &&
                    latestStatus != ReservationStatusConstants.AwaitingDeposit)
                {
                    continue;
                }

                r.Status = ReservationStatusConstants.Expired;
                r.UpdatedAt = now;

                if (r.UnitAllocation != null)
                {
                    r.UnitAllocation.Status = AllocationStatusConstants.Released;
                    r.UnitAllocation.EndedAt = now;

                    if (r.UnitAllocation.StorageUnit != null &&
                        r.UnitAllocation.StorageUnit.PhysicalStatus == StorageUnitStatusConstants.Reserved)
                    {
                        var unitId = r.UnitAllocation.StorageUnit.Id;
                        var allocId = r.UnitAllocation.Id;
                        var hasOtherActiveAllocation = await dbContext.UnitAllocations
                            .AnyAsync(ua => ua.StorageUnitId == unitId &&
                                            ua.Id != allocId &&
                                            ua.Status == AllocationStatusConstants.Active &&
                                            (ua.AgreementId != null ||
                                             (ua.Reservation != null &&
                                              (ua.Reservation.Status == ReservationStatusConstants.Confirmed ||
                                               ua.Reservation.Status == ReservationStatusConstants.Converted ||
                                               ((ua.Reservation.Status == ReservationStatusConstants.Pending ||
                                                 ua.Reservation.Status == ReservationStatusConstants.AwaitingDeposit) &&
                                                ua.Reservation.HoldUntil > now)))),
                                cancellationToken);

                        var hasOpenMaintenance = await dbContext.MaintenanceWorkOrders
                            .AnyAsync(m => m.StorageUnitId == unitId &&
                                           m.Status != "completed" &&
                                           m.Status != "cancelled",
                                cancellationToken);

                        if (!hasOtherActiveAllocation && !hasOpenMaintenance)
                        {
                            var oldStatus = r.UnitAllocation.StorageUnit.PhysicalStatus;
                            r.UnitAllocation.StorageUnit.PhysicalStatus = StorageUnitStatusConstants.Available;
                            r.UnitAllocation.StorageUnit.UpdatedAt = now;

                            dbContext.UnitStatusHistories.Add(new UnitStatusHistory
                            {
                                StorageUnitId = unitId,
                                OldStatus = oldStatus,
                                NewStatus = StorageUnitStatusConstants.Available,
                                Reason = $"Reservation hold expired ({r.ReservationCode}).",
                                ChangedAt = now
                            });
                        }
                    }
                }

                foreach (var inv in r.Invoices.Where(i =>
                    (i.Status == InvoiceStatusConstants.Open || i.Status == InvoiceStatusConstants.Draft) &&
                    i.PaidAmount == 0))
                {
                    inv.Status = InvoiceStatusConstants.Voided;
                    inv.VoidedAt = now;
                    inv.UpdatedAt = now;
                }

                foreach (var red in r.PromotionRedemptions.Where(p => p.Status == PromotionRedemptionStatusConstants.Reserved))
                {
                    red.Status = PromotionRedemptionStatusConstants.Released;
                }

                releasedCount++;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction != null)
                await transaction.CommitAsync(cancellationToken);

            if (releasedCount > 0)
                logger?.LogInformation("Released {Count} expired reservation holds.", releasedCount);

            return releasedCount;
        }
        catch (Exception ex)
        {
            if (transaction != null)
                await transaction.RollbackAsync(cancellationToken);

            logger?.LogError(ex, "Error releasing expired reservations: {Message}", ex.Message);
            throw;
        }
        finally
        {
            if (transaction != null)
                await transaction.DisposeAsync();
        }
    }
}
