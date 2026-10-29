using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.BackgroundJobs;

/// <summary>Releases unpaid transfer holds and applies the agreed PA3 expiry calculation.</summary>
public sealed class TransferExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RentalSettings _rentalSettings;
    private readonly ILogger<TransferExpiryWorker> _logger;

    public TransferExpiryWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<RentalSettings> rentalOptions,
        ILogger<TransferExpiryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _rentalSettings = rentalOptions.Value ?? new RentalSettings();
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = Math.Max(1, _rentalSettings.TransferExpiryCheckIntervalMinutes);
        _logger.LogInformation("TransferExpiryWorker started (Interval: {Interval}m, Unpaid hold: {Hold}m)",
            intervalMinutes, _rentalSettings.TransferUnpaidHoldMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await SweepAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Transfer expiry sweep failed."); }

            try { await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("TransferExpiryWorker stopped.");
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var holdCutoff = now.AddMinutes(-Math.Max(1, _rentalSettings.TransferUnpaidHoldMinutes));

        var unpaid = await db.UnitTransferRequests
            .Include(t => t.ToUnit)
            .Where(t => t.Status == TransferRequestStatusConstants.PendingPayment && t.CreatedAt <= holdCutoff)
            .ToListAsync(cancellationToken);
        foreach (var transfer in unpaid)
        {
            transfer.Status = TransferRequestStatusConstants.Cancelled;
            if (transfer.ToUnit != null && transfer.ToUnit.PhysicalStatus == StorageUnitStatusConstants.Reserved)
            {
                transfer.ToUnit.PhysicalStatus = StorageUnitStatusConstants.Available;
                transfer.ToUnit.UpdatedAt = now;
            }
            var billingPeriod = $"{TransferConstants.BillingPeriodPrefix}{transfer.Id}";
            var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.BillingPeriod == billingPeriod && i.Status != InvoiceStatusConstants.Paid, cancellationToken);
            if (invoice != null) { invoice.Status = InvoiceStatusConstants.Voided; invoice.VoidedAt = now; invoice.UpdatedAt = now; }
        }

        var overdue = await db.UnitTransferRequests
            .Include(t => t.ToUnit)
            .Where(t => t.Status == TransferRequestStatusConstants.Paid && t.MoveOutDate < today)
            .ToListAsync(cancellationToken);
        foreach (var transfer in overdue)
        {
            transfer.Status = TransferRequestStatusConstants.CancelledOverdueMoveOut;
            transfer.PenaltyAmount = decimal.Round(transfer.DiffRentAmount * _rentalSettings.TransferOverduePenaltyRate, 0, MidpointRounding.AwayFromZero);
            transfer.CreditAmount = (transfer.DiffRentAmount - transfer.PenaltyAmount) + transfer.DiffDepositAmount;
            if (transfer.ToUnit != null && transfer.ToUnit.PhysicalStatus == StorageUnitStatusConstants.Reserved)
            {
                transfer.ToUnit.PhysicalStatus = StorageUnitStatusConstants.Available;
                transfer.ToUnit.UpdatedAt = now;
            }
        }

        if (unpaid.Count + overdue.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Expired {UnpaidCount} unpaid and {OverdueCount} overdue transfer requests.", unpaid.Count, overdue.Count);
        }
    }
}
