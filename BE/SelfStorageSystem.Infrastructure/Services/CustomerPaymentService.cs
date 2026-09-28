using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Customer.Payments;
using SelfStorageSystem.Domain.Entities;
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
            throw new KeyNotFoundException("Reservation not found for this customer.");
        }

        // BR-RSV-01: Disallow payment if the hold period has expired
        if (reservation.HoldUntil < now || reservation.Status == "expired" || reservation.Status == "cancelled")
        {
            throw new InvalidOperationException("Reservation hold time has expired as per BR-RSV-01. Please reserve a new unit.");
        }

        if (reservation.Status == "confirmed" || reservation.Status == "converted")
        {
            throw new InvalidOperationException("This reservation has already been confirmed and paid.");
        }

        // 2. Locate open/draft invoice
        var invoice = reservation.Invoices
            .FirstOrDefault(i => i.Status == "open" || i.Status == "draft");

        if (invoice == null)
        {
            throw new InvalidOperationException("No unpaid invoice found for this reservation.");
        }

        var amountToPay = invoice.TotalAmount - invoice.PaidAmount;
        if (amountToPay <= 0)
        {
            throw new InvalidOperationException("Invoice has already been fully paid.");
        }

        const string paymentMethod = "bank_transfer";
        const string provider = "SePay";

        // 3. Create initial Payment record in pending status
        var payment = new Payment
        {
            CustomerId = customerId,
            TargetInvoiceId = invoice.Id,
            Amount = amountToPay,
            Currency = "VND",
            Method = paymentMethod,
            Provider = provider,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            Status = "pending",
            Metadata = "{}",
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var expiresInSeconds = (int)Math.Max(0, (reservation.HoldUntil - now).TotalSeconds);

        // Dynamic invoice prefix configured from SePayConfig
        var prefix = !string.IsNullOrWhiteSpace(_sePayConfig.InvoicePrefix) ? _sePayConfig.InvoicePrefix.Trim() : "DH";
        var transferContent = $"{prefix}{invoice.Id}";

        var baseUrl = !string.IsNullOrWhiteSpace(_vietQrConfig.BaseUrl)
            ? _vietQrConfig.BaseUrl.TrimEnd('/')
            : "https://vietqr.app/img";

        var qrUrl = $"{baseUrl}?bank={Uri.EscapeDataString(_vietQrConfig.BankCode)}&acc={Uri.EscapeDataString(_vietQrConfig.AccountNo)}&template={Uri.EscapeDataString(_vietQrConfig.Template)}&showinfo={(_vietQrConfig.ShowInfo ? "true" : "false")}&holder={Uri.EscapeDataString(_vietQrConfig.AccountName)}&amount={(long)amountToPay}&memo={Uri.EscapeDataString(transferContent)}";

        return new CheckoutResponse
        {
            PaymentId = payment.Id,
            ReservationId = reservation.Id,
            InvoiceId = invoice.Id,
            InvoiceNo = invoice.InvoiceNo,
            Amount = amountToPay,
            Currency = "VND",
            PaymentMethod = paymentMethod,
            ExpiresInSeconds = expiresInSeconds,
            VietQr = new VietQrPaymentInfo
            {
                BankCode = _vietQrConfig.BankCode,
                AccountNo = _vietQrConfig.AccountNo,
                AccountName = _vietQrConfig.AccountName,
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

        // Extract invoice ID dynamically matching the configured prefix
        var prefix = !string.IsNullOrWhiteSpace(_sePayConfig.InvoicePrefix) ? _sePayConfig.InvoicePrefix.Trim() : "DH";
        var regexPattern = $@"{Regex.Escape(prefix)}(\d+)";

        var match = Regex.Match(payload.Content, regexPattern, RegexOptions.IgnoreCase);
        if (!match.Success || !long.TryParse(match.Groups[1].Value, out var invoiceId))
        {
            _logger.LogInformation("SePay Webhook: Content does not match pattern {Prefix}(invoiceId). Content: {Content}", prefix, payload.Content);
            return false;
        }

        var transactionRef = payload.ReferenceCode ?? payload.Id.ToString();

        // 1. Idempotency Check: Prevent Replay Attacks
        var existingPayment = await _dbContext.Payments
            .FirstOrDefaultAsync(p => p.Provider == "SePay" && p.ProviderTransactionId == transactionRef, cancellationToken);

        if (existingPayment != null && existingPayment.Status == "succeeded")
        {
            _logger.LogInformation("SePay Webhook: Transaction {Ref} already processed successfully. Skipping replay.", transactionRef);
            return true;
        }

        var invoice = await _dbContext.Invoices
            .Include(i => i.Reservation)
                .ThenInclude(r => r!.Facility)
            .Include(i => i.Reservation)
                .ThenInclude(r => r!.UnitType)
            .Include(i => i.Customer)
                .ThenInclude(c => c.User)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

        if (invoice == null)
        {
            _logger.LogWarning("SePay Webhook: Invoice ID {InvoiceId} not found.", invoiceId);
            return false;
        }

        if (invoice.Status == "paid")
        {
            _logger.LogInformation("SePay Webhook: Invoice ID {InvoiceId} already paid previously.", invoiceId);
            return true;
        }

        var now = DateTimeOffset.UtcNow;
        var remainingBalance = invoice.TotalAmount - invoice.PaidAmount;

        // 2. Amount Tampering Prevention: Validate transferred amount against remaining balance
        if (payload.TransferAmount < remainingBalance)
        {
            _logger.LogWarning("SePay Webhook: Received amount {Amount} is less than required balance {Remaining} (Invoice {InvoiceId}). Recording partial payment without confirming reservation.",
                payload.TransferAmount, remainingBalance, invoice.Id);

            var partialPayment = new Payment
            {
                CustomerId = invoice.CustomerId,
                TargetInvoiceId = invoice.Id,
                Amount = payload.TransferAmount,
                Currency = "VND",
                Method = "bank_transfer",
                Provider = "SePay",
                ProviderTransactionId = transactionRef,
                IdempotencyKey = $"SEPAY-{payload.Id}-{Guid.NewGuid():N}",
                Status = "succeeded",
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
            await _dbContext.SaveChangesAsync(cancellationToken);

            // DB trigger trg_payment_allocation_guard automatically marks invoice as 'partially_paid'
            return true;
        }

        // Full payment record
        var payment = new Payment
        {
            CustomerId = invoice.CustomerId,
            TargetInvoiceId = invoice.Id,
            Amount = payload.TransferAmount,
            Currency = "VND",
            Method = "bank_transfer",
            Provider = "SePay",
            ProviderTransactionId = transactionRef,
            IdempotencyKey = $"SEPAY-{payload.Id}-{Guid.NewGuid():N}",
            Status = "pending",
            Metadata = "{}",
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync(cancellationToken);

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

        return list.Select(p => new PaymentHistoryDto
        {
            PaymentId = p.Id,
            InvoiceId = p.TargetInvoiceId,
            InvoiceNo = p.TargetInvoice.InvoiceNo,
            ReservationId = p.TargetInvoice.ReservationId,
            ReservationCode = p.TargetInvoice.Reservation?.ReservationCode,
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
    }

    private async Task CompletePaymentAsync(
        Payment payment,
        string transactionNo,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var reservation = payment.TargetInvoice?.Reservation;

        // 3. Late Payment Handling: Payment arrived after reservation expired or cancelled
        var isLatePayment = reservation != null && (reservation.Status == "expired" || reservation.Status == "cancelled");

        if (isLatePayment)
        {
            _logger.LogWarning("Late Payment: Reservation {ReservationCode} is in {Status} state. Recording payment for manual refund or unit change.",
                reservation!.ReservationCode, reservation.Status);

            using var lateTx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                payment.Status = "succeeded";
                payment.PaidAt = now;
                payment.ProviderTransactionId = transactionNo;
                payment.Metadata = $"{{\"note\": \"Late payment received after reservation was {reservation.Status}. Needs manual refund or unit reassignment.\", \"isLatePayment\": true}}";
                payment.UpdatedAt = now;

                await _dbContext.SaveChangesAsync(cancellationToken);
                await lateTx.CommitAsync(cancellationToken);

                // Notify customer via email regarding late payment
                if (payment.Customer?.User?.Email != null)
                {
                    try
                    {
                        var userEmail = payment.Customer.User.Email;
                        var customerName = payment.Customer.FullName ?? "Valued Customer";
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
                        _logger.LogWarning(ex, "Failed to send late payment email notification to {Email}", payment.Customer?.User?.Email);
                    }
                }

                return;
            }
            catch (Exception ex)
            {
                await lateTx.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Error processing late payment for Payment ID {PaymentId}: {Message}", payment.Id, ex.Message);
                return;
            }
        }

        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            payment.Status = "succeeded";
            payment.PaidAt = now;
            payment.ProviderTransactionId = transactionNo;
            payment.UpdatedAt = now;

            // Create PaymentAllocation (Database trigger trg_payment_allocation_guard will mark invoice as 'paid')
            var existingAllocation = await _dbContext.PaymentAllocations
                .FirstOrDefaultAsync(pa => pa.PaymentId == payment.Id && pa.InvoiceId == payment.TargetInvoiceId, cancellationToken);

            if (existingAllocation == null)
            {
                var allocation = new PaymentAllocation
                {
                    PaymentId = payment.Id,
                    InvoiceId = payment.TargetInvoiceId,
                    AllocatedAmount = payment.Amount,
                    AllocatedAt = now
                };
                _dbContext.PaymentAllocations.Add(allocation);
            }

            // Update Reservation status to confirmed
            if (reservation != null && reservation.Status != "confirmed")
            {
                reservation.Status = "confirmed";
                reservation.ConfirmedAt = now;
                reservation.UpdatedAt = now;

                // Finalize applied promotions
                var redemptions = await _dbContext.PromotionRedemptions
                    .Where(pr => pr.ReservationId == reservation.Id && pr.Status == "reserved")
                    .ToListAsync(cancellationToken);

                foreach (var red in redemptions)
                {
                    red.Status = "applied";
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Payment completed successfully for Payment ID {PaymentId}, Invoice ID {InvoiceId}", payment.Id, payment.TargetInvoiceId);

            // Send confirmation email
            if (reservation != null && payment.Customer?.User?.Email != null)
            {
                try
                {
                    var userEmail = payment.Customer.User.Email;
                    var customerName = payment.Customer.FullName ?? "Valued Customer";
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

                    await _emailService.SendEmailAsync(userEmail, subject, body, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send reservation confirmation email to {Email}", payment.Customer?.User?.Email);
                }
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error completing payment for Payment ID {PaymentId}: {Message}", payment.Id, ex.Message);
            throw;
        }
    }
}
