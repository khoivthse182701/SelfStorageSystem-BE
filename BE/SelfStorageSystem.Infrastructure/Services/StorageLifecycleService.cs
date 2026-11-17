using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;
using SelfStorageSystem.Infrastructure.Common;
using SelfStorageSystem.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace SelfStorageSystem.Infrastructure.Services;

public sealed class StorageLifecycleService : IStorageLifecycleService
{
    private readonly SelfStorageDbContext _db;
    private readonly IReservationHoldExpiryService _holdExpiryService;
    private readonly IRealtimeNotifier? _realtimeNotifier;
    private readonly RentalSettings _rentalSettings;
    private readonly ILogger<StorageLifecycleService> _logger;

    public StorageLifecycleService(
        SelfStorageDbContext db,
        IReservationHoldExpiryService? holdExpiryService = null,
        IRealtimeNotifier? realtimeNotifier = null,
        IOptions<RentalSettings>? rentalOptions = null,
        ILogger<StorageLifecycleService>? logger = null)
    {
        _db = db;
        _holdExpiryService = holdExpiryService ?? new ReservationHoldExpiryService(db);
        _realtimeNotifier = realtimeNotifier;
        _rentalSettings = rentalOptions?.Value ?? new RentalSettings();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<StorageLifecycleService>.Instance;
    }

    public async Task RunCycleAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        // 0. Release expired reservation holds (Flow 1)
        await _holdExpiryService.ReleaseExpiredHoldsAsync(respectGracePeriod: true, cancellationToken);

