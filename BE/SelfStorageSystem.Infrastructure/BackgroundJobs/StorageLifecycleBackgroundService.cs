using SelfStorageSystem.Infrastructure.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.BackgroundJobs;

/// <summary>
/// Background worker automatically handling storage lifecycle events:
/// 1. Transitions past-due invoices from Open to Overdue.
/// 2. Auto-creates DelinquencyCases for overdue agreements.
/// 3. Suspends access credentials and records delinquency lockout when grace period expires.
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

    private static readonly TimeZoneInfo LocalFacilityTimeZone = TimeZoneHelper.ResolveTimeZone(null);

    private async Task ProcessLifecycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
        var now = DateTimeOffset.UtcNow;
        var localNow = TimeZoneInfo.ConvertTime(now, LocalFacilityTimeZone);
        var today = DateOnly.FromDateTime(localNow.DateTime);

        // 0. Auto-expire Abandoned Reservations (Status == Pending and HoldUntil < now)
        var expiredReservations = await db.Reservations
            .Include(r => r.UnitAllocation)
                .ThenInclude(ua => ua!.StorageUnit)
            .Where(r => r.Status == ReservationStatusConstants.Pending && r.HoldUntil < now)
            .OrderBy(r => r.HoldUntil)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (expiredReservations.Count > 0)
        {
            foreach (var r in expiredReservations)
            {
                r.Status = ReservationStatusConstants.Expired;
                r.UpdatedAt = now;
                if (r.UnitAllocation != null)
                {
                    r.UnitAllocation.Status = AllocationStatusConstants.Cancelled;
                    r.UnitAllocation.EndedAt = now;
                    if (r.UnitAllocation.StorageUnit != null && r.UnitAllocation.StorageUnit.PhysicalStatus == StorageUnitStatusConstants.Reserved)
                    {
                        r.UnitAllocation.StorageUnit.PhysicalStatus = StorageUnitStatusConstants.Available;
                        r.UnitAllocation.StorageUnit.UpdatedAt = now;
                    }
                }

                var redemptions = await db.PromotionRedemptions
                    .Where(pr => pr.ReservationId == r.Id && pr.Status == PromotionRedemptionStatusConstants.Reserved)
                    .ToListAsync(cancellationToken);
                foreach (var red in redemptions)
                {
                    red.Status = PromotionRedemptionStatusConstants.Released;
                }
            }
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("StorageLifecycleBackgroundService cleaned up {Count} expired abandoned reservations.", expiredReservations.Count);
        }

        // Transfer holds expire by elapsed minutes in TransferExpiryWorker, never by calendar due date.
        var overdueInvoices = await db.Invoices
            .Where(i => i.Status == InvoiceStatusConstants.Open && i.DueDate < today &&
                (i.BillingPeriod == null || !i.BillingPeriod.StartsWith(TransferConstants.BillingPeriodPrefix)) &&
                !i.InvoiceNo.StartsWith(TransferConstants.InvoicePrefix))
            .OrderBy(i => i.DueDate)
            .ThenBy(i => i.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (overdueInvoices.Count > 0)
        {
            foreach (var inv in overdueInvoices)
            {
                inv.Status = InvoiceStatusConstants.Overdue;
                inv.UpdatedAt = now;
            }
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("StorageLifecycleBackgroundService processed {Count} past-due invoices.", overdueInvoices.Count);
        }

        // 2. Auto-sync open DelinquencyCases ONLY for actual overdue debts (Invoices past due with remaining balance > 0)
        var overdueAgreements = await db.RentalAgreements
            .Include(a => a.Invoices)
            .Where(a => a.Status == RentalAgreementStatusConstants.Active || a.Status == RentalAgreementStatusConstants.ExpiringSoon)
            .Where(a => a.Invoices.Any(i =>
                i.Status == InvoiceStatusConstants.Overdue &&
                (i.TotalAmount - i.PaidAmount) > 0 &&
                (i.BillingPeriod == null || !i.BillingPeriod.StartsWith(TransferConstants.BillingPeriodPrefix)) &&
                (i.InvoiceNo == null || !i.InvoiceNo.StartsWith(TransferConstants.InvoicePrefix))))
            .Where(a => !a.DelinquencyCases.Any(c => c.Status == DelinquencyCaseStatusConstants.Delinquent || c.Status == DelinquencyCaseStatusConstants.Grace || c.Status == DelinquencyCaseStatusConstants.TerminationStarted))
            .OrderBy(a => a.EndDate)
            .ThenBy(a => a.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (overdueAgreements.Count > 0)
        {
            foreach (var ag in overdueAgreements)
            {
                // In-memory lookup: Find earliest overdue invoice with actual outstanding balance
                var targetInvoice = ag.Invoices
                    .Where(i => i.Status == InvoiceStatusConstants.Overdue &&
                                (i.TotalAmount - i.PaidAmount) > 0 &&
                                (i.BillingPeriod == null || !i.BillingPeriod.StartsWith(TransferConstants.BillingPeriodPrefix)) &&
                                (i.InvoiceNo == null || !i.InvoiceNo.StartsWith(TransferConstants.InvoicePrefix)))
                    .OrderBy(i => i.DueDate)
                    .FirstOrDefault();

                if (targetInvoice != null)
                {
                    var balance = targetInvoice.TotalAmount - targetInvoice.PaidAmount;
                    if (balance > 0)
                    {
                        db.DelinquencyCases.Add(new DelinquencyCase
                        {
                            AgreementId = ag.Id,
                            InvoiceId = targetInvoice.Id,
                            Status = DelinquencyCaseStatusConstants.Delinquent,
                            OpenedAt = now,
                            GraceEndsAt = now.AddDays(7),
                            OutstandingSnapshot = balance,
                            Notes = $"Auto-opened by StorageLifecycleBackgroundService for overdue agreement #{ag.AgreementNo}"
                        });
                    }
                }
            }
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("StorageLifecycleBackgroundService auto-opened delinquency cases for {Count} debt-ridden agreements.", overdueAgreements.Count);
        }

        // 2b. Expired or Expiring Agreements WITHOUT Debt (EndDate <= today, no open overdue debt)
        var expiringAgreements = await db.RentalAgreements
            .Include(a => a.Invoices)
            .Where(a => a.Status == RentalAgreementStatusConstants.Active && a.EndDate <= today)
            .Where(a => !a.Invoices.Any(i => i.Status == InvoiceStatusConstants.Overdue && (i.TotalAmount - i.PaidAmount) > 0))
            .OrderBy(a => a.EndDate)
            .ThenBy(a => a.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (expiringAgreements.Count > 0)
        {
            foreach (var ag in expiringAgreements)
            {
                ag.Status = RentalAgreementStatusConstants.ExpiringSoon;
                ag.UpdatedAt = now;
            }
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("StorageLifecycleBackgroundService transitioned {Count} fully-paid expired agreements to ExpiringSoon.", expiringAgreements.Count);
        }

        // 3. Suspend access for delinquency cases past grace period (Batch query to eliminate N+1)
        var casesPastGrace = await db.DelinquencyCases
            .Include(c => c.Agreement)
            .Where(c => (c.Status == DelinquencyCaseStatusConstants.Delinquent || c.Status == DelinquencyCaseStatusConstants.Grace) && c.GraceEndsAt <= now)
            .OrderBy(c => c.GraceEndsAt)
            .ThenBy(c => c.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (casesPastGrace.Count > 0)
        {
            var caseAgreementIds = casesPastGrace.Select(c => c.AgreementId).Distinct().ToList();
            var activeCredentials = await db.AccessCredentials
                .Where(ac => caseAgreementIds.Contains(ac.AgreementId) && ac.Status == CredentialStatusConstants.Active)
                .ToListAsync(cancellationToken);

            var credsByAgreement = activeCredentials.ToLookup(ac => ac.AgreementId);

            foreach (var dCase in casesPastGrace)
            {
                dCase.Status = DelinquencyCaseStatusConstants.TerminationStarted;

                // Suspend active credentials for this agreement in-memory
                foreach (var cred in credsByAgreement[dCase.AgreementId])
                {
                    cred.Status = CredentialStatusConstants.Suspended;
                    cred.RevokedAt = now;
                }

                db.DelinquencyActions.Add(new DelinquencyAction
                {
                    DelinquencyCaseId = dCase.Id,
                    ActionType = DelinquencyActionTypeConstants.CredentialSuspended,
                    Details = JsonSerializer.Serialize(new
                    {
                        note = $"Grace period expired on {dCase.GraceEndsAt:yyyy-MM-dd HH:mm}. Access credentials suspended automatically.",
                        system = true
                    }),
                    PerformedBy = null,
                    OccurredAt = now
                });
            }
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("StorageLifecycleBackgroundService locked access for {Count} agreements past grace period.", casesPastGrace.Count);
        }
    }
}
