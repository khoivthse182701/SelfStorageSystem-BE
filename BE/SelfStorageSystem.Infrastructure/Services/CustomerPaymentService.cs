using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Customer.Payments;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public class CustomerPaymentService : ICustomerPaymentService
{
    private readonly SelfStorageDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly VietQrConfig _vietQrConfig;
    private readonly SePayConfig _sePayConfig;
    private readonly ILogger<CustomerPaymentService> _logger;

    public CustomerPaymentService(
        SelfStorageDbContext dbContext,
        IEmailService emailService,
        IOptions<PaymentSettings> paymentOptions,
        ILogger<CustomerPaymentService> logger)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _vietQrConfig = paymentOptions.Value.VietQr;
        _sePayConfig = paymentOptions.Value.SePay;
        _logger = logger;
    }

    public async Task<CheckoutResponse> CreateCheckoutAsync(
        long customerId,
        CreateCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        // 1. Verify reservation ownership and validity
        var reservation = await _dbContext.Reservations
            .Include(r => r.Facility)
            .Include(r => r.UnitType)
            .Include(r => r.Invoices)
            .FirstOrDefaultAsync(r => r.Id == request.ReservationId && r.CustomerId == customerId, cancellationToken);

        if (reservation == null)
        {
            throw AppException.FromError(PaymentErrors.ReservationNotFound);
        }

        // BR-RSV-01: Disallow payment if the hold period has expired
        if (reservation.HoldUntil < now ||
            reservation.Status == ReservationStatusConstants.Expired ||
            reservation.Status == ReservationStatusConstants.Cancelled)
        {
            throw AppException.FromError(PaymentErrors.HoldExpired);
        }

        if (reservation.Status == ReservationStatusConstants.Confirmed ||
            reservation.Status == ReservationStatusConstants.Converted)
        {
            throw AppException.FromError(PaymentErrors.AlreadyConfirmed);
        }

        // 2. Locate open/draft invoice
        var invoice = reservation.Invoices
            .FirstOrDefault(i => i.Status == InvoiceStatusConstants.Open || i.Status == InvoiceStatusConstants.Draft);

        if (invoice == null)
        {
            throw AppException.FromError(PaymentErrors.InvoiceNotFound);
        }

        var amountToPay = invoice.TotalAmount - invoice.PaidAmount;
        if (amountToPay <= 0)
        {
            throw AppException.FromError(PaymentErrors.InvoiceAlreadyPaid);
        }

        const string paymentMethod = PaymentConstants.MethodBankTransfer;
        const string provider = PaymentConstants.ProviderSePay;

        // 3. Reuse or create initial Payment record in pending status (prevents duplicate spam on refreshes)
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

        var expiresInSeconds = (int)Math.Max(0, (reservation.HoldUntil - now).TotalSeconds);

        // Dynamic invoice prefix configured from SePayConfig
        var prefix = !string.IsNullOrWhiteSpace(_sePayConfig.InvoicePrefix)
            ? _sePayConfig.InvoicePrefix.Trim()
            : SePayConstants.DefaultInvoicePrefix;
        var transferContent = $"{prefix}{invoice.Id}";

        var baseUrl = !string.IsNullOrWhiteSpace(_vietQrConfig.BaseUrl)
            ? _vietQrConfig.BaseUrl.TrimEnd('/')
            : SePayConstants.DefaultBaseUrl;

        var bankCode = Uri.EscapeDataString(_vietQrConfig.BankCode ?? SePayConstants.DefaultBankCode);
        var accNo = Uri.EscapeDataString(_vietQrConfig.AccountNo ?? string.Empty);
        var template = Uri.EscapeDataString(_vietQrConfig.Template ?? SePayConstants.DefaultTemplate);
        var showInfo = _vietQrConfig.ShowInfo ? "true" : "false";
        var holder = Uri.EscapeDataString(_vietQrConfig.AccountName ?? string.Empty);
        var memo = Uri.EscapeDataString(transferContent);

        var qrUrl = $"{baseUrl}?bank={bankCode}&acc={accNo}&template={template}&showinfo={showInfo}&holder={holder}&amount={(long)amountToPay}&memo={memo}";

        return new CheckoutResponse
        {
            PaymentId = payment.Id,
            ReservationId = reservation.Id,
            InvoiceId = invoice.Id,
            InvoiceNo = invoice.InvoiceNo,
            Amount = amountToPay,
            Currency = PaymentConstants.CurrencyVnd,
            PaymentMethod = paymentMethod,
            ExpiresInSeconds = expiresInSeconds,
            VietQr = new VietQrPaymentInfo
            {
                BankCode = _vietQrConfig.BankCode ?? string.Empty,
                AccountNo = _vietQrConfig.AccountNo ?? string.Empty,
                AccountName = _vietQrConfig.AccountName ?? string.Empty,
                Amount = amountToPay,
                TransferContent = transferContent,
                QrImageUrl = qrUrl
            }
        };
    }

    public async Task<bool> ProcessSePayWebhookAsync(
        SePayWebhookPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (payload == null || string.IsNullOrWhiteSpace(payload.Content))
        {
            return false;
        }

        // Validate positive incoming amount (prevent zero/negative or outgoing debit transactions from triggering payments)
        if (payload.TransferAmount <= 0)
        {
            _logger.LogWarning(PaymentLogMessages.NonPositiveTransfer, payload.TransferAmount);
            return false;
        }

        if (string.Equals(payload.TransferType, SePayConstants.TransferTypeOut, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(PaymentLogMessages.OutgoingDebitIgnored);
            return false;
        }

        var prefix = !string.IsNullOrWhiteSpace(_sePayConfig.InvoicePrefix)
            ? _sePayConfig.InvoicePrefix.Trim()
            : SePayConstants.DefaultInvoicePrefix;

        var contentText = $"{payload.Content} {payload.Description}";

        Invoice? invoice = null;

        // 1. Try extracting invoice ID via configured prefix (e.g. DH1004, DH-1004)
        var prefixMatch = Regex.Match(contentText, $@"{Regex.Escape(prefix)}[\s\-_]*(\d+)", RegexOptions.IgnoreCase);
        if (prefixMatch.Success && long.TryParse(prefixMatch.Groups[1].Value, out var invoiceId))
        {
            invoice = await _dbContext.Invoices
                .Include(i => i.Reservation)
                    .ThenInclude(r => r!.Facility)
                .Include(i => i.Reservation)
                    .ThenInclude(r => r!.UnitType)
                .Include(i => i.Customer)
                    .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
        }

        // 2. Try matching invoice number pattern (e.g. INV-20261001-01002)
        if (invoice == null)
        {
            var invMatch = Regex.Match(contentText, @"INV[\s\-_]*([0-9A-Za-z\-_]+)", RegexOptions.IgnoreCase);
            if (invMatch.Success)
            {
                var matchedCode = invMatch.Groups[0].Value.Replace(" ", "");
                invoice = await _dbContext.Invoices
                    .Include(i => i.Reservation)
                        .ThenInclude(r => r!.Facility)
                    .Include(i => i.Reservation)
                        .ThenInclude(r => r!.UnitType)
                    .Include(i => i.Customer)
                        .ThenInclude(c => c.User)
                    .FirstOrDefaultAsync(i => i.InvoiceNo.Contains(matchedCode) || matchedCode.Contains(i.InvoiceNo), cancellationToken);
            }
        }

        // 3. Try matching reservation code pattern (e.g. RSV-20261001-01002)
        if (invoice == null)
        {
            var rsvMatch = Regex.Match(contentText, @"RSV[\s\-_]*([0-9A-Za-z\-_]+)", RegexOptions.IgnoreCase);
            if (rsvMatch.Success)
            {
                var matchedRsv = rsvMatch.Groups[0].Value.Replace(" ", "");
                invoice = await _dbContext.Invoices
                    .Include(i => i.Reservation)
                        .ThenInclude(r => r!.Facility)
                    .Include(i => i.Reservation)
                        .ThenInclude(r => r!.UnitType)
                    .Include(i => i.Customer)
                        .ThenInclude(c => c.User)
                    .FirstOrDefaultAsync(i => i.Reservation != null && (i.Reservation.ReservationCode.Contains(matchedRsv) || matchedRsv.Contains(i.Reservation.ReservationCode)), cancellationToken);
            }
        }

        // 4. Fallback: If bank memo does not contain the invoice or reservation code (e.g. Zalopay/transfer without memo),
        // match against recent open/pending invoices with exact remaining balance equal to transfer amount.
        if (invoice == null)
        {
            invoice = await _dbContext.Invoices
                .Include(i => i.Reservation)
                    .ThenInclude(r => r!.Facility)
                .Include(i => i.Reservation)
                    .ThenInclude(r => r!.UnitType)
                .Include(i => i.Customer)
                    .ThenInclude(c => c.User)
                .Where(i => (i.Status == InvoiceStatusConstants.Open || i.Status == InvoiceStatusConstants.Draft || i.Status == InvoiceStatusConstants.PartiallyPaid)
                         && (i.TotalAmount - i.PaidAmount) == payload.TransferAmount)
                .OrderByDescending(i => i.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice != null)
            {
                _logger.LogInformation("Matched open invoice #{InvoiceId} ({InvoiceNo}) via fallback exact transfer amount {Amount:N0} VND.",
                    invoice.Id, invoice.InvoiceNo, payload.TransferAmount);
            }
        }

        if (invoice == null)
        {
            // Check if bank memo / content matches a rental renewal (e.g. RNW-1, DH1)
            RentalRenewal? renewal = null;
            var rnwMatch = Regex.Match(contentText, @"RNW[\s\-_]*(\d+)", RegexOptions.IgnoreCase);
            if (rnwMatch.Success && long.TryParse(rnwMatch.Groups[1].Value, out var parsedRnwId))
            {
                renewal = await _dbContext.RentalRenewals
                    .Include(r => r.Agreement)
                    .FirstOrDefaultAsync(r => r.Id == parsedRnwId, cancellationToken);
            }

            if (renewal == null && prefixMatch.Success && long.TryParse(prefixMatch.Groups[1].Value, out var prefixNum))
            {
                renewal = await _dbContext.RentalRenewals
                    .Include(r => r.Agreement)
                    .FirstOrDefaultAsync(r => r.Id == prefixNum, cancellationToken);
            }

            if (renewal != null)
            {
                var months = (renewal.RequestedEndDate.Year - renewal.OldEndDate.Year) * 12 + renewal.RequestedEndDate.Month - renewal.OldEndDate.Month;
                if (months <= 0) months = 1;
                var renewalAmount = (renewal.NewMonthlyRate ?? renewal.OldMonthlyRate) * months;

                var nowTime = DateTimeOffset.UtcNow;
                renewal.Status = RenewalStatusConstants.Paid;
                renewal.ApprovedEndDate = renewal.RequestedEndDate;
                renewal.ReviewedAt = nowTime;

                if (renewal.Agreement != null)
                {
                    renewal.Agreement.EndDate = renewal.RequestedEndDate;
                    renewal.Agreement.UpdatedAt = nowTime;
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Renewal #{RenewalId} marked as paid via SePay webhook.", renewal.Id);
                return true;
            }

            _logger.LogWarning(PaymentLogMessages.PatternMismatch, prefix, payload.Content);
            return false;
        }

        var transactionRef = !string.IsNullOrWhiteSpace(payload.ReferenceCode)
            ? payload.ReferenceCode.Trim()
            : (payload.Id > 0 ? payload.Id.ToString() : Guid.NewGuid().ToString("N"));

        // 1. Idempotency Check: Prevent Replay Attacks
        var existingPayment = await _dbContext.Payments
            .FirstOrDefaultAsync(p => p.Provider == PaymentConstants.ProviderSePay && p.ProviderTransactionId == transactionRef, cancellationToken);

        if (existingPayment != null && existingPayment.Status == PaymentConstants.StatusSucceeded)
        {
            _logger.LogInformation(PaymentLogMessages.TransactionReplay, transactionRef);
            return true;
        }

        if (invoice.Status == InvoiceStatusConstants.Paid)
        {
            _logger.LogInformation(PaymentLogMessages.InvoiceAlreadyPaid, invoice.Id);
            return true;
        }

        var now = DateTimeOffset.UtcNow;
        var remainingBalance = invoice.TotalAmount - invoice.PaidAmount;

        // 2. Partial Payment Handling: Validate transferred amount against remaining balance
        if (payload.TransferAmount < remainingBalance)
        {
            _logger.LogWarning(PaymentLogMessages.PartialPayment,
                payload.TransferAmount, remainingBalance, invoice.Id);

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
                IdempotencyKey = $"SEPAY-{payload.Id}-{Guid.NewGuid():N}",
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
            await _dbContext.SaveChangesAsync(cancellationToken);

            return true;
        }

        // 3. Full or Final Payment Handling: Reuse existing pending checkout payment or insert new one
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
                IdempotencyKey = $"SEPAY-{payload.Id}-{Guid.NewGuid():N}",
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

        await CompletePaymentAsync(payment, transactionRef, cancellationToken);
        return true;
    }

    public async Task<List<PaymentHistoryDto>> GetMyPaymentHistoryAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        var list = await _dbContext.Payments
            .AsNoTracking()
            .Include(p => p.TargetInvoice)
                .ThenInclude(i => i.Reservation)
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
            Amount = p.Amount,
            Currency = p.Currency,
            Method = p.Method,
            Provider = p.Provider,
            ProviderTransactionId = p.ProviderTransactionId,
            Status = p.Status,
            PaidAt = p.PaidAt,
            CreatedAt = p.CreatedAt,
            FailureReason = p.FailureReason
        }).ToList();

        // Include customer's agreement renewals
        var renewals = await _dbContext.RentalRenewals
            .AsNoTracking()
            .Include(r => r.Agreement)
            .Where(r => r.RequestedBy == customerId || (r.Agreement != null && r.Agreement.CustomerId == customerId))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var r in renewals)
        {
            var months = (r.RequestedEndDate.Year - r.OldEndDate.Year) * 12 + r.RequestedEndDate.Month - r.OldEndDate.Month;
            if (months <= 0) months = 1;
            var amount = (r.NewMonthlyRate ?? r.OldMonthlyRate) * months;

            string status = r.Status;
            if (string.Equals(r.Status, RenewalStatusConstants.PendingPayment, StringComparison.OrdinalIgnoreCase))
            {
                // 15-minute countdown hold check
                if (now - r.CreatedAt > TimeSpan.FromMinutes(15))
                {
                    status = "expired";
                }
                else
                {
                    status = PaymentConstants.StatusPending;
                }
            }
            else if (string.Equals(r.Status, RenewalStatusConstants.Paid, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(r.Status, RenewalStatusConstants.Approved, StringComparison.OrdinalIgnoreCase))
            {
                status = PaymentConstants.StatusSucceeded;
            }
            else if (string.Equals(r.Status, RenewalStatusConstants.Cancelled, StringComparison.OrdinalIgnoreCase))
            {
                status = PaymentConstants.StatusCancelled;
            }
            else if (string.Equals(r.Status, RenewalStatusConstants.Rejected, StringComparison.OrdinalIgnoreCase))
            {
                status = PaymentConstants.StatusFailed;
            }

            history.Add(new PaymentHistoryDto
            {
                PaymentId = 0,
                InvoiceId = 0,
                InvoiceNo = $"RNW-{r.Id}",
                ReservationId = null,
                ReservationCode = !string.IsNullOrWhiteSpace(r.Agreement?.AgreementNo) 
                    ? $"RNW-{r.Agreement.AgreementNo}" 
                    : $"RNW-{r.Id}",
                Amount = amount,
                Currency = PaymentConstants.CurrencyVnd,
                Method = PaymentConstants.MethodBankTransfer,
                Provider = PaymentConstants.ProviderSePay,
                ProviderTransactionId = null,
                Status = status,
                PaidAt = r.ReviewedAt,
                CreatedAt = r.CreatedAt,
                FailureReason = null,
                RenewalId = r.Id,
                AgreementNo = r.Agreement?.AgreementNo
            });
        }

        return history.OrderByDescending(h => h.CreatedAt).ToList();
    }

    private async Task CompletePaymentAsync(
        Payment payment,
        string transactionNo,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // Ensure TargetInvoice and navigations are thoroughly populated
        var invoice = payment.TargetInvoice;
        if (invoice == null && payment.TargetInvoiceId > 0)
        {
            invoice = await _dbContext.Invoices
                .Include(i => i.Reservation)
                    .ThenInclude(r => r!.Facility)
                .Include(i => i.Reservation)
                    .ThenInclude(r => r!.UnitType)
                .Include(i => i.Customer)
                    .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(i => i.Id == payment.TargetInvoiceId, cancellationToken);
            if (invoice != null)
            {
                payment.TargetInvoice = invoice;
            }
        }

        var customer = payment.Customer ?? invoice?.Customer;
        var reservation = invoice?.Reservation;

        // 1. Late Payment Handling: Payment arrived after reservation expired or cancelled
        var isLatePayment = reservation != null &&
            (reservation.Status == ReservationStatusConstants.Expired || reservation.Status == ReservationStatusConstants.Cancelled);

        if (isLatePayment)
        {
            _logger.LogWarning(PaymentLogMessages.LatePaymentState,
                reservation!.ReservationCode, reservation.Status);

            IDbContextTransaction? lateTx = null;
            if (_dbContext.Database.IsSqlServer())
            {
                lateTx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            }

            try
            {
                payment.Status = PaymentConstants.StatusSucceeded;
                payment.PaidAt = now;
                payment.ProviderTransactionId = transactionNo;
                payment.Metadata = $"{{\"note\": \"Late payment received after reservation was {reservation.Status}. Needs manual refund or unit reassignment.\", \"isLatePayment\": true}}";
                payment.UpdatedAt = now;

                await _dbContext.SaveChangesAsync(cancellationToken);

                if (lateTx != null)
                {
                    await lateTx.CommitAsync(cancellationToken);
                }

                // Notify customer via email regarding late payment
                var userEmail = customer?.User?.Email;
                if (!string.IsNullOrWhiteSpace(userEmail))
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

                        await _emailService.SendEmailAsync(userEmail, subject, body, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, PaymentLogMessages.LatePaymentEmailFailed, userEmail);
                    }
                }

                return;
            }
            catch (Exception ex)
            {
                if (lateTx != null)
                {
                    await lateTx.RollbackAsync(cancellationToken);
                }
                _logger.LogError(ex, PaymentLogMessages.LatePaymentFailed, payment.Id, ex.Message);
                return;
            }
            finally
            {
                if (lateTx != null)
                {
                    await lateTx.DisposeAsync();
                }
            }
        }

        IDbContextTransaction? transaction = null;
        if (_dbContext.Database.IsSqlServer())
        {
            transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        try
        {
            payment.Status = PaymentConstants.StatusSucceeded;
            payment.PaidAt = now;
            payment.ProviderTransactionId = transactionNo;
            payment.UpdatedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Safe allocation amount calculation: cannot exceed remaining invoice balance (prevents SQL trigger 51011 throw)
            var remainingBalance = invoice != null ? Math.Max(0, invoice.TotalAmount - invoice.PaidAmount) : payment.Amount;
            var allocatedAmount = Math.Min(payment.Amount, remainingBalance);

            // Create PaymentAllocation if not already present
            var existingAllocation = await _dbContext.PaymentAllocations
                .FirstOrDefaultAsync(pa => pa.PaymentId == payment.Id && pa.InvoiceId == payment.TargetInvoiceId, cancellationToken);

            if (existingAllocation == null && allocatedAmount > 0)
            {
                var allocation = new PaymentAllocation
                {
                    PaymentId = payment.Id,
                    InvoiceId = payment.TargetInvoiceId,
                    AllocatedAmount = allocatedAmount,
                    AllocatedAt = now
                };
                _dbContext.PaymentAllocations.Add(allocation);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // Sync invoice paid amount and status in-memory / DB
            if (invoice != null)
            {
                invoice.PaidAmount += allocatedAmount;
                if (invoice.PaidAmount >= invoice.TotalAmount)
                {
                    invoice.Status = InvoiceStatusConstants.Paid;
                }
                else if (invoice.PaidAmount > 0)
                {
                    invoice.Status = InvoiceStatusConstants.PartiallyPaid;
                }
                invoice.UpdatedAt = now;
            }

            // Cancel any older/duplicate pending payments for this invoice
            var otherPendingPayments = await _dbContext.Payments
                .Where(p => p.TargetInvoiceId == payment.TargetInvoiceId && p.Id != payment.Id && p.Status == PaymentConstants.StatusPending)
                .ToListAsync(cancellationToken);

            foreach (var opp in otherPendingPayments)
            {
                opp.Status = PaymentConstants.StatusCancelled;
                opp.FailureReason = "Superseded by completed transaction.";
                opp.UpdatedAt = now;
            }

            // Update Reservation status to confirmed (BR-RSV-01, BR-RSV-04)
            if (reservation != null && reservation.Status != ReservationStatusConstants.Confirmed)
            {
                reservation.Status = ReservationStatusConstants.Confirmed;
                reservation.ConfirmedAt = now;
                reservation.UpdatedAt = now;

                // Finalize applied promotions
                var redemptions = await _dbContext.PromotionRedemptions
                    .Where(pr => pr.ReservationId == reservation.Id && pr.Status == PromotionRedemptionStatusConstants.Reserved)
                    .ToListAsync(cancellationToken);

                foreach (var red in redemptions)
                {
                    red.Status = PromotionRedemptionStatusConstants.Applied;
                }

                // Create active RentalAgreement and AccessCredential so the customer immediately owns and accesses their unit
                var existingAgreement = await _dbContext.RentalAgreements
                    .FirstOrDefaultAsync(a => a.ReservationId == reservation.Id, cancellationToken);

                if (existingAgreement == null)
                {
                    var agreementNo = $"AGR-HCM-{now:yyyyMMdd}-{reservation.Id:D4}";

                    var policyVersionId = await _dbContext.PolicyVersions
                        .Where(p => p.PolicyType == "rental_terms")
                        .OrderByDescending(p => p.ValidFrom)
                        .Select(p => p.Id)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (policyVersionId <= 0) policyVersionId = 1;

                    var agreement = new RentalAgreement
                    {
                        AgreementNo = agreementNo,
                        ReservationId = reservation.Id,
                        CustomerId = reservation.CustomerId,
                        FacilityId = reservation.FacilityId,
                        PolicyVersionId = policyVersionId,
                        StartDate = reservation.StartDate,
                        EndDate = reservation.EndDate,
                        MonthlyRateSnapshot = reservation.MonthlyRateSnapshot,
                        DepositSnapshot = reservation.DepositSnapshot,
                        DepositBalance = reservation.DepositSnapshot,
                        Status = RentalAgreementStatusConstants.Active,
                        SignedAt = now,
                        CheckedInAt = now,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _dbContext.RentalAgreements.Add(agreement);
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    if (invoice != null)
                    {
                        invoice.AgreementId = agreement.Id;
                    }

                    // Transition reservation unit allocation to rental allocation
                    var resAllocation = await _dbContext.UnitAllocations
                        .FirstOrDefaultAsync(ua => ua.ReservationId == reservation.Id && ua.Status == AllocationStatusConstants.Active, cancellationToken);

                    if (resAllocation != null)
                    {
                        resAllocation.Status = "consumed";
                        resAllocation.EndedAt = now;
                        await _dbContext.SaveChangesAsync(cancellationToken);

                        var rentalAllocation = new UnitAllocation
                        {
                            StorageUnitId = resAllocation.StorageUnitId,
                            AgreementId = agreement.Id,
                            AllocationKind = AllocationKindConstants.Rental,
                            AllocationStartDate = reservation.StartDate,
                            AllocationEndDate = reservation.EndDate,
                            Status = AllocationStatusConstants.Active,
                            CreatedAt = now
                        };
                        _dbContext.UnitAllocations.Add(rentalAllocation);
                        await _dbContext.SaveChangesAsync(cancellationToken);

                        // Update storage unit physical status to occupied
                        var storageUnit = await _dbContext.StorageUnits.FindAsync(new object[] { resAllocation.StorageUnitId }, cancellationToken);
                        if (storageUnit != null && storageUnit.PhysicalStatus == StorageUnitStatusConstants.Reserved)
                        {
                            storageUnit.PhysicalStatus = StorageUnitStatusConstants.Occupied;
                            storageUnit.UpdatedAt = now;
                            await _dbContext.SaveChangesAsync(cancellationToken);
                        }
                    }

                    // Generate secure 6-digit numeric keypad PIN
                    var randomPin = Random.Shared.Next(100000, 999999).ToString();
                    var pinCred = new AccessCredential
                    {
                        AgreementId = agreement.Id,
                        CredentialType = CredentialTypeConstants.Pin,
                        SecretDigest = BCrypt.Net.BCrypt.HashPassword(randomPin),
                        DisplayHint = randomPin,
                        IssuedAt = now,
                        ExpiresAt = now.AddYears(1),
                        Status = CredentialStatusConstants.Active,
                        CreatedAt = now
                    };
                    _dbContext.AccessCredentials.Add(pinCred);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            _logger.LogInformation(PaymentLogMessages.PaymentCompleted,
                payment.Id, payment.TargetInvoiceId, reservation?.ReservationCode ?? "N/A");

            // Send confirmation email
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
        catch (Exception ex)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            _logger.LogError(ex, PaymentLogMessages.PaymentCompletionError, payment.Id, ex.Message);
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