        var defaultTz = TimeZoneHelper.ResolveTimeZone(null, _rentalSettings.DefaultTimezone);
        var defaultToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, defaultTz).DateTime);
        var facilitiesById = await _db.Facilities
            .AsNoTracking()
            .ToDictionaryAsync(f => f.Id, cancellationToken);

        // 1. Mark Open non-transfer invoices past due date as Overdue
        var openCandidateInvoices = await _db.Invoices
            .Where(i => i.Status == InvoiceStatusConstants.Open &&
                        (i.BillingPeriod == null || !i.BillingPeriod.StartsWith(TransferConstants.BillingPeriodPrefix)) &&
                        !i.InvoiceNo.StartsWith(TransferConstants.InvoicePrefix))
            .OrderBy(i => i.DueDate)
            .ThenBy(i => i.Id)
            .Take(200)
            .ToListAsync(cancellationToken);

        var agreementIdsForInvoices = openCandidateInvoices
            .Where(i => i.AgreementId.HasValue)
            .Select(i => i.AgreementId!.Value)
            .Distinct()
            .ToList();
        var agreementFacilityMap = agreementIdsForInvoices.Count > 0
            ? await _db.RentalAgreements
                .AsNoTracking()
                .Where(a => agreementIdsForInvoices.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.FacilityId, cancellationToken)
            : new Dictionary<long, long>();

        var markedOverdueInvoices = 0;
        foreach (var inv in openCandidateInvoices)
        {
            var tz = inv.AgreementId.HasValue
                     && agreementFacilityMap.TryGetValue(inv.AgreementId.Value, out var facId)
                     && facilitiesById.TryGetValue(facId, out var fac)
                ? TimeZoneHelper.ResolveTimeZone(fac.Timezone, _rentalSettings.DefaultTimezone)
                : defaultTz;
            var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, tz).DateTime);
            if (inv.DueDate < localToday)
            {
                inv.Status = InvoiceStatusConstants.Overdue;
                inv.UpdatedAt = now;
                markedOverdueInvoices++;
            }
        }
        if (markedOverdueInvoices > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("StorageLifecycleService marked {Count} past-due invoices as overdue.", markedOverdueInvoices);
        }

        // Load active FeeRules (late_fee) & Overdue PolicyVersion
        var activeLateFeeRules = await _db.FeeRules
            .AsNoTracking()
            .Where(f => f.IsActive && f.FeeType == "late_fee")
            .OrderByDescending(f => f.ValidFrom)
            .ThenByDescending(f => f.Id)
            .ToListAsync(cancellationToken);

        var (policyGraceDays, policyDefaultAfterDays) = await LoadOverduePolicyAsync(cancellationToken);

        // 2. T-7 Expiry Reminders, Renewal Invoice Generation, and ExpiringSoon Transition
        var candidateT7Agreements = await _db.RentalAgreements
            .Include(a => a.Invoices)
            .Include(a => a.MoveOutRequest)
            .Where(a => a.Status == RentalAgreementStatusConstants.Active || a.Status == RentalAgreementStatusConstants.ExpiringSoon)
            .OrderBy(a => a.EndDate)
            .ThenBy(a => a.Id)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var ag in candidateT7Agreements)
        {
            var tz = facilitiesById.TryGetValue(ag.FacilityId, out var facT7)
                ? TimeZoneHelper.ResolveTimeZone(facT7.Timezone, _rentalSettings.DefaultTimezone)
                : defaultTz;
            var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, tz).DateTime);
            var daysToEnd = ag.EndDate.DayNumber - localToday.DayNumber;

            // T-7 .. T-0 (inclusive): generate reminder + auto renewal invoice if customer has not requested move-out
            if (daysToEnd >= 0 && daysToEnd <= 7 && ag.MoveOutRequest == null)
            {
                if (ag.Status == RentalAgreementStatusConstants.Active)
                {
                    ag.Status = RentalAgreementStatusConstants.ExpiringSoon;
                    ag.UpdatedAt = now;
                    await _db.SaveChangesAsync(cancellationToken);
                }

                var autoPeriod = $"AUTO-RNW-{ag.EndDate:yyyyMMdd}";
                var hasRenewalOrAutoInvoice = ag.Invoices.Any(i =>
                    i.Status != InvoiceStatusConstants.Voided &&
                    (string.Equals(i.BillingPeriod, autoPeriod, StringComparison.OrdinalIgnoreCase) ||
                     (i.BillingPeriod != null && i.BillingPeriod.StartsWith(PaymentConstants.RenewalInvoicePrefix, StringComparison.OrdinalIgnoreCase) && i.Status != InvoiceStatusConstants.Paid)));

                if (!hasRenewalOrAutoInvoice && ag.MonthlyRateSnapshot > 0)
                {
                    var renewalInv = new Invoice
                    {
                        InvoiceNo = $"INV-AUTO-{ag.Id}-{ag.EndDate:yyMMdd}",
                        AgreementId = ag.Id,
                        CustomerId = ag.CustomerId,
                        BillingPeriod = autoPeriod,
                        IssueDate = localToday,
                        DueDate = ag.EndDate < localToday ? localToday : ag.EndDate,
                        Currency = PaymentConstants.CurrencyVnd,
                        SubtotalAmount = ag.MonthlyRateSnapshot,
                        DiscountAmount = 0m,
                        TaxAmount = 0m,
                        TotalAmount = ag.MonthlyRateSnapshot,
                        PaidAmount = 0m,
                        Status = InvoiceStatusConstants.Draft,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _db.Invoices.Add(renewalInv);
                    await _db.SaveChangesAsync(cancellationToken);

                    _db.InvoiceLines.Add(new InvoiceLine
                    {
                        InvoiceId = renewalInv.Id,
                        LineType = InvoiceLineTypeConstants.Rent,
                        Description = $"Gia hạn tự động kỳ tiếp theo cho hợp đồng #{ag.AgreementNo}",
                        Quantity = 1,
                        UnitPrice = ag.MonthlyRateSnapshot,
                        LineAmount = ag.MonthlyRateSnapshot,
                        Metadata = "{}"
                    });
                    await _db.SaveChangesAsync(cancellationToken);

                    renewalInv.Status = InvoiceStatusConstants.Open;
                    renewalInv.UpdatedAt = DateTimeOffset.UtcNow;
                    await _db.SaveChangesAsync(cancellationToken);
                    ag.Invoices.Add(renewalInv);
                }

                var t7Key = $"renewal-t7-{ag.Id}-{ag.EndDate:yyyyMMdd}";
                if (!await _db.Notifications.AnyAsync(n => n.DeduplicationKey == t7Key, cancellationToken))
                {
                    await AddNotificationSafeAsync(
                        ag.CustomerId,
                        "RENTAL_EXPIRY_T7",
                        $"Hợp đồng #{ag.AgreementNo} sắp hết hạn",
                        $"Hợp đồng lưu trữ #{ag.AgreementNo} sẽ hết hạn vào ngày {ag.EndDate:dd/MM/yyyy}. Vui lòng gia hạn để tránh gián đoạn truy cập.",
                        t7Key,
                        new { agreementId = ag.Id, agreementNo = ag.AgreementNo, endDate = ag.EndDate, daysRemaining = daysToEnd },
                        now,
                        cancellationToken);
                }
            }
        }

        // 2b. Suspend access for existing DelinquencyCases past GraceEndsAt before daily overdue scan
        var casesPastGrace = await _db.DelinquencyCases
            .Where(c => (c.Status == DelinquencyCaseStatusConstants.Delinquent || c.Status == DelinquencyCaseStatusConstants.Grace)
                        && c.GraceEndsAt <= now
                        && c.ResolvedAt == null)
            .OrderBy(c => c.GraceEndsAt)
            .ThenBy(c => c.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (casesPastGrace.Count > 0)
        {
            var caseAgreementIds = casesPastGrace.Select(c => c.AgreementId).Distinct().ToList();
            var activeCreds = await _db.AccessCredentials
                .Where(ac => caseAgreementIds.Contains(ac.AgreementId) && ac.Status == CredentialStatusConstants.Active)
                .ToListAsync(cancellationToken);

            var credsByAgreement = activeCreds.ToLookup(ac => ac.AgreementId);
            var changed = false;

            foreach (var dCase in casesPastGrace)
            {
                if (dCase.Status != DelinquencyCaseStatusConstants.TerminationStarted)
                {
                    dCase.Status = DelinquencyCaseStatusConstants.TerminationStarted;
                    changed = true;
                }

                var creds = credsByAgreement[dCase.AgreementId].ToList();
                if (creds.Count > 0)
                {
                    foreach (var cred in creds)
                    {
                        cred.Status = CredentialStatusConstants.Suspended;
                        cred.RevokedAt = null;
                    }

                    _db.DelinquencyActions.Add(new DelinquencyAction
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
                    changed = true;
                }
            }

            if (changed)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        // 3. D+1 Overdue Detection, Daily Late-Fee Accrual (idempotent per day), PIN Suspension, & >30d Defaulted
        var agreementsToEvaluate = await _db.RentalAgreements
            .Include(a => a.Invoices)
                .ThenInclude(i => i.InvoiceLines)
            .Include(a => a.DelinquencyCases)
            .Include(a => a.AccessCredentials)
            .Include(a => a.MoveOutRequest)
            .Where(a => a.Status == RentalAgreementStatusConstants.Active
                        || a.Status == RentalAgreementStatusConstants.ExpiringSoon
                        || a.Status == RentalAgreementStatusConstants.Overdue)
            .OrderBy(a => a.EndDate)
            .ThenBy(a => a.Id)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var ag in agreementsToEvaluate)
        {
            var tz = facilitiesById.TryGetValue(ag.FacilityId, out var facOvd)
                ? TimeZoneHelper.ResolveTimeZone(facOvd.Timezone, _rentalSettings.DefaultTimezone)
                : defaultTz;
            var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, tz).DateTime);

            var unpaidPastDue = ag.Invoices
                .Where(i => (i.Status == InvoiceStatusConstants.Overdue || i.Status == InvoiceStatusConstants.Open || i.Status == InvoiceStatusConstants.PartiallyPaid)
                            && i.DueDate < localToday
                            && (i.TotalAmount - i.PaidAmount) > 0
                            && (i.BillingPeriod == null || !i.BillingPeriod.StartsWith(TransferConstants.BillingPeriodPrefix, StringComparison.OrdinalIgnoreCase))
                            && !i.InvoiceNo.StartsWith(TransferConstants.InvoicePrefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(i => i.DueDate)
                .ThenBy(i => i.Id)
                .ToList();

            var expiredWithoutMoveOut = ag.EndDate < localToday && ag.MoveOutRequest == null;

            // Ensure overdue invoice exists if agreement is strictly past EndDate with no move-out and no unpaid invoice
            if (unpaidPastDue.Count == 0 && expiredWithoutMoveOut && ag.MonthlyRateSnapshot > 0)
            {
                var existingUnpaidAny = ag.Invoices.Any(i =>
                    i.Status is InvoiceStatusConstants.Open or InvoiceStatusConstants.PartiallyPaid or InvoiceStatusConstants.Overdue
                    && (i.TotalAmount - i.PaidAmount) > 0);
                var allPaid = ag.Invoices.Any(i => i.Status == InvoiceStatusConstants.Paid);

                // Only generate auto-renewal overdue invoice if agreement was still active/overdue without an existing paid final settlement
                if (!existingUnpaidAny && !allPaid)
                {
                    var autoPeriod = $"AUTO-OVD-{ag.EndDate:yyyyMMdd}";
                    var ovdInv = new Invoice
                    {
                        InvoiceNo = $"INV-OVD-{ag.Id}-{ag.EndDate:yyMMdd}",
                        AgreementId = ag.Id,
                        CustomerId = ag.CustomerId,
                        BillingPeriod = autoPeriod,
                        IssueDate = ag.EndDate,
                        DueDate = ag.EndDate,
                        Currency = PaymentConstants.CurrencyVnd,
                        SubtotalAmount = ag.MonthlyRateSnapshot,
                        DiscountAmount = 0m,
                        TaxAmount = 0m,
                        TotalAmount = ag.MonthlyRateSnapshot,
                        PaidAmount = 0m,
                        Status = InvoiceStatusConstants.Draft,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _db.Invoices.Add(ovdInv);
                    await _db.SaveChangesAsync(cancellationToken);

                    _db.InvoiceLines.Add(new InvoiceLine
                    {
                        InvoiceId = ovdInv.Id,
                        LineType = InvoiceLineTypeConstants.Rent,
                        Description = $"Gia hạn quá hạn hợp đồng #{ag.AgreementNo}",
                        Quantity = 1,
                        UnitPrice = ag.MonthlyRateSnapshot,
                        LineAmount = ag.MonthlyRateSnapshot,
                        Metadata = "{}"
                    });
                    await _db.SaveChangesAsync(cancellationToken);

                    ovdInv.Status = InvoiceStatusConstants.Overdue;
                    ovdInv.UpdatedAt = DateTimeOffset.UtcNow;
                    await _db.SaveChangesAsync(cancellationToken);

                    ag.Invoices.Add(ovdInv);
                    unpaidPastDue.Add(ovdInv);
                }
            }

            if (unpaidPastDue.Count == 0)
                continue;

            foreach (var inv in unpaidPastDue.Where(i => i.Status == InvoiceStatusConstants.Open && i.DueDate < localToday))
            {
                inv.Status = InvoiceStatusConstants.Overdue;
                inv.UpdatedAt = now;
            }

            var targetInvoice = unpaidPastDue[0];
            var feeRule = ResolveLateFeeRule(activeLateFeeRules, ag.FacilityId, localToday);
            var effectiveGraceDays = feeRule != null ? feeRule.GraceDays : policyGraceDays;
            var daysPastDue = Math.Max(0, localToday.DayNumber - targetInvoice.DueDate.DayNumber);

            if (daysPastDue <= effectiveGraceDays)
                continue;

            // D+1 after grace: Transition agreement Active/ExpiringSoon -> Overdue
            if (ag.Status is RentalAgreementStatusConstants.Active or RentalAgreementStatusConstants.ExpiringSoon or "extended")
            {
                ag.Status = RentalAgreementStatusConstants.Overdue;
                ag.UpdatedAt = now;
            }

            // Open or get existing DelinquencyCase for targetInvoice
            var dCase = ag.DelinquencyCases
                .Where(c => c.ResolvedAt == null &&
                            c.Status != DelinquencyCaseStatusConstants.Cured &&
                            c.Status != DelinquencyCaseStatusConstants.Waived)
                .OrderByDescending(c => c.OpenedAt)
                .FirstOrDefault();

            var graceEndsAt = new DateTimeOffset(targetInvoice.DueDate.AddDays(effectiveGraceDays + 1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var outstandingTotal = unpaidPastDue.Sum(i => Math.Max(0m, i.TotalAmount - i.PaidAmount));

            if (dCase == null)
            {
                var openedAt = graceEndsAt < now ? graceEndsAt : now;
                dCase = new DelinquencyCase
                {
                    AgreementId = ag.Id,
                    InvoiceId = targetInvoice.Id,
                    Status = DelinquencyCaseStatusConstants.Delinquent,
                    OpenedAt = openedAt,
                    GraceEndsAt = graceEndsAt,
                    OutstandingSnapshot = Math.Max(1m, outstandingTotal),
                    Notes = $"Auto-opened by StorageLifecycleService ({daysPastDue}d past due)"
                };
                _db.DelinquencyCases.Add(dCase);
                await _db.SaveChangesAsync(cancellationToken);
            }

            // Suspend active PIN / access credentials immediately on D+1 overdue (idempotent)
            var activeCreds = ag.AccessCredentials
                .Where(c => c.Status == CredentialStatusConstants.Active || c.Status == CredentialStatusConstants.Pending)
                .ToList();

            if (activeCreds.Count > 0)
            {
                foreach (var cred in activeCreds)
                {
                    cred.Status = CredentialStatusConstants.Suspended;
                    cred.RevokedAt = null;
                }

                _db.DelinquencyActions.Add(new DelinquencyAction
                {
                    DelinquencyCaseId = dCase.Id,
                    ActionType = DelinquencyActionTypeConstants.CredentialSuspended,
                    Details = JsonSerializer.Serialize(new
                    {
                        note = $"Access suspended automatically due to overdue balance ({daysPastDue}d past due).",
                        suspendedCount = activeCreds.Count,
                        system = true
                    }),
                    PerformedBy = null,
                    OccurredAt = now
                });
                await _db.SaveChangesAsync(cancellationToken);
            }

            // Idempotent daily late_fee InvoiceLine accrual (at most 1 per calendar day in facility timezone)
            var feeDateMarker = $"\"feeDate\":\"{localToday:yyyy-MM-dd}\"";
            var alreadyChargedToday = targetInvoice.InvoiceLines.Any(l =>
                string.Equals(l.LineType, InvoiceLineTypeConstants.LateFee, StringComparison.OrdinalIgnoreCase) &&
                ((!string.IsNullOrEmpty(l.Metadata) && l.Metadata.Contains(feeDateMarker, StringComparison.Ordinal)) ||
                 (!string.IsNullOrEmpty(l.Description) && l.Description.Contains($"{localToday:yyyy-MM-dd}", StringComparison.Ordinal))));

            var dailyFee = ComputeDailyLateFee(feeRule, ag.MonthlyRateSnapshot);
            if (!alreadyChargedToday && dailyFee > 0m)
            {
                var feeLine = new InvoiceLine
                {
                    InvoiceId = targetInvoice.Id,
                    LineType = InvoiceLineTypeConstants.LateFee,
                    Description = $"Phí phạt quá hạn ngày {localToday:yyyy-MM-dd} (HĐ #{ag.AgreementNo})",
                    Quantity = 1,
                    UnitPrice = dailyFee,
                    LineAmount = dailyFee,
                    Metadata = JsonSerializer.Serialize(new
                    {
                        feeDate = localToday.ToString("yyyy-MM-dd"),
                        feeRuleCode = feeRule?.Code ?? "DEFAULT_LATE_FEE",
                        system = true
                    })
                };
                _db.InvoiceLines.Add(feeLine);

                if (_db.Database.IsSqlServer())
                {
                    await _db.SaveChangesAsync(cancellationToken);
                    await _db.Entry(targetInvoice).ReloadAsync(cancellationToken);
                }
                else
                {
                    targetInvoice.SubtotalAmount += dailyFee;
                    targetInvoice.TotalAmount += dailyFee;
                    targetInvoice.UpdatedAt = now;
                    await _db.SaveChangesAsync(cancellationToken);
                }

                dCase.OutstandingSnapshot = Math.Max(1m, unpaidPastDue.Sum(i => Math.Max(0m, i.TotalAmount - i.PaidAmount)));

                _db.DelinquencyActions.Add(new DelinquencyAction
                {
                    DelinquencyCaseId = dCase.Id,
                    ActionType = DelinquencyActionTypeConstants.LateFeeAdded,
                    Details = JsonSerializer.Serialize(new
                    {
                        feeDate = localToday.ToString("yyyy-MM-dd"),
                        amount = dailyFee,
                        invoiceNo = targetInvoice.InvoiceNo,
                        system = true
                    }),
                    PerformedBy = null,
                    OccurredAt = now
                });
                await _db.SaveChangesAsync(cancellationToken);
            }

            // > 30 days overdue -> transition to defaulted + termination_started
            var overdueFromEndDays = Math.Max(daysPastDue, localToday.DayNumber - ag.EndDate.DayNumber);
            if (overdueFromEndDays > policyDefaultAfterDays && ag.Status == RentalAgreementStatusConstants.Overdue)
            {
                ag.Status = "defaulted";
                ag.UpdatedAt = now;
                dCase.Status = DelinquencyCaseStatusConstants.TerminationStarted;

                _db.DelinquencyActions.Add(new DelinquencyAction
                {
                    DelinquencyCaseId = dCase.Id,
                    ActionType = DelinquencyActionTypeConstants.TerminationStarted,
                    Details = JsonSerializer.Serialize(new
                    {
                        note = $"Agreement transitioned to defaulted after {overdueFromEndDays} days overdue.",
                        system = true
                    }),
                    PerformedBy = null,
                    OccurredAt = now
                });
                await _db.SaveChangesAsync(cancellationToken);
            }

            // Notify customer and facility staff/manager once per day
            var custNotifyKey = $"overdue-cust-{ag.Id}-{localToday:yyyyMMdd}";
            if (!await _db.Notifications.AnyAsync(n => n.DeduplicationKey == custNotifyKey, cancellationToken))
            {
                await AddNotificationSafeAsync(
                    ag.CustomerId,
                    "AGREEMENT_OVERDUE",
                    $"Hợp đồng #{ag.AgreementNo} quá hạn thanh toán",
                    $"Hợp đồng #{ag.AgreementNo} đã quá hạn {daysPastDue} ngày. Mã PIN đã bị tạm khóa; vui lòng gia hạn hoặc thanh toán để mở lại quyền truy cập.",
                    custNotifyKey,
                    new { agreementId = ag.Id, agreementNo = ag.AgreementNo, daysPastDue, status = ag.Status },
                    now,
                    cancellationToken);

                var opsUserIds = await _db.StaffFacilityAssignments
                    .AsNoTracking()
                    .Where(s => s.FacilityId == ag.FacilityId && s.StartsAt <= now && (s.EndsAt == null || s.EndsAt > now))
                    .Select(s => s.EmployeeId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                foreach (var opsId in opsUserIds)
                {
                    var opsKey = $"overdue-ops-{ag.Id}-{opsId}-{localToday:yyyyMMdd}";
                    await AddNotificationSafeAsync(
                        opsId,
                        "AGREEMENT_OVERDUE_ALERT",
                        $"Cảnh báo quá hạn HĐ #{ag.AgreementNo}",
                        $"Hợp đồng #{ag.AgreementNo} quá hạn {daysPastDue} ngày ({ag.Status}). PIN đã bị khóa tự động.",
                        opsKey,
                        new { agreementId = ag.Id, facilityId = ag.FacilityId, daysPastDue, status = ag.Status },
                        now,
                        cancellationToken);
                }
            }
        }
    }

    private async Task<(int GraceDays, int DefaultAfterDays)> LoadOverduePolicyAsync(CancellationToken cancellationToken)
    {
        var policy = await _db.PolicyVersions
            .AsNoTracking()
            .Where(p => p.PolicyType == PolicyType.Overdue.ToDbValue())
            .OrderByDescending(p => p.ValidFrom)
            .FirstOrDefaultAsync(cancellationToken);

        int graceDays = 0;
        int defaultAfterDays = 30;
        if (!string.IsNullOrWhiteSpace(policy?.Content))
        {
            try
            {
                using var doc = JsonDocument.Parse(policy.Content);
                if (doc.RootElement.TryGetProperty("grace_days", out var g) && g.TryGetInt32(out var gv))
                    graceDays = Math.Max(0, gv);
                if (doc.RootElement.TryGetProperty("default_after_days", out var d) && d.TryGetInt32(out var dv))
                    defaultAfterDays = Math.Max(1, dv);
            }
            catch (JsonException)
            {
                // Ignore malformed policy JSON
            }
        }
        return (graceDays, defaultAfterDays);
    }

    private static FeeRule? ResolveLateFeeRule(IReadOnlyList<FeeRule> rules, long facilityId, DateOnly today)
    {
        return rules
            .Where(r => (r.FacilityId == facilityId || r.FacilityId == null)
                        && r.ValidFrom <= today
                        && (r.ValidTo == null || r.ValidTo >= today))
            .OrderByDescending(r => r.FacilityId == facilityId ? 1 : 0)
            .ThenByDescending(r => r.ValidFrom)
            .ThenByDescending(r => r.Id)
            .FirstOrDefault();
    }

    private static decimal ComputeDailyLateFee(FeeRule? rule, decimal monthlyRate)
    {
        if (rule == null)
            return 0m;

        var method = rule.CalculationMethod.Trim().ToLowerInvariant();
        if (method == FeeCalculationMethod.Percentage.ToDbValue() || method == "percentage" || method == "percent")
        {
            var pct = rule.RatePercent ?? 0m;
            return Math.Round(monthlyRate * (pct / 100m), 0, MidpointRounding.AwayFromZero);
        }

        return Math.Max(0m, rule.Amount ?? 0m);
    }

    private async Task AddNotificationSafeAsync(
        long recipientUserId,
        string templateCode,
        string subject,
        string body,
        string deduplicationKey,
        object payload,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            var exists = await _db.Notifications.AnyAsync(n => n.DeduplicationKey == deduplicationKey, cancellationToken);
            if (exists) return;

            var userExists = await _db.Users.AnyAsync(u => u.Id == recipientUserId, cancellationToken);
            if (!userExists) return;

            _db.Notifications.Add(new Notification
            {
                UserId = recipientUserId,
                Channel = "in_app",
                TemplateCode = templateCode,
                Subject = subject.Length <= 255 ? subject : subject[..255],
                Payload = JsonSerializer.Serialize(new { body, data = payload }),
                Status = "sent",
                ScheduledAt = now,
                SentAt = now,
                Attempts = 1,
                DeduplicationKey = deduplicationKey,
                CreatedAt = now
            });
            await _db.SaveChangesAsync(cancellationToken);

            if (_realtimeNotifier != null)
            {
                await _realtimeNotifier.SendNotificationToUserAsync(recipientUserId, subject, body, payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Skipped non-critical lifecycle notification for user {UserId}: {Message}", recipientUserId, ex.Message);
        }
    }
}
