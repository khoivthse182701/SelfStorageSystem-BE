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

    private static readonly TimeZoneInfo LocalFacilityTimeZone = GetVietnamTimeZone();

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }
    }

    private async Task ProcessLifecycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
        var now = DateTimeOffset.UtcNow;
        var localNow = TimeZoneInfo.ConvertTime(now, LocalFacilityTimeZone);
        var today = DateOnly.FromDateTime(localNow.DateTime);

        // 1. Mark overdue invoices (Open & past DueDate)
        var overdueInvoices = await db.Invoices
            .Where(i => i.Status == InvoiceStatusConstants.Open && i.DueDate < today)
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
            _logger.LogInformation("StorageLifecycleBackgroundService transitioned {Count} invoices to Overdue.", overdueInvoices.Count);
        }

        // 2. Auto-sync open DelinquencyCases for overdue agreements (Batch / Eager Load to prevent N+1)
        var overdueAgreements = await db.RentalAgreements
            .Include(a => a.Invoices)
            .Where(a => a.Status == RentalAgreementStatusConstants.Active)
            .Where(a => a.Invoices.Any(i => i.Status == InvoiceStatusConstants.Overdue) || a.EndDate < today)
            .Where(a => !a.DelinquencyCases.Any(c => c.Status == DelinquencyCaseStatusConstants.Delinquent || c.Status == DelinquencyCaseStatusConstants.Grace || c.Status == DelinquencyCaseStatusConstants.TerminationStarted))
            .Take(50)
            .ToListAsync(cancellationToken);

        if (overdueAgreements.Count > 0)
        {
            foreach (var ag in overdueAgreements)
            {
                // In-memory lookup: Find earliest overdue or past-due invoice, fallback to latest invoice
                var targetInvoice = ag.Invoices
                    .Where(i => i.Status == InvoiceStatusConstants.Overdue || (i.Status == InvoiceStatusConstants.Open && i.DueDate < today))
                    .OrderBy(i => i.DueDate)
                    .FirstOrDefault() ?? ag.Invoices.OrderByDescending(i => i.Id).FirstOrDefault();

                if (targetInvoice != null)
                {
                    var balance = targetInvoice.TotalAmount;
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
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("StorageLifecycleBackgroundService auto-opened {Count} delinquency cases.", overdueAgreements.Count);
        }

        // 3. Suspend access for delinquency cases past grace period (Batch query to eliminate N+1)
        var casesPastGrace = await db.DelinquencyCases
            .Include(c => c.Agreement)
            .Where(c => (c.Status == DelinquencyCaseStatusConstants.Delinquent || c.Status == DelinquencyCaseStatusConstants.Grace) && c.GraceEndsAt <= now)
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
                    Details = $"Grace period expired on {dCase.GraceEndsAt:yyyy-MM-dd HH:mm}. Access credentials suspended automatically.",
                    PerformedBy = null,
                    OccurredAt = now
                });
            }
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("StorageLifecycleBackgroundService locked access for {Count} agreements past grace period.", casesPastGrace.Count);
        }
    }
}
