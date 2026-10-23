using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.BackgroundJobs;

/// <summary>Releases unpaid transfer holds and applies the agreed PA3 expiry calculation.</summary>
public sealed class TransferExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TransferExpiryWorker> _logger;

    public TransferExpiryWorker(IServiceScopeFactory scopeFactory, ILogger<TransferExpiryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await SweepAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Transfer expiry sweep failed."); }

            try { await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var unpaid = await db.UnitTransferRequests
            .Include(t => t.ToUnit)
            .Where(t => t.Status == TransferRequestStatusConstants.PendingPayment && t.CreatedAt <= now.AddMinutes(-15))
            .ToListAsync(cancellationToken);
        foreach (var transfer in unpaid)
        {
            transfer.Status = TransferRequestStatusConstants.Cancelled;
            if (transfer.ToUnit != null && transfer.ToUnit.PhysicalStatus == StorageUnitStatusConstants.Reserved)
            {
                transfer.ToUnit.PhysicalStatus = StorageUnitStatusConstants.Available;
                transfer.ToUnit.UpdatedAt = now;
            }
            var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.BillingPeriod == $"TRANSFER:{transfer.Id}" && i.Status != InvoiceStatusConstants.Paid, cancellationToken);
            if (invoice != null) { invoice.Status = InvoiceStatusConstants.Voided; invoice.VoidedAt = now; invoice.UpdatedAt = now; }
        }

        var overdue = await db.UnitTransferRequests
            .Include(t => t.ToUnit)
            .Where(t => t.Status == TransferRequestStatusConstants.Paid && t.MoveOutDate < today)
            .ToListAsync(cancellationToken);
        foreach (var transfer in overdue)
        {
            transfer.Status = TransferRequestStatusConstants.CancelledOverdueMoveOut;
            transfer.PenaltyAmount = decimal.Round(transfer.DiffRentAmount * 0.5m, 0, MidpointRounding.AwayFromZero);
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
