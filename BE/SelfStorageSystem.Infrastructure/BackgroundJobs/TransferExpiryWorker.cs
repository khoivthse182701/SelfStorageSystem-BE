using SelfStorageSystem.Infrastructure.Common;
using SelfStorageSystem.Domain.Entities;
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
        try
        {
            var intervalMinutes = Math.Max(1, _rentalSettings.TransferExpiryCheckIntervalMinutes);
            _logger.LogInformation("TransferExpiryWorker started (Interval: {Interval}m, Unpaid hold: {Hold}m)",
                intervalMinutes, _rentalSettings.TransferUnpaidHoldMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SweepAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Transfer expiry sweep failed: {Message}", ex.Message);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("TransferExpiryWorker stopped.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("TransferExpiryWorker canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "FATAL unhandled exception in TransferExpiryWorker: {Message}", ex.Message);
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneHelper.ResolveTimeZone(_rentalSettings.DefaultTimezone)).DateTime);
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

        // 3. Stalled in-transit transfers past 48h after MoveOutDate (warn destination facility manager)
        var inTransitCutoff = today.AddDays(-2);
        var stalledTransfers = await db.UnitTransferRequests
            .Include(t => t.ToUnit)
            .Include(t => t.Agreement)
            .Where(t => t.Status == TransferRequestStatusConstants.MovedOutInTransit && t.MoveOutDate <= inTransitCutoff)
            .ToListAsync(cancellationToken);

        int alertedCount = 0;
        foreach (var transfer in stalledTransfers)
        {
            var destinationFacilityId = transfer.RequestedFacilityId > 0 ? transfer.RequestedFacilityId : (transfer.ToUnit?.FacilityId ?? transfer.Agreement?.FacilityId ?? 0L);
            var alertKey = $"[ALERT-TRANSIT-STALLED-{transfer.Id}]";
            var alreadyAlerted = await db.SupportTickets.AnyAsync(st => st.Subject.Contains(alertKey), cancellationToken);

            if (!alreadyAlerted && destinationFacilityId > 0)
            {
                var customerId = transfer.Agreement?.CustomerId ?? 0;
                if (customerId > 0)
                {
                    db.SupportTickets.Add(new SupportTicket
                    {
                        TicketNo = TicketConstants.GenerateTicketNo(),
                        CustomerId = customerId,
                        FacilityId = destinationFacilityId,
                        AgreementId = transfer.AgreementId,
                        StorageUnitId = transfer.ToUnitId,
                        Category = TicketCategoryConstants.Other,
                        Priority = TicketPriorityConstants.Urgent,
                        Subject = $"[INTERNAL] {alertKey} Cảnh báo chuyển kho quá hạn check-in",
                        Description = $"Yêu cầu chuyển kho #{transfer.Id} (Kho đích: {transfer.ToUnit?.UnitCode}) đã trả kho cũ từ ngày {transfer.MoveOutDate:yyyy-MM-dd} (quá 48h) nhưng khách hàng chưa đến check-in nhận kho mới. Quản lý cơ sở cần kiểm tra liên hệ khách hàng hoặc giải phóng kho đích.",
                        Status = TicketStatusConstants.Open,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                    alertedCount++;
                }
            }
        }

        if (unpaid.Count + overdue.Count + alertedCount > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Expired {UnpaidCount} unpaid, {OverdueCount} overdue, and alerted {AlertedCount} stalled in-transit transfer requests.", unpaid.Count, overdue.Count, alertedCount);
        }
    }
}
