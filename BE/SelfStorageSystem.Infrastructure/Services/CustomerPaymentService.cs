using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Payments;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Common;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public class CustomerPaymentService : ICustomerPaymentService
{
    private readonly SelfStorageDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly VietQrConfig _vietQrConfig;
    private readonly SePayConfig _sePayConfig;
    private readonly RentalSettings _rentalSettings;
    private readonly ILogger<CustomerPaymentService> _logger;
    private readonly IQrTokenService? _qrTokenService;
    private readonly IRealtimeNotifier? _realtimeNotifier;

    public CustomerPaymentService(
        SelfStorageDbContext dbContext,
        IEmailService emailService,
        IOptions<PaymentSettings> paymentOptions,
        ILogger<CustomerPaymentService> logger,
        IOptions<RentalSettings>? rentalOptions = null,
        IQrTokenService? qrTokenService = null,
        IRealtimeNotifier? realtimeNotifier = null)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _vietQrConfig = paymentOptions.Value.VietQr;
        _sePayConfig = paymentOptions.Value.SePay;
        _rentalSettings = rentalOptions?.Value ?? new RentalSettings();
        _logger = logger;
        _qrTokenService = qrTokenService;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<CheckoutResponse> CreateCheckoutAsync(
        long customerId,
        CreateCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var targetCount = (request.ReservationId.HasValue ? 1 : 0) +
                          (request.InvoiceId.HasValue ? 1 : 0) +
                          (request.RenewalId.HasValue ? 1 : 0);
        if ((request.ReservationId.HasValue && request.ReservationId <= 0) ||
            (request.InvoiceId.HasValue && request.InvoiceId <= 0) ||
            (request.RenewalId.HasValue && request.RenewalId <= 0) || targetCount != 1)
        {
            throw AppException.FromError(PaymentErrors.InvoiceNotFound);
        }

        if (request.RenewalId.HasValue)
        {
            var renewal = await _dbContext.RentalRenewals
                .Include(r => r.Agreement)
                    .ThenInclude(a => a.Facility)
                .FirstOrDefaultAsync(r => r.Id == request.RenewalId.Value && r.RequestedBy == customerId, cancellationToken);
            if (renewal == null || renewal.Status != RenewalStatusConstants.PendingPayment ||
                now - renewal.CreatedAt > TimeSpan.FromMinutes(_rentalSettings.RenewalHoldMinutes))
            {
                throw AppException.FromError(PaymentErrors.InvoiceNotFound);
            }

            var renewalPeriod = $"{PaymentConstants.RenewalInvoicePrefix}{renewal.Id}";
            var renewalInvoice = await _dbContext.Invoices
                .FirstOrDefaultAsync(i => i.AgreementId == renewal.AgreementId
                                       && i.BillingPeriod == renewalPeriod
                                       && i.Status != InvoiceStatusConstants.Voided, cancellationToken);

            if (renewalInvoice == null)
            {
                var months = Math.Max(1, (renewal.RequestedEndDate.Year - renewal.OldEndDate.Year) * 12
                    + renewal.RequestedEndDate.Month - renewal.OldEndDate.Month);
                var amount = (renewal.NewMonthlyRate ?? renewal.OldMonthlyRate) * months;
                var tzRnw = TimeZoneHelper.ResolveTimeZone(renewal.Agreement?.Facility?.Timezone, _rentalSettings.DefaultTimezone);
                var todayRnw = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, tzRnw).DateTime);

                renewalInvoice = new Invoice
                {
                    InvoiceNo = $"INV-RNW-{renewal.Id}-{now:yyMMdd}",
                    AgreementId = renewal.AgreementId,
                    CustomerId = customerId,
                    BillingPeriod = renewalPeriod,
                    IssueDate = todayRnw,
                    DueDate = todayRnw.AddDays(3),
                    Currency = PaymentConstants.CurrencyVnd,
                    SubtotalAmount = amount,
                    DiscountAmount = 0m,
                    TaxAmount = 0m,
                    TotalAmount = amount,
                    PaidAmount = 0m,
                    Status = InvoiceStatusConstants.Draft,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.Invoices.Add(renewalInvoice);
                await _dbContext.SaveChangesAsync(cancellationToken);

                _dbContext.InvoiceLines.Add(new InvoiceLine
                {
                    InvoiceId = renewalInvoice.Id,
                    LineType = InvoiceLineTypeConstants.Rent,
                    Description = $"Gia hạn hợp đồng ({months} tháng)",
                    Quantity = months,
                    UnitPrice = renewal.NewMonthlyRate ?? renewal.OldMonthlyRate,
                    LineAmount = amount,
                    Metadata = "{}"
                });
                await _dbContext.SaveChangesAsync(cancellationToken);

                renewalInvoice.Status = InvoiceStatusConstants.Open;
                renewalInvoice.UpdatedAt = DateTimeOffset.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            request.InvoiceId = renewalInvoice.Id;
            request.RenewalId = null;
        }

        Reservation? reservation;
        Invoice? invoice;

        if (request.ReservationId.HasValue)
        {
            reservation = await _dbContext.Reservations
                .Include(r => r.Facility)
                .Include(r => r.UnitType)
                .Include(r => r.Invoices)
                .FirstOrDefaultAsync(r => r.Id == request.ReservationId.Value && r.CustomerId == customerId, cancellationToken)
                ?? throw AppException.FromError(PaymentErrors.ReservationNotFound);

            if (reservation.HoldUntil < now ||
                reservation.Status == ReservationStatusConstants.Expired ||
                reservation.Status == ReservationStatusConstants.Cancelled)
            {
                throw AppException.FromError(PaymentErrors.HoldExpired);
            }

            if (reservation.Status is ReservationStatusConstants.Confirmed or ReservationStatusConstants.Converted)
                throw AppException.FromError(PaymentErrors.AlreadyConfirmed);

            invoice = reservation.Invoices.FirstOrDefault(i =>
                i.Status?.Trim().ToLowerInvariant() is
                    InvoiceStatusConstants.Open or InvoiceStatusConstants.Draft or
                    InvoiceStatusConstants.PartiallyPaid or InvoiceStatusConstants.Overdue);
        }
        else
        {
            invoice = await _dbContext.Invoices
                .Include(i => i.Reservation).ThenInclude(r => r!.Facility)
                .Include(i => i.Reservation).ThenInclude(r => r!.UnitType)
                .FirstOrDefaultAsync(i => i.Id == request.InvoiceId!.Value && i.CustomerId == customerId, cancellationToken);
            reservation = invoice?.Reservation;

            if (reservation != null)
            {
                if (reservation.Status is ReservationStatusConstants.Cancelled or ReservationStatusConstants.Expired || reservation.HoldUntil < now)
                    throw AppException.FromError(PaymentErrors.HoldExpired);

                if (reservation.Status is ReservationStatusConstants.Confirmed or ReservationStatusConstants.Converted)
                    throw AppException.FromError(PaymentErrors.AlreadyConfirmed);
            }
        }

        var normalizedInvoiceStatus = invoice?.Status?.Trim().ToLowerInvariant();
        if (invoice == null || normalizedInvoiceStatus is not (
            InvoiceStatusConstants.Open or InvoiceStatusConstants.Draft or InvoiceStatusConstants.PartiallyPaid or InvoiceStatusConstants.Overdue))
        {
            throw AppException.FromError(PaymentErrors.InvoiceNotFound);
        }

        var amountToPay = invoice.TotalAmount - invoice.PaidAmount;
        if (amountToPay <= 0)
            throw AppException.FromError(PaymentErrors.InvoiceAlreadyPaid);

        const string paymentMethod = PaymentConstants.MethodBankTransfer;
        const string provider = PaymentConstants.ProviderSePay;

        var payment = await _dbContext.Payments
            .FirstOrDefaultAsync(p => p.CustomerId == customerId
                                   && p.TargetInvoiceId == invoice.Id
                                   && p.Status == PaymentConstants.StatusPending
                                   && p.Provider == provider, cancellationToken);

        if (payment == null)
        {
            payment = new Payment
            {
                CustomerId = customerId,
                TargetInvoiceId = invoice.Id,
                TargetInvoice = invoice,
                Amount = amountToPay,
                Currency = PaymentConstants.CurrencyVnd,
                Method = paymentMethod,
                Provider = provider,
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                Status = PaymentConstants.StatusPending,
                Metadata = "{}",
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.Payments.Add(payment);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (payment.Amount != amountToPay)
        {
            payment.Amount = amountToPay;
            payment.UpdatedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var expiresInSeconds = reservation == null
            ? _rentalSettings.CheckoutExpirySeconds
            : (int)Math.Max(0, (reservation.HoldUntil - now).TotalSeconds);

        var prefix = !string.IsNullOrWhiteSpace(_sePayConfig.InvoicePrefix)
            ? _sePayConfig.InvoicePrefix.Trim()
            : SePayConstants.DefaultInvoicePrefix;
        var transferContent = $"{prefix}{invoice.Id}";

        return BuildCheckoutResponse(
            payment.Id,
            reservation?.Id ?? 0,
            invoice.Id,
            invoice.InvoiceNo,
            amountToPay,
            expiresInSeconds,
            transferContent);
    }

    public async Task<PaymentStatusDto> GetPaymentStatusAsync(
        long customerId,
        long paymentId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _dbContext.Payments
            .AsNoTracking()
            .Include(p => p.TargetInvoice)
                .ThenInclude(i => i.Reservation)
                    .ThenInclude(r => r!.RentalAgreement)
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken)
            ?? throw AppException.FromError(PaymentErrors.InvoiceNotFound);

        if (payment.CustomerId != customerId)
            throw AppException.FromError(ReservationErrors.Unauthorized);

        var invoice = payment.TargetInvoice;
        var reservation = invoice?.Reservation;
        string? checkInQrToken = null;
        if (reservation?.Status == ReservationStatusConstants.Confirmed)
        {
            checkInQrToken = _qrTokenService?.IssueToken(
                "checkin",
                reservation.Id,
                reservation.RentalAgreement?.Id,
                customerId,
                reservation.FacilityId,
                TimeSpan.FromDays(90));
        }

        return new PaymentStatusDto
        {
            PaymentId = payment.Id,
            InvoiceId = payment.TargetInvoiceId,
            InvoiceStatus = invoice?.Status ?? string.Empty,
            ReservationId = reservation?.Id,
            ReservationStatus = reservation?.Status,
            PaymentStatus = payment.Status,
            PaidAmount = invoice?.PaidAmount ?? payment.Amount,
            TotalAmount = invoice?.TotalAmount ?? payment.Amount,
            RequiresReconciliation = NeedsReconciliation(payment.Metadata),
            CheckInQrToken = checkInQrToken
        };
    }

    public async Task<bool> ProcessSePayWebhookAsync(
        SePayWebhookPayload payload,
        CancellationToken cancellationToken = default)
    {
        var result = await ProcessSePayWebhookWithResultAsync(payload, cancellationToken);
        return result.Processed;
    }

    public async Task<SePayWebhookResultDto> ProcessSePayWebhookWithResultAsync(
        SePayWebhookPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (payload == null)
            return Unmatched();

        var contentText = $"{payload.Code} {payload.Content} {payload.Description}".Trim();
        if (string.IsNullOrWhiteSpace(contentText))
            return Unmatched();

        if (payload.TransferAmount <= 0)
        {
            _logger.LogWarning(PaymentLogMessages.NonPositiveTransfer, payload.TransferAmount);
            return Unmatched();
        }

        if (string.Equals(payload.TransferType, SePayConstants.TransferTypeOut, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(PaymentLogMessages.OutgoingDebitIgnored);
            return Unmatched();
        }

        var externalEventId = ResolveExternalEventId(payload);
        var transactionRef = !string.IsNullOrWhiteSpace(payload.ReferenceCode)
            ? payload.ReferenceCode.Trim()
            : (payload.Id > 0 ? payload.Id.ToString(CultureInfo.InvariantCulture) : externalEventId);

        // 1. Primary Idempotency check in integration_events & payments
        if (await IsDuplicateEventAsync(externalEventId, transactionRef, cancellationToken))
        {
            _logger.LogInformation(PaymentLogMessages.TransactionReplay, transactionRef);
            var prev = await _dbContext.Payments.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Provider == PaymentConstants.ProviderSePay && p.ProviderTransactionId == transactionRef, cancellationToken);
            return new SePayWebhookResultDto
            {
                Processed = true,
                Action = "duplicate_ignored",
                PaymentId = prev?.Id,
                InvoiceId = prev?.TargetInvoiceId
            };
        }

        var prefix = !string.IsNullOrWhiteSpace(_sePayConfig.InvoicePrefix)
            ? _sePayConfig.InvoicePrefix.Trim()
            : SePayConstants.DefaultInvoicePrefix;
        var prefixMatch = Regex.Match(contentText, $@"{Regex.Escape(prefix)}[\s\-_]*(\d+)", RegexOptions.IgnoreCase);

        var invoice = await MatchInvoiceAsync(contentText, prefixMatch, cancellationToken);
        if (invoice == null)
        {
            var renewalResult = await TryProcessRenewalWebhookAsync(contentText, prefixMatch, payload, externalEventId, cancellationToken);
            if (renewalResult != null) return renewalResult;

            _logger.LogWarning("SePay webhook unmatched: Content='{Content}', Amount={Amount}, ReferenceCode='{ReferenceCode}', GatewayId={GatewayId}",
                payload.Content, payload.TransferAmount, payload.ReferenceCode, payload.Id);
            return Unmatched();
        }

        if (invoice.Status == InvoiceStatusConstants.Paid)
        {
            _logger.LogInformation(PaymentLogMessages.InvoiceAlreadyPaid, invoice.Id);
            await RecordIntegrationEventAsync("sepay", externalEventId, "bank_transfer", invoice.Id, payload.TransferAmount, IntegrationEventStatus.Ignored, cancellationToken);
            return new SePayWebhookResultDto { Processed = true, Action = "duplicate_ignored", InvoiceId = invoice.Id, ReservationId = invoice.ReservationId };
        }

        var now = DateTimeOffset.UtcNow;
        var remainingBalance = Math.Max(0m, invoice.TotalAmount - invoice.PaidAmount);

        var requiresReconciliation = invoice.Status == InvoiceStatusConstants.Voided ||
            invoice.Reservation?.Status is ReservationStatusConstants.Cancelled or ReservationStatusConstants.Expired;

        // 2. Partial Payment (Underpayment) on still-active invoice
        if (!requiresReconciliation && payload.TransferAmount < remainingBalance)
        {
            _logger.LogWarning(PaymentLogMessages.PartialPayment, payload.TransferAmount, remainingBalance, invoice.Id);

            IDbContextTransaction? partialTx = _dbContext.Database.IsSqlServer()
                ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
                : null;
            try
            {
                var partialPayment = new Payment
                {
                    CustomerId = invoice.CustomerId,
                    TargetInvoiceId = invoice.Id,
                    TargetInvoice = invoice,
                    Customer = invoice.Customer,
                    Amount = payload.TransferAmount,
                    Currency = PaymentConstants.CurrencyVnd,
                    Method = PaymentConstants.MethodBankTransfer,
                    Provider = PaymentConstants.ProviderSePay,
                    ProviderTransactionId = transactionRef,
                    IdempotencyKey = $"SEPAY-{externalEventId}-{Guid.NewGuid():N}",
                    Status = PaymentConstants.StatusSucceeded,
                    PaidAt = now,
                    Metadata = "{\"note\": \"Partial payment received - reservation status kept pending until fully paid.\"}",
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _dbContext.Payments.Add(partialPayment);
                await _dbContext.SaveChangesAsync(cancellationToken);

                _dbContext.PaymentAllocations.Add(new PaymentAllocation
                {
                    PaymentId = partialPayment.Id,
                    InvoiceId = invoice.Id,
                    AllocatedAmount = payload.TransferAmount,
                    AllocatedAt = now
                });

                invoice.PaidAmount += payload.TransferAmount;
                invoice.Status = InvoiceStatusConstants.PartiallyPaid;
                invoice.UpdatedAt = now;

                await RecordIntegrationEventAsync("sepay", externalEventId, "partial_payment", invoice.Id, payload.TransferAmount, IntegrationEventStatus.Processed, cancellationToken, save: false);
                await _dbContext.SaveChangesAsync(cancellationToken);

                if (partialTx != null) await partialTx.CommitAsync(cancellationToken);

                await NotifyPartialPaymentAsync(invoice, partialPayment, cancellationToken);

                return new SePayWebhookResultDto
                {
                    Processed = true,
                    Action = "partial_recorded",
                    InvoiceId = invoice.Id,
                    ReservationId = invoice.ReservationId,
                    PaymentId = partialPayment.Id
                };
            }
            catch (DbUpdateException)
            {
                if (partialTx != null) await partialTx.RollbackAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();
                if (await IsDuplicateEventAsync(externalEventId, transactionRef, cancellationToken))
                {
                    return new SePayWebhookResultDto { Processed = true, Action = "duplicate_ignored", InvoiceId = invoice.Id, ReservationId = invoice.ReservationId };
                }
                throw;
            }
            catch
            {
                if (partialTx != null) await partialTx.RollbackAsync(cancellationToken);
                throw;
            }
            finally
            {
                if (partialTx != null) await partialTx.DisposeAsync();
            }
        }

        // 3. Full, Overpayment, Transfer, or Late Payment
        var payment = await _dbContext.Payments
            .FirstOrDefaultAsync(p => p.TargetInvoiceId == invoice.Id && p.Status == PaymentConstants.StatusPending, cancellationToken);

        if (payment == null)
        {
            payment = new Payment
            {
                CustomerId = invoice.CustomerId,
                TargetInvoiceId = invoice.Id,
                TargetInvoice = invoice,
                Customer = invoice.Customer,
                Amount = payload.TransferAmount,
                Currency = PaymentConstants.CurrencyVnd,
                Method = PaymentConstants.MethodBankTransfer,
                Provider = PaymentConstants.ProviderSePay,
                ProviderTransactionId = transactionRef,
                IdempotencyKey = $"SEPAY-{externalEventId}-{Guid.NewGuid():N}",
                Status = PaymentConstants.StatusPending,
                Metadata = "{}",
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.Payments.Add(payment);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            payment.TargetInvoice = invoice;
            payment.Customer = invoice.Customer;
            payment.Amount = payload.TransferAmount;
            payment.ProviderTransactionId = transactionRef;
            payment.UpdatedAt = now;
        }

        return await CompletePaymentWithResultAsync(payment, transactionRef, externalEventId, cancellationToken);
    }

    public async Task<List<PaymentHistoryDto>> GetMyPaymentHistoryAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        var list = await _dbContext.Payments
            .AsNoTracking()
            .Include(p => p.TargetInvoice)
                .ThenInclude(i => i.Reservation)
                    .ThenInclude(r => r!.RentalAgreement)
            .Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        var history = list.Select(p => new PaymentHistoryDto
        {
            PaymentId = p.Id,
            InvoiceId = p.TargetInvoiceId,
            InvoiceNo = p.TargetInvoice?.InvoiceNo ?? string.Empty,
            ReservationId = p.TargetInvoice?.ReservationId,
            ReservationCode = p.TargetInvoice?.Reservation?.ReservationCode,
            ReservationStatus = p.TargetInvoice?.Reservation?.Status,
            HoldUntil = p.TargetInvoice?.Reservation?.HoldUntil,
            CanCancelReservation = p.TargetInvoice?.Reservation is { } reservation &&
                (reservation.Status == ReservationStatusConstants.Pending ||
                 reservation.Status == ReservationStatusConstants.AwaitingDeposit ||
                 reservation.Status == ReservationStatusConstants.Confirmed) &&
                (reservation.RentalAgreement == null ||
                 (reservation.RentalAgreement.Status == RentalAgreementStatusConstants.Draft &&
                  reservation.RentalAgreement.CheckedInAt == null)),
            Amount = p.Amount,
            Currency = p.Currency,
            Method = p.Method,
            Provider = p.Provider,
            ProviderTransactionId = p.ProviderTransactionId,
            Status = (p.Status == PaymentConstants.StatusPending &&
                      string.Equals(p.TargetInvoice?.Status, InvoiceStatusConstants.Voided, StringComparison.OrdinalIgnoreCase))
                ? PaymentConstants.StatusCancelled
                : p.Status,
            PaidAt = p.PaidAt,
            CreatedAt = p.CreatedAt,
            FailureReason = p.FailureReason,
            RequiresReconciliation = NeedsReconciliation(p.Metadata)
        }).ToList();

        var renewals = await _dbContext.RentalRenewals
            .AsNoTracking()
            .Include(r => r.Agreement)
            .Where(r => r.RequestedBy == customerId || (r.Agreement != null && r.Agreement.CustomerId == customerId))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var r in renewals)
        {
            var months = Math.Max(1, (r.RequestedEndDate.Year - r.OldEndDate.Year) * 12 + r.RequestedEndDate.Month - r.OldEndDate.Month);
            var amount = (r.NewMonthlyRate ?? r.OldMonthlyRate) * months;
            var status = r.Status switch
            {
                _ when string.Equals(r.Status, RenewalStatusConstants.PendingPayment, StringComparison.OrdinalIgnoreCase) =>
                    now - r.CreatedAt > TimeSpan.FromMinutes(_rentalSettings.RenewalHoldMinutes) ? "expired" : PaymentConstants.StatusPending,
                _ when string.Equals(r.Status, RenewalStatusConstants.Paid, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(r.Status, RenewalStatusConstants.Approved, StringComparison.OrdinalIgnoreCase) => PaymentConstants.StatusSucceeded,
                _ when string.Equals(r.Status, RenewalStatusConstants.Cancelled, StringComparison.OrdinalIgnoreCase) => PaymentConstants.StatusCancelled,
                _ when string.Equals(r.Status, RenewalStatusConstants.Rejected, StringComparison.OrdinalIgnoreCase) => PaymentConstants.StatusFailed,
                _ => r.Status
            };

            history.Add(new PaymentHistoryDto
            {
                PaymentId = 0,
                InvoiceId = 0,
                InvoiceNo = $"RNW-{r.Id}",
                ReservationId = null,
                ReservationCode = !string.IsNullOrWhiteSpace(r.Agreement?.AgreementNo) ? $"RNW-{r.Agreement.AgreementNo}" : $"RNW-{r.Id}",
                Amount = amount,
                Currency = PaymentConstants.CurrencyVnd,
                Method = PaymentConstants.MethodBankTransfer,
                Provider = PaymentConstants.ProviderSePay,
                Status = status,
                PaidAt = r.ReviewedAt,
                CreatedAt = r.CreatedAt,
                RenewalId = r.Id,
                AgreementNo = r.Agreement?.AgreementNo
            });
        }

        return history.OrderByDescending(h => h.CreatedAt).ToList();
    }

    public async Task<PagedResult<PaymentHistoryDto>> GetMyPaymentHistoryPagedAsync(
        long customerId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var all = await GetMyPaymentHistoryAsync(customerId, cancellationToken);
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<PaymentHistoryDto>(items, all.Count, page, pageSize);
    }

    public async Task<List<UnpaidInvoiceDto>> GetMyUnpaidInvoicesAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.Reservation)
            .Include(i => i.Agreement)
            .Where(i => i.CustomerId == customerId &&
                        (i.Status == InvoiceStatusConstants.Open || i.Status == InvoiceStatusConstants.Draft ||
                         i.Status == InvoiceStatusConstants.PartiallyPaid || i.Status == InvoiceStatusConstants.Overdue) &&
                        i.TotalAmount > i.PaidAmount &&
                        (i.Reservation == null ||
                         ((i.Reservation.Status == ReservationStatusConstants.Pending ||
                           i.Reservation.Status == ReservationStatusConstants.AwaitingDeposit) &&
                          i.Reservation.HoldUntil > now)))
            .OrderBy(i => i.DueDate)
            .ThenByDescending(i => i.CreatedAt)
            .Select(i => new UnpaidInvoiceDto
            {
                InvoiceId = i.Id,
                InvoiceNo = i.InvoiceNo,
                ReservationId = i.ReservationId,
                ReservationCode = i.Reservation != null ? i.Reservation.ReservationCode : null,
                ReservationStatus = i.Reservation != null ? i.Reservation.Status : null,
                HoldUntil = i.Reservation != null ? i.Reservation.HoldUntil : null,
                AgreementNo = i.Agreement != null ? i.Agreement.AgreementNo : null,
                AmountDue = i.TotalAmount - i.PaidAmount,
                Currency = i.Currency,
                Status = i.Status,
                DueDate = i.DueDate,
                CreatedAt = i.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    private CheckoutResponse BuildCheckoutResponse(long paymentId, long reservationId, long invoiceId, string invoiceNo, decimal amount, int expiresInSeconds, string transferContent)
    {
        var qrUrl = VietQrUrlBuilder.Build(_vietQrConfig, amount, transferContent);
        return new CheckoutResponse
        {
            PaymentId = paymentId,
            ReservationId = reservationId,
            InvoiceId = invoiceId,
            InvoiceNo = invoiceNo,
            Amount = amount,
            Currency = PaymentConstants.CurrencyVnd,
            PaymentMethod = PaymentConstants.MethodBankTransfer,
            ExpiresInSeconds = expiresInSeconds,
            VietQr = new VietQrPaymentInfo
            {
                BankCode = _vietQrConfig.BankCode ?? string.Empty,
                AccountNo = _vietQrConfig.AccountNo ?? string.Empty,
                AccountName = _vietQrConfig.AccountName ?? string.Empty,
                Amount = amount,
                TransferContent = transferContent,
                QrImageUrl = qrUrl
            }
        };
    }

    private async Task<Invoice?> MatchInvoiceAsync(string contentText, Match prefixMatch, CancellationToken ct)
    {
        if (prefixMatch.Success && long.TryParse(prefixMatch.Groups[1].Value, out var invoiceId))
        {
            var matched = await LoadInvoiceGraphAsync(i => i.Id == invoiceId, ct);
            if (matched != null) return matched;
        }

        var invMatch = Regex.Match(contentText, @"INV[\s\-_]*([0-9A-Za-z\-_]+)", RegexOptions.IgnoreCase);
        if (invMatch.Success)
        {
            var matchedCode = invMatch.Groups[0].Value.Replace(" ", "");
            var exact = await LoadInvoiceGraphAsync(i => i.InvoiceNo == matchedCode, ct);
            if (exact != null) return exact;

            var matched = await LoadInvoiceGraphAsync(i => i.InvoiceNo.Contains(matchedCode) || matchedCode.Contains(i.InvoiceNo), ct);
            if (matched != null) return matched;
        }

        var rsvMatch = Regex.Match(contentText, @"RSV[\s\-_]*([0-9A-Za-z\-_]+)", RegexOptions.IgnoreCase);
        if (rsvMatch.Success)
        {
            var matchedRsv = rsvMatch.Groups[0].Value.Replace(" ", "");
            var exact = await LoadInvoiceGraphAsync(i => i.Reservation != null && i.Reservation.ReservationCode == matchedRsv, ct);
            if (exact != null) return exact;

            return await LoadInvoiceGraphAsync(i => i.Reservation != null &&
                (i.Reservation.ReservationCode.Contains(matchedRsv) || matchedRsv.Contains(i.Reservation.ReservationCode)), ct);
        }

        return null;
    }

    private Task<Invoice?> LoadInvoiceGraphAsync(System.Linq.Expressions.Expression<Func<Invoice, bool>> predicate, CancellationToken ct) =>
        _dbContext.Invoices
            .Include(i => i.Reservation).ThenInclude(r => r!.Facility)
            .Include(i => i.Reservation).ThenInclude(r => r!.UnitType)
            .Include(i => i.Customer).ThenInclude(c => c.User)
            .FirstOrDefaultAsync(predicate, ct);

    private async Task<SePayWebhookResultDto?> TryProcessRenewalWebhookAsync(
        string contentText,
        Match prefixMatch,
        SePayWebhookPayload payload,
        string externalEventId,
        CancellationToken ct)
    {
        RentalRenewal? renewal = null;
        var rnwMatch = Regex.Match(contentText, @"RNW[\s\-_]*(\d+)", RegexOptions.IgnoreCase);
        if (rnwMatch.Success && long.TryParse(rnwMatch.Groups[1].Value, out var parsedRnwId))
            renewal = await _dbContext.RentalRenewals.Include(r => r.Agreement).FirstOrDefaultAsync(r => r.Id == parsedRnwId, ct);

        if (renewal == null && prefixMatch.Success && long.TryParse(prefixMatch.Groups[1].Value, out var prefixNum))
            renewal = await _dbContext.RentalRenewals.Include(r => r.Agreement).FirstOrDefaultAsync(r => r.Id == prefixNum, ct);

        if (renewal == null) return null;

        var renewalPeriod = $"{PaymentConstants.RenewalInvoicePrefix}{renewal.Id}";
        var renewalInvoice = await _dbContext.Invoices
            .FirstOrDefaultAsync(i => i.AgreementId == renewal.AgreementId
                                   && i.BillingPeriod == renewalPeriod
                                   && i.Status != InvoiceStatusConstants.Voided, ct);

        var months = Math.Max(1, (renewal.RequestedEndDate.Year - renewal.OldEndDate.Year) * 12 + renewal.RequestedEndDate.Month - renewal.OldEndDate.Month);
        var renewalAmount = renewalInvoice != null
            ? Math.Max(0m, renewalInvoice.TotalAmount - renewalInvoice.PaidAmount)
            : (renewal.NewMonthlyRate ?? renewal.OldMonthlyRate) * months;

        if (payload.TransferAmount != renewalAmount)
        {
            _logger.LogWarning("Renewal #{RenewalId} received {ReceivedAmount:N0} VND but requires {ExpectedAmount:N0} VND.", renewal.Id, payload.TransferAmount, renewalAmount);
            return Unmatched();
        }

        var renewalEventId = !string.IsNullOrWhiteSpace(payload.ReferenceCode)
            ? payload.ReferenceCode.Trim()
            : $"renewal-{payload.Id}";

        if (await _dbContext.IntegrationEvents.AnyAsync(e =>
                (e.Source == "sepay_renewal" || e.Source == "sepay") &&
                (e.ExternalEventId == renewalEventId || e.ExternalEventId == externalEventId) &&
                e.Status == "processed", ct))
        {
            return new SePayWebhookResultDto { Processed = true, Action = "duplicate_ignored" };
        }

        var nowTime = DateTimeOffset.UtcNow;

        if (renewalInvoice != null && renewalInvoice.Status != InvoiceStatusConstants.Paid)
        {
            var payment = await _dbContext.Payments
                .FirstOrDefaultAsync(p => p.TargetInvoiceId == renewalInvoice.Id && p.Status == PaymentConstants.StatusPending, ct);

            if (payment == null)
            {
                payment = new Payment
                {
                    CustomerId = renewalInvoice.CustomerId,
                    TargetInvoiceId = renewalInvoice.Id,
                    Amount = payload.TransferAmount,
                    Currency = renewalInvoice.Currency ?? PaymentConstants.CurrencyVnd,
                    Method = PaymentConstants.MethodBankTransfer,
                    Provider = PaymentConstants.ProviderSePay,
                    ProviderTransactionId = renewalEventId,
                    IdempotencyKey = $"RNW-{renewal.Id}-{renewalEventId}",
                    Status = PaymentConstants.StatusSucceeded,
                    Metadata = "{}",
                    PaidAt = nowTime,
                    CreatedAt = nowTime,
                    UpdatedAt = nowTime
                };
                _dbContext.Payments.Add(payment);
            }
            else
            {
                payment.Status = PaymentConstants.StatusSucceeded;
                payment.PaidAt = nowTime;
                payment.ProviderTransactionId = renewalEventId;
                payment.UpdatedAt = nowTime;
            }
            await _dbContext.SaveChangesAsync(ct);

            var hasAlloc = await _dbContext.PaymentAllocations
                .AnyAsync(pa => pa.PaymentId == payment.Id && pa.InvoiceId == renewalInvoice.Id, ct);
            if (!hasAlloc && payload.TransferAmount > 0)
            {
                _dbContext.PaymentAllocations.Add(new PaymentAllocation
                {
                    PaymentId = payment.Id,
                    InvoiceId = renewalInvoice.Id,
                    AllocatedAmount = Math.Min(payload.TransferAmount, Math.Max(0m, renewalInvoice.TotalAmount - renewalInvoice.PaidAmount)),
                    AllocatedAt = nowTime
                });
                await _dbContext.SaveChangesAsync(ct);
            }

            renewalInvoice.PaidAmount = renewalInvoice.TotalAmount;
            renewalInvoice.Status = InvoiceStatusConstants.Paid;
            renewalInvoice.UpdatedAt = nowTime;
        }

        await ApplyRenewalAndCureAgreementAsync(renewal, renewalInvoice?.Id, nowTime, ct);

        _dbContext.IntegrationEvents.Add(new IntegrationEvent
        {
            Source = "sepay_renewal",
            ExternalEventId = renewalEventId,
            EventType = "renewal_payment",
            Payload = $"{{\"renewalId\":{renewal.Id},\"amount\":{payload.TransferAmount.ToString(CultureInfo.InvariantCulture)}}}",
            Status = IntegrationEventStatus.Processed.ToDbValue(),
            ReceivedAt = nowTime,
            ProcessedAt = nowTime
        });

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Renewal #{RenewalId} confirmed and agreement restored via SePay webhook.", renewal.Id);
        return new SePayWebhookResultDto
        {
            Processed = true,
            Action = "renewal_confirmed",
            InvoiceId = renewalInvoice?.Id
        };
    }

    private async Task ApplyRenewalAndCureAgreementAsync(
        RentalRenewal renewal,
        long? renewalInvoiceId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        renewal.Status = RenewalStatusConstants.Approved;
        renewal.ApprovedEndDate = renewal.RequestedEndDate;
        renewal.ReviewedAt = now;

        var agreement = renewal.Agreement ?? await _dbContext.RentalAgreements
            .FirstOrDefaultAsync(a => a.Id == renewal.AgreementId, ct);

        if (agreement != null)
        {
            agreement.EndDate = renewal.RequestedEndDate;
            if (renewal.NewMonthlyRate.HasValue && renewal.NewMonthlyRate.Value > 0)
            {
                agreement.MonthlyRateSnapshot = renewal.NewMonthlyRate.Value;
            }
            if (agreement.Status is RentalAgreementStatusConstants.ExpiringSoon
                or RentalAgreementStatusConstants.Overdue
                or "extended"
                or "expired")
            {
                agreement.Status = RentalAgreementStatusConstants.Active;
            }
            agreement.UpdatedAt = now;

            // Settle carried-over past-due non-transfer invoices covered by renewal
            var overdueInvoices = await _dbContext.Invoices
                .Where(i => i.AgreementId == agreement.Id
                            && (renewalInvoiceId == null || i.Id != renewalInvoiceId.Value)
                            && (i.Status == InvoiceStatusConstants.Overdue || i.Status == InvoiceStatusConstants.Open || i.Status == InvoiceStatusConstants.PartiallyPaid)
                            && (i.BillingPeriod == null || (!i.BillingPeriod.StartsWith(TransferConstants.BillingPeriodPrefix)
                                                             && !i.BillingPeriod.StartsWith(PaymentConstants.RenewalInvoicePrefix))))
                .ToListAsync(ct);

            foreach (var ovd in overdueInvoices)
            {
                if (ovd.Status == InvoiceStatusConstants.Overdue || ovd.DueDate <= DateOnly.FromDateTime(now.UtcDateTime))
                {
                    var isAutoRenewalInvoice = !string.IsNullOrWhiteSpace(ovd.BillingPeriod)
                        && (ovd.BillingPeriod.StartsWith("AUTO-RNW-", StringComparison.OrdinalIgnoreCase)
                            || ovd.BillingPeriod.StartsWith("AUTO-OVD-", StringComparison.OrdinalIgnoreCase));

                    if (isAutoRenewalInvoice && ovd.PaidAmount <= 0m)
                    {
                        ovd.Status = InvoiceStatusConstants.Voided;
                    }
                    else
                    {
                        ovd.PaidAmount = ovd.TotalAmount;
                        ovd.Status = InvoiceStatusConstants.Paid;
                    }
                    ovd.UpdatedAt = now;
                }
            }
        }

        var activeAllocations = await _dbContext.UnitAllocations
            .Where(ua => ua.AgreementId == renewal.AgreementId && ua.Status == AllocationStatusConstants.Active)
            .ToListAsync(ct);
        foreach (var alloc in activeAllocations)
        {
            alloc.AllocationEndDate = renewal.RequestedEndDate;
        }

        // Cure any open delinquency cases on this agreement
        var openCases = await _dbContext.DelinquencyCases
            .Where(dc => dc.AgreementId == renewal.AgreementId && dc.ResolvedAt == null
                         && dc.Status != DelinquencyCaseStatusConstants.Cured && dc.Status != DelinquencyCaseStatusConstants.Waived)
            .ToListAsync(ct);

        foreach (var dc in openCases)
        {
            dc.Status = DelinquencyCaseStatusConstants.Cured;
            dc.OutstandingSnapshot = 0m;
            dc.ResolvedAt = now;
            _dbContext.DelinquencyActions.Add(new DelinquencyAction
            {
                DelinquencyCaseId = dc.Id,
                ActionType = DelinquencyActionTypeConstants.Note,
                Details = JsonSerializer.Serialize(new { note = $"Delinquency cured automatically by renewal #{renewal.Id}.", system = true }),
                PerformedBy = null,
                OccurredAt = now
            });
        }

        // Restore any suspended PIN / access credentials
        var suspendedCreds = await _dbContext.AccessCredentials
            .Where(ac => ac.AgreementId == renewal.AgreementId && ac.Status == CredentialStatusConstants.Suspended)
            .ToListAsync(ct);

        foreach (var cred in suspendedCreds)
        {
            cred.Status = CredentialStatusConstants.Active;
            cred.RevokedAt = null;
        }

        if (_realtimeNotifier != null && agreement != null)
        {
            await _realtimeNotifier.SendNotificationToUserAsync(
                agreement.CustomerId,
                $"Gia hạn hợp đồng #{agreement.AgreementNo} thành công",
                $"Hợp đồng #{agreement.AgreementNo} đã được gia hạn đến {renewal.RequestedEndDate:dd/MM/yyyy} và quyền truy cập đã được khôi phục.",
                new { agreementId = agreement.Id, renewalId = renewal.Id, newEndDate = renewal.RequestedEndDate });
        }
    }

    private async Task TryCureOverdueAgreementOnInvoiceSettledAsync(long agreementId, DateTimeOffset now, CancellationToken ct, Invoice? settledInvoice = null)
    {
        var agreement = await _dbContext.RentalAgreements
            .Include(a => a.Facility)
            .Include(a => a.Invoices)
            .FirstOrDefaultAsync(a => a.Id == agreementId, ct);

        if (agreement == null) return;

        var isAutoRenewalPaid = settledInvoice != null
            && !string.IsNullOrWhiteSpace(settledInvoice.BillingPeriod)
            && (settledInvoice.BillingPeriod.StartsWith("AUTO-RNW-", StringComparison.OrdinalIgnoreCase)
                || settledInvoice.BillingPeriod.StartsWith("AUTO-OVD-", StringComparison.OrdinalIgnoreCase));

        if (isAutoRenewalPaid)
        {
            agreement.EndDate = agreement.EndDate.AddMonths(1);
            agreement.UpdatedAt = now;

            var activeAllocations = await _dbContext.UnitAllocations
                .Where(ua => ua.AgreementId == agreementId && ua.Status == AllocationStatusConstants.Active)
                .ToListAsync(ct);
            foreach (var alloc in activeAllocations)
            {
                alloc.AllocationEndDate = agreement.EndDate;
            }
        }

        var tz = TimeZoneHelper.ResolveTimeZone(agreement.Facility?.Timezone, _rentalSettings.DefaultTimezone);
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, tz).DateTime);

        var hasRemainingDebt = agreement.Invoices.Any(i =>
            (i.Status == InvoiceStatusConstants.Overdue || ((i.Status == InvoiceStatusConstants.Open || i.Status == InvoiceStatusConstants.PartiallyPaid) && i.DueDate < localToday))
            && (i.TotalAmount - i.PaidAmount) > 0
            && (i.BillingPeriod == null || !i.BillingPeriod.StartsWith(TransferConstants.BillingPeriodPrefix, StringComparison.OrdinalIgnoreCase)));

        if (hasRemainingDebt) return;

        var openCases = await _dbContext.DelinquencyCases
            .Where(dc => dc.AgreementId == agreementId && dc.ResolvedAt == null
                         && dc.Status != DelinquencyCaseStatusConstants.Cured && dc.Status != DelinquencyCaseStatusConstants.Waived)
            .ToListAsync(ct);

        foreach (var dc in openCases)
        {
            dc.Status = DelinquencyCaseStatusConstants.Cured;
            dc.OutstandingSnapshot = 0m;
            dc.ResolvedAt = now;
            _dbContext.DelinquencyActions.Add(new DelinquencyAction
            {
                DelinquencyCaseId = dc.Id,
                ActionType = DelinquencyActionTypeConstants.Note,
                Details = JsonSerializer.Serialize(new { note = "Overdue invoices settled via payment.", system = true }),
                PerformedBy = null,
                OccurredAt = now
            });
        }

        var suspendedCreds = await _dbContext.AccessCredentials
            .Where(ac => ac.AgreementId == agreementId && ac.Status == CredentialStatusConstants.Suspended)
            .ToListAsync(ct);

        foreach (var cred in suspendedCreds)
        {
            cred.Status = CredentialStatusConstants.Active;
            cred.RevokedAt = null;
        }

        if (agreement.Status is RentalAgreementStatusConstants.Overdue or RentalAgreementStatusConstants.ExpiringSoon or "extended" or "expired"
            && agreement.EndDate >= localToday)
        {
            agreement.Status = RentalAgreementStatusConstants.Active;
            agreement.UpdatedAt = now;
        }

        if (agreement.DepositBalance < 0)
        {
            agreement.DepositBalance = 0m;
            agreement.NegativeDebtNotifiedAt = null;
            agreement.UpdatedAt = now;
        }

        if (agreement.Status == RentalAgreementStatusConstants.CheckoutPending)
        {
            var hasAnyUnpaid = agreement.Invoices.Any(i =>
                (i.Status == InvoiceStatusConstants.Open || i.Status == InvoiceStatusConstants.Overdue || i.Status == InvoiceStatusConstants.PartiallyPaid)
                && (i.TotalAmount - i.PaidAmount) > 0);

            var hasPendingRefunds = await _dbContext.Refunds
                .AnyAsync(r => r.AgreementId == agreementId && (r.Status == RefundStatusConstants.Pending || r.Status == RefundStatusConstants.PendingManagerPayout || r.Status == RefundStatusConstants.PendingCounterPayout), ct);

            if (!hasAnyUnpaid && !hasPendingRefunds)
            {
                agreement.Status = RentalAgreementStatusConstants.Completed;
                agreement.ActualEndDate ??= localToday;
                agreement.UpdatedAt = now;

                var moveOut = await _dbContext.MoveOutRequests
                    .FirstOrDefaultAsync(m => m.AgreementId == agreementId && m.Status != MoveOutStatusConstants.Completed && m.Status != MoveOutStatusConstants.Cancelled, ct);
                if (moveOut != null)
                {
                    moveOut.Status = MoveOutStatusConstants.Completed;
                    moveOut.FinalizedAt = now;
                    moveOut.UpdatedAt = now;
                }
            }
        }
    }

    private async Task ExecutePaidUnitTransferAsync(
        UnitTransferRequest transfer,
        DateTimeOffset now,
        CancellationToken ct)
    {
        transfer.Status = TransferRequestStatusConstants.Completed;
        transfer.CompletedAt = now;

        var agreement = transfer.Agreement;
        if (agreement == null)
        {
            agreement = await _dbContext.RentalAgreements
                .Include(a => a.UnitAllocations)
                .FirstOrDefaultAsync(a => a.Id == transfer.AgreementId, ct);
        }

        if (agreement == null) return;

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var activeAlloc = agreement.UnitAllocations
            .FirstOrDefault(ua => ua.Status == AllocationStatusConstants.Active);

        if (activeAlloc != null)
        {
            activeAlloc.Status = AllocationStatusConstants.Released;
            activeAlloc.EndedAt = now;
            await _dbContext.SaveChangesAsync(ct);
        }

        if (transfer.FromUnit != null)
        {
            transfer.FromUnit.PhysicalStatus = StorageUnitStatusConstants.Available;
            transfer.FromUnit.UpdatedAt = now;
        }

        if (transfer.ToUnit != null)
        {
            transfer.ToUnit.PhysicalStatus = StorageUnitStatusConstants.Occupied;
            transfer.ToUnit.UpdatedAt = now;

            _dbContext.UnitAllocations.Add(new UnitAllocation
            {
                AgreementId = agreement.Id,
                StorageUnitId = transfer.ToUnit.Id,
                AllocationKind = AllocationKindConstants.Rental,
                AllocationStartDate = today,
                AllocationEndDate = agreement.EndDate,
                Status = AllocationStatusConstants.Active,
                CreatedAt = now
            });

            agreement.FacilityId = transfer.ToUnit.FacilityId;
        }

        var targetRate = await _dbContext.FacilityRates
            .AsNoTracking()
            .Where(r => r.FacilityId == transfer.RequestedFacilityId
                     && r.UnitTypeId == transfer.RequestedUnitTypeId
                     && r.ValidFrom <= today
                     && (r.ValidTo == null || r.ValidTo >= today))
            .OrderByDescending(r => r.ValidFrom)
            .FirstOrDefaultAsync(ct);

        if (targetRate != null && targetRate.MonthlyRate > 0)
        {
            agreement.MonthlyRateSnapshot = targetRate.MonthlyRate;
        }

        if (transfer.DiffDepositAmount > 0)
        {
            agreement.DepositSnapshot += transfer.DiffDepositAmount;
            agreement.DepositBalance += transfer.DiffDepositAmount;
        }

        agreement.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task<SePayWebhookResultDto> CompletePaymentWithResultAsync(
        Payment payment,
        string transactionNo,
        string externalEventId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var invoice = payment.TargetInvoice;
        if (invoice == null && payment.TargetInvoiceId > 0)
        {
            invoice = await LoadInvoiceGraphAsync(i => i.Id == payment.TargetInvoiceId, cancellationToken);
            if (invoice != null) payment.TargetInvoice = invoice;
        }

        var customer = payment.Customer ?? invoice?.Customer;
        var reservation = invoice?.Reservation;

        var isLatePayment = invoice?.Status == InvoiceStatusConstants.Voided ||
            (reservation != null && reservation.Status is ReservationStatusConstants.Expired or ReservationStatusConstants.Cancelled);

        if (isLatePayment)
        {
            _logger.LogWarning(PaymentLogMessages.LatePaymentState,
                reservation?.ReservationCode ?? invoice?.InvoiceNo, reservation?.Status ?? invoice?.Status);

            IDbContextTransaction? lateTx = _dbContext.Database.IsSqlServer()
                ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
                : null;

            try
            {
                payment.Status = PaymentConstants.StatusSucceeded;
                payment.PaidAt = now;
                payment.ProviderTransactionId = transactionNo;
                payment.Metadata = JsonSerializer.Serialize(new
                {
                    note = "Late payment received after invoice or reservation was closed. Manual reconciliation required.",
                    isLatePayment = true,
                    requiresReconciliation = true,
                    invoiceStatus = invoice?.Status,
                    reservationStatus = reservation?.Status
                });
                payment.UpdatedAt = now;
                await _dbContext.SaveChangesAsync(cancellationToken);

                if (payment.Amount > 0)
                {
                    _dbContext.Refunds.Add(new Refund
                    {
                        PaymentId = payment.Id,
                        AgreementId = null,
                        Amount = payment.Amount,
                        Currency = payment.Currency ?? PaymentConstants.CurrencyVnd,
                        Provider = PaymentConstants.ProviderSePay,
                        IdempotencyKey = $"REFUND-LATE-{payment.Id}",
                        Reason = "Late payment received after reservation hold expired or invoice voided.",
                        Status = RefundStatusConstants.Pending,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }

                await RecordIntegrationEventAsync("sepay", externalEventId, "late_payment", invoice?.Id ?? 0, payment.Amount, IntegrationEventStatus.Processed, cancellationToken, save: false);
                await _dbContext.SaveChangesAsync(cancellationToken);

                if (lateTx != null) await lateTx.CommitAsync(cancellationToken);

                await NotifyLatePaymentAsync(payment, customer, reservation, cancellationToken);

                return new SePayWebhookResultDto
                {
                    Processed = true,
                    Action = "late_payment_flagged",
                    InvoiceId = invoice?.Id,
                    ReservationId = reservation?.Id,
                    PaymentId = payment.Id
                };
            }
            catch (Exception ex)
            {
                if (lateTx != null) await lateTx.RollbackAsync(cancellationToken);
                _logger.LogError(ex, PaymentLogMessages.LatePaymentFailed, payment.Id, ex.Message);
                throw;
            }
            finally
            {
                if (lateTx != null) await lateTx.DisposeAsync();
            }
        }

        IDbContextTransaction? transaction = _dbContext.Database.IsSqlServer()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            payment.Status = PaymentConstants.StatusSucceeded;
            payment.PaidAt = now;
            payment.ProviderTransactionId = transactionNo;
            payment.UpdatedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var remainingBalance = invoice != null ? Math.Max(0m, invoice.TotalAmount - invoice.PaidAmount) : payment.Amount;
            var allocatedAmount = Math.Min(payment.Amount, remainingBalance);
            var overpaymentAmount = Math.Max(0m, payment.Amount - remainingBalance);

            var existingAllocation = await _dbContext.PaymentAllocations
                .FirstOrDefaultAsync(pa => pa.PaymentId == payment.Id && pa.InvoiceId == payment.TargetInvoiceId, cancellationToken);

            if (existingAllocation == null && allocatedAmount > 0)
            {
                _dbContext.PaymentAllocations.Add(new PaymentAllocation
                {
                    PaymentId = payment.Id,
                    InvoiceId = payment.TargetInvoiceId,
                    AllocatedAmount = allocatedAmount,
                    AllocatedAt = now
                });
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            var isTransfer = false;
            if (invoice != null)
            {
                invoice.PaidAmount += allocatedAmount;
                invoice.Status = invoice.PaidAmount >= invoice.TotalAmount
                    ? InvoiceStatusConstants.Paid
                    : (invoice.PaidAmount > 0 ? InvoiceStatusConstants.PartiallyPaid : invoice.Status);
                invoice.UpdatedAt = now;

                if (invoice.Status == InvoiceStatusConstants.Paid &&
                    !string.IsNullOrWhiteSpace(invoice.BillingPeriod) &&
                    invoice.BillingPeriod.StartsWith("TRANSFER:", StringComparison.OrdinalIgnoreCase) &&
                    long.TryParse(invoice.BillingPeriod["TRANSFER:".Length..], out var transferId))
                {
                    var transfer = await _dbContext.UnitTransferRequests
                        .Include(t => t.Agreement)
                            .ThenInclude(a => a.UnitAllocations)
                        .Include(t => t.FromUnit)
                        .Include(t => t.ToUnit)
                        .FirstOrDefaultAsync(t => t.Id == transferId, cancellationToken);
                    if (transfer != null && transfer.Status == TransferRequestStatusConstants.PendingPayment)
                    {
                        await ExecutePaidUnitTransferAsync(transfer, now, cancellationToken);
                        isTransfer = true;
                    }
                }
                else if (invoice.Status == InvoiceStatusConstants.Paid &&
                    !string.IsNullOrWhiteSpace(invoice.BillingPeriod) &&
                    invoice.BillingPeriod.StartsWith(PaymentConstants.RenewalInvoicePrefix, StringComparison.OrdinalIgnoreCase) &&
                    long.TryParse(invoice.BillingPeriod[PaymentConstants.RenewalInvoicePrefix.Length..], out var paidRenewalId))
                {
                    var renewal = await _dbContext.RentalRenewals
                        .Include(r => r.Agreement)
                        .FirstOrDefaultAsync(r => r.Id == paidRenewalId, cancellationToken);
                    if (renewal != null)
                    {
                        await ApplyRenewalAndCureAgreementAsync(renewal, invoice.Id, now, cancellationToken);
                    }
                }
                else if (invoice.Status == InvoiceStatusConstants.Paid && invoice.AgreementId.HasValue)
                {
                    await TryCureOverdueAgreementOnInvoiceSettledAsync(invoice.AgreementId.Value, now, cancellationToken, invoice);
                }
            }

            if (overpaymentAmount > 0)
            {
                payment.Metadata = JsonSerializer.Serialize(new
                {
                    overpaymentAmount,
                    requiresReconciliation = true,
                    note = "Overpayment detected; excess amount queued for refund."
                });

                _dbContext.Refunds.Add(new Refund
                {
                    PaymentId = payment.Id,
                    AgreementId = invoice?.AgreementId,
                    Amount = overpaymentAmount,
                    Currency = payment.Currency ?? PaymentConstants.CurrencyVnd,
                    Provider = PaymentConstants.ProviderSePay,
                    IdempotencyKey = $"REFUND-OVERPAY-{payment.Id}",
                    Reason = $"Overpayment refund ({overpaymentAmount:N0} VND excess over invoice balance).",
                    Status = RefundStatusConstants.Pending,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            var otherPendingPayments = await _dbContext.Payments
                .Where(p => p.TargetInvoiceId == payment.TargetInvoiceId && p.Id != payment.Id && p.Status == PaymentConstants.StatusPending)
                .ToListAsync(cancellationToken);
            foreach (var opp in otherPendingPayments)
            {
                opp.Status = PaymentConstants.StatusCancelled;
                opp.FailureReason = "Superseded by completed transaction.";
                opp.UpdatedAt = now;
            }

            RentalAgreement? createdAgreement = null;
            if (reservation != null && reservation.Status != ReservationStatusConstants.Confirmed)
            {
                reservation.Status = ReservationStatusConstants.Confirmed;
                reservation.ConfirmedAt = now;
                reservation.UpdatedAt = now;

                var redemptions = await _dbContext.PromotionRedemptions
                    .Where(pr => pr.ReservationId == reservation.Id && pr.Status == PromotionRedemptionStatusConstants.Reserved)
                    .ToListAsync(cancellationToken);
                foreach (var red in redemptions)
                    red.Status = PromotionRedemptionStatusConstants.Applied;

                createdAgreement = await _dbContext.RentalAgreements
                    .FirstOrDefaultAsync(a => a.ReservationId == reservation.Id, cancellationToken);

                if (createdAgreement == null)
                {
                    var policyVersionId = await _dbContext.PolicyVersions
                        .Where(p => p.PolicyType == "rental_terms")
                        .OrderByDescending(p => p.ValidFrom)
                        .Select(p => p.Id)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (policyVersionId <= 0)
                    {
                        policyVersionId = await _dbContext.PolicyVersions
                            .OrderByDescending(p => p.ValidFrom)
                            .Select(p => p.Id)
                            .FirstOrDefaultAsync(cancellationToken);
                    }
                    if (policyVersionId <= 0) policyVersionId = 1;

                    createdAgreement = new RentalAgreement
                    {
                        AgreementNo = $"AGR-HCM-{now:yyyyMMdd}-{reservation.Id:D4}",
                        ReservationId = reservation.Id,
                        CustomerId = reservation.CustomerId,
                        FacilityId = reservation.FacilityId,
                        PolicyVersionId = policyVersionId,
                        StartDate = reservation.StartDate,
                        EndDate = reservation.EndDate,
                        MonthlyRateSnapshot = reservation.MonthlyRateSnapshot,
                        DepositSnapshot = reservation.DepositSnapshot,
                        DepositBalance = reservation.DepositSnapshot,
                        Status = RentalAgreementStatusConstants.Draft,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _dbContext.RentalAgreements.Add(createdAgreement);
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    if (invoice != null) invoice.AgreementId = createdAgreement.Id;
                }
            }

            await RecordIntegrationEventAsync("sepay", externalEventId, "payment_settled", invoice?.Id ?? 0, payment.Amount, IntegrationEventStatus.Processed, cancellationToken, save: false);

            await PersistConfirmedNotificationsAsync(payment, invoice, reservation, now, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (transaction != null) await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(PaymentLogMessages.PaymentCompleted,
                payment.Id, payment.TargetInvoiceId, reservation?.ReservationCode ?? "N/A");

            await DispatchRealtimeAndEmailSuccessAsync(payment, invoice, reservation, customer, createdAgreement, cancellationToken);

            var action = overpaymentAmount > 0
                ? "overpayment_refund_created"
                : (isTransfer ? "transfer_confirmed" : "confirmed_full");

            return new SePayWebhookResultDto
            {
                Processed = true,
                Action = action,
                InvoiceId = invoice?.Id,
                ReservationId = reservation?.Id,
                PaymentId = payment.Id
            };
        }
        catch (DbUpdateException ex)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            if (await IsDuplicateEventAsync(externalEventId, transactionNo, cancellationToken))
            {
                return new SePayWebhookResultDto
                {
                    Processed = true,
                    Action = "duplicate_ignored",
                    InvoiceId = invoice?.Id,
                    ReservationId = reservation?.Id,
                    PaymentId = payment.Id
                };
            }

            _logger.LogError(ex, PaymentLogMessages.PaymentCompletionError, payment.Id, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, PaymentLogMessages.PaymentCompletionError, payment.Id, ex.Message);
            throw;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    private async Task<bool> IsDuplicateEventAsync(string externalEventId, string transactionRef, CancellationToken ct)
    {
        if (await _dbContext.IntegrationEvents.AsNoTracking().AnyAsync(e =>
                (e.Source == "sepay" || e.Source == "sepay_renewal") &&
                e.ExternalEventId == externalEventId &&
                e.Status == "processed", ct))
        {
            return true;
        }

        return await _dbContext.Payments.AsNoTracking().AnyAsync(p =>
            p.Provider == PaymentConstants.ProviderSePay &&
            p.ProviderTransactionId == transactionRef &&
            p.Status == PaymentConstants.StatusSucceeded, ct);
    }

    private async Task RecordIntegrationEventAsync(
        string source,
        string externalEventId,
        string eventType,
        long invoiceId,
        decimal amount,
        IntegrationEventStatus status,
        CancellationToken ct,
        bool save = true)
    {
        var exists = await _dbContext.IntegrationEvents.AnyAsync(e => e.Source == source && e.ExternalEventId == externalEventId, ct);
        if (exists) return;

        var now = DateTimeOffset.UtcNow;
        _dbContext.IntegrationEvents.Add(new IntegrationEvent
        {
            Source = source,
            ExternalEventId = externalEventId,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(new { invoiceId, amount }),
            Status = status.ToDbValue(),
            ReceivedAt = now,
            ProcessedAt = now
        });

        if (save) await _dbContext.SaveChangesAsync(ct);
    }

    private async Task PersistConfirmedNotificationsAsync(
        Payment payment,
        Invoice? invoice,
        Reservation? reservation,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (reservation == null) return;

        var custUserExists = await _dbContext.Users.AsNoTracking().AnyAsync(u => u.Id == reservation.CustomerId, ct);
        if (custUserExists)
        {
            _dbContext.Notifications.Add(new Notification
            {
                UserId = reservation.CustomerId,
                Channel = NotificationChannel.InApp.ToDbValue(),
                TemplateCode = "reservation_confirmed",
                Subject = $"Reservation {reservation.ReservationCode} confirmed",
                Payload = JsonSerializer.Serialize(new { reservationId = reservation.Id, reservationCode = reservation.ReservationCode, paymentId = payment.Id, invoiceId = invoice?.Id }),
                Status = NotificationStatus.Sent.ToDbValue(),
                ScheduledAt = now,
                SentAt = now,
                Attempts = 1,
                DeduplicationKey = $"rsv-conf-cust-{reservation.Id}-{payment.Id}",
                CreatedAt = now
            });
        }

        var assignedStaffIds = await _dbContext.StaffFacilityAssignments
            .AsNoTracking()
            .Where(a => a.FacilityId == reservation.FacilityId && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
            .Select(a => a.EmployeeId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var staffId in assignedStaffIds)
        {
            _dbContext.Notifications.Add(new Notification
            {
                UserId = staffId,
                Channel = NotificationChannel.InApp.ToDbValue(),
                TemplateCode = "staff_new_confirmed_reservation",
                Subject = $"New confirmed booking {reservation.ReservationCode}",
                Payload = JsonSerializer.Serialize(new { reservationId = reservation.Id, reservationCode = reservation.ReservationCode, facilityId = reservation.FacilityId }),
                Status = NotificationStatus.Sent.ToDbValue(),
                ScheduledAt = now,
                SentAt = now,
                Attempts = 1,
                DeduplicationKey = $"rsv-conf-staff-{reservation.Id}-{staffId}",
                CreatedAt = now
            });
        }
    }

    private async Task DispatchRealtimeAndEmailSuccessAsync(
        Payment payment,
        Invoice? invoice,
        Reservation? reservation,
        CustomerProfile? customer,
        RentalAgreement? agreement,
        CancellationToken cancellationToken)
    {
        if (_realtimeNotifier != null)
        {
            var qrToken = reservation != null
                ? _qrTokenService?.IssueToken("checkin", reservation.Id, agreement?.Id, reservation.CustomerId, reservation.FacilityId, TimeSpan.FromDays(90))
                : null;

            await _realtimeNotifier.SendPaymentSucceededAsync(payment.CustomerId, new
            {
                paymentId = payment.Id,
                invoiceId = payment.TargetInvoiceId,
                reservationId = reservation?.Id,
                amount = payment.Amount,
                status = payment.Status,
                checkInQrToken = qrToken
            });

            if (reservation != null)
            {
                await _realtimeNotifier.SendReservationConfirmedAsync(reservation.CustomerId, reservation.FacilityId, new
                {
                    reservationId = reservation.Id,
                    reservationCode = reservation.ReservationCode,
                    facilityId = reservation.FacilityId,
                    status = reservation.Status,
                    checkInQrToken = qrToken
                });

                await _realtimeNotifier.SendNotificationToUserAsync(
                    reservation.CustomerId,
                    "Payment confirmed",
                    $"Reservation {reservation.ReservationCode} has been confirmed.",
                    new { reservationId = reservation.Id, paymentId = payment.Id });
            }
        }

        var customerEmail = customer?.User?.Email;
        if (reservation != null && !string.IsNullOrWhiteSpace(customerEmail))
        {
            try
            {
                var customerName = customer?.FullName ?? "Valued Customer";
                var subject = $"[Self-Storage] Reservation Confirmed - Code: {reservation.ReservationCode}";
                var body = $@"
                    <h3>Dear {customerName},</h3>
                    <p>Thank you for completing your payment for your storage unit reservation!</p>
                    <ul>
                        <li><strong>Reservation Code:</strong> {reservation.ReservationCode}</li>
                        <li><strong>Facility:</strong> {reservation.Facility?.Name}</li>
                        <li><strong>Address:</strong> {reservation.Facility?.AddressLine}</li>
                        <li><strong>Unit Type:</strong> {reservation.UnitType?.Name}</li>
                        <li><strong>Paid Amount:</strong> {payment.Amount:N0} VND</li>
                        <li><strong>Start Date:</strong> {reservation.StartDate:yyyy-MM-dd}</li>
                    </ul>
                    <p><strong>Check-In Instructions:</strong> Please present your national ID/Passport and your Reservation Code on your mobile app when visiting our facility for handover as per BR-RSV-04.</p>
                    <p>Best regards,<br/>Self Storage System Support Team</p>";

                await _emailService.SendEmailAsync(customerEmail, subject, body, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, PaymentLogMessages.ConfirmationEmailFailed, customerEmail);
            }
        }
    }

    private async Task NotifyPartialPaymentAsync(Invoice invoice, Payment payment, CancellationToken ct)
    {
        if (_realtimeNotifier == null) return;
        await _realtimeNotifier.SendPaymentPartialReceivedAsync(invoice.CustomerId, new
        {
            paymentId = payment.Id,
            invoiceId = invoice.Id,
            reservationId = invoice.ReservationId,
            paidAmount = invoice.PaidAmount,
            totalAmount = invoice.TotalAmount,
            remainingAmount = Math.Max(0m, invoice.TotalAmount - invoice.PaidAmount)
        });
        await _realtimeNotifier.SendNotificationToUserAsync(
            invoice.CustomerId,
            "Partial payment received",
            $"Received {payment.Amount:N0} VND. Remaining balance: {Math.Max(0m, invoice.TotalAmount - invoice.PaidAmount):N0} VND.",
            new { paymentId = payment.Id, invoiceId = invoice.Id });
    }

    private async Task NotifyLatePaymentAsync(Payment payment, CustomerProfile? customer, Reservation? reservation, CancellationToken ct)
    {
        if (_realtimeNotifier != null)
        {
            await _realtimeNotifier.SendNotificationToUserAsync(
                payment.CustomerId,
                "Late payment queued for reconciliation",
                $"Payment of {payment.Amount:N0} VND was received after reservation hold expiry and queued for staff/manager reconciliation.",
                new { paymentId = payment.Id, reservationId = reservation?.Id, requiresReconciliation = true });
        }

        var userEmail = customer?.User?.Email;
        if (reservation != null && !string.IsNullOrWhiteSpace(userEmail))
        {
            try
            {
                var customerName = customer?.FullName ?? "Valued Customer";
                var subject = $"[Self-Storage] Payment Recorded for Reservation {reservation.ReservationCode} (Action Required)";
                var body = $@"
                    <h3>Dear {customerName},</h3>
                    <p>We have successfully received your payment of <strong>{payment.Amount:N0} VND</strong> via SePay.</p>
                    <p>However, the temporary hold period for reservation <strong>{reservation.ReservationCode}</strong> had expired before the transaction was confirmed, and the unit was released back to inventory per BR-RSV-01.</p>
                    <p>Our Customer Support team will reach out to you within 24 business hours to reassign an equivalent available storage unit or provide a full 100% refund.</p>
                    <p>Best regards,<br/>Self Storage System Support Team</p>";

                await _emailService.SendEmailAsync(userEmail, subject, body, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, PaymentLogMessages.LatePaymentEmailFailed, userEmail);
            }
        }
    }

    private static string ResolveExternalEventId(SePayWebhookPayload payload) =>
        payload.Id > 0
            ? payload.Id.ToString(CultureInfo.InvariantCulture)
            : (!string.IsNullOrWhiteSpace(payload.ReferenceCode)
                ? payload.ReferenceCode.Trim()
                : $"{payload.TransactionDate}:{payload.TransferAmount}:{payload.Content.GetHashCode()}");

    private static SePayWebhookResultDto Unmatched() => new() { Processed = false, Action = "unmatched_ignored" };

    private static bool NeedsReconciliation(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata)) return false;
        try
        {
            using var document = JsonDocument.Parse(metadata);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                ((root.TryGetProperty("requiresReconciliation", out var review) && review.ValueKind == JsonValueKind.True) ||
                 (root.TryGetProperty("isLatePayment", out var late) && late.ValueKind == JsonValueKind.True));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
