using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Facilities;
using SelfStorageSystem.Contracts.Customer.Reservations;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Common;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public class CustomerReservationService : ICustomerReservationService
{
    private readonly SelfStorageDbContext _dbContext;
    private readonly ReservationSettings _reservationSettings;
    private readonly ILogger<CustomerReservationService> _logger;
    private readonly IPricingEngine _pricingEngine;
    private readonly IQrTokenService? _qrTokenService;
    private readonly IReservationHoldExpiryService _holdExpiryService;

    public CustomerReservationService(
        SelfStorageDbContext dbContext,
        IOptions<ReservationSettings> reservationOptions,
        ILogger<CustomerReservationService> logger,
        IPricingEngine? pricingEngine = null,
        IQrTokenService? qrTokenService = null,
        IReservationHoldExpiryService? holdExpiryService = null)
    {
        _dbContext = dbContext;
        _reservationSettings = reservationOptions.Value;
        _logger = logger;
        _pricingEngine = pricingEngine ?? new PricingEngine(dbContext, reservationOptions);
        _qrTokenService = qrTokenService;
        _holdExpiryService = holdExpiryService ?? new ReservationHoldExpiryService(dbContext, reservationOptions);
    }

    public async Task<CreateReservationResponse> CreateReservationAsync(
        long customerId,
        CreateReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        // Clean up expired holds through the single hold-expiry handler before checking availability.
        await _holdExpiryService.ReleaseExpiredHoldsAsync(respectGracePeriod: false, cancellationToken);

        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? null
            : $"{customerId}:{request.IdempotencyKey.Trim()}";

        if (idempotencyKey != null)
        {
            var existingEvent = await _dbContext.IntegrationEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Source == "reservation" && e.ExternalEventId == idempotencyKey && e.Status == "processed", cancellationToken);

            if (existingEvent != null && TryExtractReservationId(existingEvent.Payload, out var existingRsvId))
            {
                var existing = await LoadCreateResponseFromExistingAsync(existingRsvId, cancellationToken);
                if (existing != null)
                    return existing;
            }
        }

        if (!await _dbContext.CustomerProfiles.AnyAsync(c => c.UserId == customerId, cancellationToken))
            throw AppException.FromError(ReservationErrors.CustomerNotFound);

        var endDate = request.StartDate.AddMonths(request.DurationMonths);
        var facility = await _dbContext.Facilities
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.FacilityId && f.Status == FacilityStatusConstants.Active, cancellationToken)
            ?? throw AppException.FromError(ReservationErrors.FacilityNotFound);

        var unitType = await _dbContext.UnitTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UnitTypeId && u.IsActive, cancellationToken)
            ?? throw AppException.FromError(ReservationErrors.UnitTypeNotFound);

        var facilityRate = await _dbContext.FacilityRates
            .AsNoTracking()
            .Where(fr => fr.FacilityId == request.FacilityId && fr.UnitTypeId == request.UnitTypeId
                      && fr.ValidFrom <= request.StartDate
                      && (fr.ValidTo == null || fr.ValidTo > request.StartDate))
            .OrderByDescending(fr => fr.ValidFrom)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw AppException.FromError(ReservationErrors.NoPublishedRate);

        var nowOffset = DateTimeOffset.UtcNow;
        var tz = TimeZoneHelper.ResolveTimeZone(facility.Timezone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowOffset, tz).DateTime);
        var holdUntil = nowOffset.AddMinutes(Math.Clamp(_reservationSettings.HoldDurationMinutes, 1, 15));

        IDbContextTransaction? transaction = _dbContext.Database.IsSqlServer()
            ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        StorageUnit? selectedUnit = null;
        try
        {
            var quote = await _pricingEngine.CalculateQuoteAsync(
                new PricingQuoteRequest
                {
                    FacilityId = request.FacilityId,
                    UnitTypeId = request.UnitTypeId,
                    StartDate = request.StartDate,
                    DurationMonths = request.DurationMonths,
                    PromotionCode = request.PromotionCode
                },
                customerId,
                lockPromotionRow: true,
                throwConflictOnPromotionLimit: true,
                cancellationToken);

            selectedUnit = await AcquireStorageUnitAsync(request, endDate, cancellationToken);

            var reservationCode = await GenerateReservationCodeAsync(cancellationToken);
            var invoiceNo = await GenerateInvoiceNoAsync(cancellationToken);

            var reservation = new Reservation
            {
                CustomerId = customerId,
                FacilityId = request.FacilityId,
                UnitTypeId = request.UnitTypeId,
                FacilityRateId = facilityRate.Id,
                ReservationCode = reservationCode,
                StartDate = request.StartDate,
                EndDate = endDate,
                MonthlyRateSnapshot = quote.BaseMonthlyRate,
                DepositSnapshot = quote.DepositAmount,
                BookingFeeSnapshot = quote.BookingFee,
                DiscountSnapshot = quote.DiscountAmount,
                QuotedTotal = quote.TotalContractAmount,
                HoldUntil = holdUntil,
                Status = ReservationStatusConstants.Pending,
                CreatedAt = nowOffset,
                UpdatedAt = nowOffset
            };

            _dbContext.Reservations.Add(reservation);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _dbContext.UnitAllocations.Add(new UnitAllocation
            {
                StorageUnitId = selectedUnit.Id,
                ReservationId = reservation.Id,
                AllocationKind = AllocationKindConstants.Reservation,
                AllocationStartDate = request.StartDate,
                AllocationEndDate = endDate,
                Status = AllocationStatusConstants.Active,
                CreatedAt = nowOffset
            });

            if (!string.Equals(selectedUnit.PhysicalStatus, StorageUnitStatusConstants.Reserved, StringComparison.OrdinalIgnoreCase))
            {
                var oldStatus = selectedUnit.PhysicalStatus;
                selectedUnit.PhysicalStatus = StorageUnitStatusConstants.Reserved;
                selectedUnit.UpdatedAt = nowOffset;

                _dbContext.UnitStatusHistories.Add(new UnitStatusHistory
                {
                    StorageUnitId = selectedUnit.Id,
                    OldStatus = oldStatus,
                    NewStatus = StorageUnitStatusConstants.Reserved,
                    Reason = $"Reserved under {reservationCode}",
                    ChangedBy = customerId,
                    ChangedAt = nowOffset
                });
            }

            if (quote.AppliedPromotionId.HasValue)
            {
                _dbContext.PromotionRedemptions.Add(new PromotionRedemption
                {
                    PromotionId = quote.AppliedPromotionId.Value,
                    CustomerId = customerId,
                    ReservationId = reservation.Id,
                    DiscountAmount = quote.DiscountAmount,
                    Status = PromotionRedemptionStatusConstants.Reserved,
                    RedeemedAt = nowOffset
                });
            }

            // Set TaxAmount directly on Invoice header; never insert a 'tax' invoice_line (trg_invoice_lines_refresh_totals adds inv.TaxAmount).
            var invoice = new Invoice
            {
                CustomerId = customerId,
                ReservationId = reservation.Id,
                InvoiceNo = invoiceNo,
                IssueDate = today,
                DueDate = today,
                Currency = PaymentConstants.CurrencyVnd,
                SubtotalAmount = quote.BaseMonthlyRate + quote.DepositAmount + quote.BookingFee,
                DiscountAmount = quote.DiscountAmount,
                TaxAmount = quote.TaxAmount,
                TotalAmount = quote.FirstPaymentAmount,
                PaidAmount = 0,
                Status = InvoiceStatusConstants.Open,
                CreatedAt = nowOffset,
                UpdatedAt = nowOffset
            };

            _dbContext.Invoices.Add(invoice);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var lineItems = new List<InvoiceLine>
            {
                new()
                {
                    InvoiceId = invoice.Id,
                    LineType = InvoiceLineTypeConstants.Rent,
                    Description = $"Rental fee for {unitType.Name} ({request.StartDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd})",
                    Quantity = 1,
                    UnitPrice = quote.BaseMonthlyRate,
                    LineAmount = quote.BaseMonthlyRate,
                    Metadata = "{}"
                },
                new()
                {
                    InvoiceId = invoice.Id,
                    LineType = InvoiceLineTypeConstants.Deposit,
                    Description = "Security deposit at the configured facility rate",
                    Quantity = 1,
                    UnitPrice = quote.DepositAmount,
                    LineAmount = quote.DepositAmount,
                    Metadata = "{}"
                }
            };

            if (quote.BookingFee > 0)
            {
                lineItems.Add(new InvoiceLine
                {
                    InvoiceId = invoice.Id,
                    LineType = InvoiceLineTypeConstants.BookingFee,
                    Description = "Reservation booking fee",
                    Quantity = 1,
                    UnitPrice = quote.BookingFee,
                    LineAmount = quote.BookingFee,
                    Metadata = "{}"
                });
            }

            if (quote.DiscountAmount > 0 && quote.AppliedPromotionCode != null)
            {
                lineItems.Add(new InvoiceLine
                {
                    InvoiceId = invoice.Id,
                    LineType = InvoiceLineTypeConstants.Discount,
                    Description = $"Promotion voucher discount ({quote.AppliedPromotionCode})",
                    Quantity = 1,
                    UnitPrice = -quote.DiscountAmount,
                    LineAmount = -quote.DiscountAmount,
                    Metadata = "{}"
                });
            }

            _dbContext.InvoiceLines.AddRange(lineItems);

            if (idempotencyKey != null)
            {
                _dbContext.IntegrationEvents.Add(new IntegrationEvent
                {
                    Source = "reservation",
                    ExternalEventId = idempotencyKey,
                    EventType = "create_reservation",
                    Payload = JsonSerializer.Serialize(new { reservationId = reservation.Id }),
                    Status = "processed",
                    ReceivedAt = nowOffset,
                    ProcessedAt = nowOffset
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (transaction != null)
                await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(ReservationLogMessages.HoldCreated, reservationCode, customerId, selectedUnit.UnitCode, holdUntil);

            return new CreateReservationResponse
            {
                ReservationId = reservation.Id,
                ReservationCode = reservation.ReservationCode,
                HoldUntil = holdUntil,
                ExpiresInSeconds = (int)Math.Max(0, (holdUntil - nowOffset).TotalSeconds),
                Status = reservation.Status,
                InvoiceId = invoice.Id,
                InvoiceNo = invoice.InvoiceNo,
                FirstPaymentAmount = quote.FirstPaymentAmount,
                MonthlyRate = quote.BaseMonthlyRate,
                SecurityDeposit = quote.DepositAmount,
                BookingFee = quote.BookingFee,
                DiscountAmount = quote.DiscountAmount,
                TaxAmount = quote.TaxAmount,
                QuotedTotal = quote.TotalContractAmount,
                UnitId = selectedUnit.Id,
                UnitCode = selectedUnit.UnitCode,
                FacilityName = facility.Name,
                UnitTypeName = unitType.Name
            };
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            if (idempotencyKey != null)
            {
                _dbContext.ChangeTracker.Clear();
                var existing = await TryLoadByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
                if (existing != null) return existing;
            }
            _logger.LogWarning(ex, "Concurrency collision detected while reserving unit {UnitId}", selectedUnit?.Id);
            throw AppException.FromError(ReservationErrors.UnitNotAvailable);
        }
        catch (AppConflictException) when (idempotencyKey != null)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            var existing = await TryLoadByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
            if (existing != null) return existing;
            throw;
        }
        catch (DbUpdateException ex) when (idempotencyKey != null)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            var existing = await TryLoadByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
            if (existing != null) return existing;

            _logger.LogError(ex, ReservationLogMessages.CreateFailed, customerId, ex.Message);
            throw;
        }
        catch (Exception ex) when (ex is not IHasAppError)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, ReservationLogMessages.CreateFailed, customerId, ex.Message);
            throw;
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    public async Task<List<ReservationSummaryDto>> GetMyReservationsAsync(
        long customerId,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildMyReservationsQuery(customerId, status);
        var list = await query.OrderByDescending(r => r.CreatedAt).ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        return list.Select(r => MapReservationSummary(r, now)).ToList();
    }

    public async Task<PagedResult<ReservationSummaryDto>> GetMyReservationsPagedAsync(
        long customerId,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = BuildMyReservationsQuery(customerId, status);
        var totalCount = await query.CountAsync(cancellationToken);
        var list = await query
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        return new PagedResult<ReservationSummaryDto>(list.Select(r => MapReservationSummary(r, now)).ToList(), totalCount, page, pageSize);
    }

    public async Task<ReservationDetailDto> GetReservationDetailAsync(
        long customerId,
        long reservationId,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .AsNoTracking()
            .AsSplitQuery()
            .Include(r => r.Facility)
            .Include(r => r.UnitType)
            .Include(r => r.UnitAllocation)
                .ThenInclude(ua => ua!.StorageUnit)
            .Include(r => r.PromotionRedemptions)
                .ThenInclude(pr => pr.Promotion)
            .Include(r => r.Invoices)
                .ThenInclude(i => i.InvoiceLines)
            .Include(r => r.RentalAgreement)
            .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken)
            ?? throw AppException.FromError(ReservationErrors.NotFound);

        if (reservation.CustomerId != customerId)
            throw AppException.FromError(ReservationErrors.Unauthorized);

        var now = DateTimeOffset.UtcNow;
        var invoice = reservation.Invoices.OrderByDescending(i => i.CreatedAt).FirstOrDefault();
        var redemption = reservation.PromotionRedemptions.FirstOrDefault();

        string? checkInQrToken = null;
        if (reservation.Status == ReservationStatusConstants.Confirmed)
        {
            checkInQrToken = _qrTokenService?.IssueToken(
                "checkin",
                reservation.Id,
                reservation.RentalAgreement?.Id,
                reservation.CustomerId,
                reservation.FacilityId,
                TimeSpan.FromDays(90));
        }

        return new ReservationDetailDto
        {
            Id = reservation.Id,
            ReservationCode = reservation.ReservationCode,
            FacilityId = reservation.FacilityId,
            FacilityName = reservation.Facility.Name,
            FacilityAddress = reservation.Facility.AddressLine,
            UnitTypeId = reservation.UnitTypeId,
            UnitTypeName = reservation.UnitType.Name,
            UnitCode = reservation.UnitAllocation?.StorageUnit?.UnitCode,
            StartDate = reservation.StartDate,
            EndDate = reservation.EndDate,
            MonthlyRateSnapshot = reservation.MonthlyRateSnapshot,
            DepositSnapshot = reservation.DepositSnapshot,
            BookingFeeSnapshot = reservation.BookingFeeSnapshot,
            DiscountSnapshot = reservation.DiscountSnapshot,
            QuotedTotal = reservation.QuotedTotal,
            HoldUntil = reservation.HoldUntil,
            ExpiresInSeconds = (int)Math.Max(0, (reservation.HoldUntil - now).TotalSeconds),
            Status = reservation.Status,
            DisplayStatus = ResolveDisplayStatus(reservation, now),
            CreatedAt = reservation.CreatedAt,
            ConfirmedAt = reservation.ConfirmedAt,
            CancelledAt = reservation.CancelledAt,
            CancellationReason = reservation.CancellationReason,
            InvoiceId = invoice?.Id,
            InvoiceStatus = invoice?.Status,
            FirstPaymentTotal = invoice?.TotalAmount ?? (reservation.MonthlyRateSnapshot + reservation.DepositSnapshot + reservation.BookingFeeSnapshot - reservation.DiscountSnapshot),
            PromotionCode = redemption?.Promotion.Code,
            CheckInQrToken = checkInQrToken,
            CheckInInstructions = DefaultMessageConstants.CheckInInstructions,
            Invoices = reservation.Invoices.Select(i => new InvoiceSummaryDto
            {
                Id = i.Id,
                InvoiceNo = i.InvoiceNo,
                IssueDate = i.IssueDate,
                DueDate = i.DueDate,
                SubtotalAmount = i.SubtotalAmount,
                DiscountAmount = i.DiscountAmount,
                TaxAmount = i.TaxAmount,
                TotalAmount = i.TotalAmount,
                PaidAmount = i.PaidAmount,
                Status = i.Status,
                Lines = i.InvoiceLines.Select(l => new InvoiceLineItemDto
                {
                    Id = l.Id,
                    LineType = l.LineType,
                    Description = l.Description,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    LineAmount = l.LineAmount ?? (l.Quantity * l.UnitPrice)
                }).ToList()
            }).ToList()
        };
    }

    public async Task<bool> CancelReservationAsync(
        long customerId,
        long reservationId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        await CancelReservationWithResultAsync(customerId, reservationId, reason, cancellationToken);
        return true;
    }

    public async Task<CancelReservationResponse> CancelReservationWithResultAsync(
        long customerId,
        long reservationId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .Include(r => r.UnitAllocation)
                .ThenInclude(ua => ua!.StorageUnit)
            .Include(r => r.Invoices)
                .ThenInclude(i => i.Payments)
            .Include(r => r.PromotionRedemptions)
            .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken)
            ?? throw AppException.FromError(ReservationErrors.NotFound);

        if (reservation.CustomerId != customerId)
            throw AppException.FromError(ReservationErrors.UnauthorizedCancel);

        if (reservation.Status != ReservationStatusConstants.Pending &&
            reservation.Status != ReservationStatusConstants.AwaitingDeposit &&
            reservation.Status != ReservationStatusConstants.Confirmed)
        {
            throw AppException.FromError(ReservationErrors.CannotCancelNonPending);
        }

        if (await _dbContext.RentalAgreements.AnyAsync(a =>
                a.ReservationId == reservation.Id &&
                (a.Status != RentalAgreementStatusConstants.Draft || a.CheckedInAt != null), cancellationToken))
        {
            throw AppException.FromError(ReservationErrors.CannotCancelNonPending);
        }

        var now = DateTimeOffset.UtcNow;
        reservation.Status = ReservationStatusConstants.Cancelled;
        reservation.CancelledAt = now;
        reservation.CancellationReason = reason ?? DefaultMessageConstants.DefaultCancellationReason;
        reservation.UpdatedAt = now;

        if (reservation.UnitAllocation != null)
        {
            reservation.UnitAllocation.Status = AllocationStatusConstants.Cancelled;
            reservation.UnitAllocation.EndedAt = now;

            if (reservation.UnitAllocation.StorageUnit != null)
            {
                var unit = reservation.UnitAllocation.StorageUnit;
                var hasOtherActiveAllocations = await _dbContext.UnitAllocations.AnyAsync(ua =>
                    ua.StorageUnitId == unit.Id &&
                    ua.Id != reservation.UnitAllocation.Id &&
                    ua.Status == AllocationStatusConstants.Active, cancellationToken);

                if (!hasOtherActiveAllocations && unit.PhysicalStatus == StorageUnitStatusConstants.Reserved)
                {
                    unit.PhysicalStatus = StorageUnitStatusConstants.Available;
                    unit.UpdatedAt = now;
                    _dbContext.UnitStatusHistories.Add(new UnitStatusHistory
                    {
                        StorageUnitId = unit.Id,
                        OldStatus = StorageUnitStatusConstants.Reserved,
                        NewStatus = StorageUnitStatusConstants.Available,
                        Reason = $"Reservation {reservation.ReservationCode} cancelled by customer.",
                        ChangedBy = customerId,
                        ChangedAt = now
                    });
                }
            }
        }

        foreach (var invoice in reservation.Invoices)
        {
            if (invoice.Status is InvoiceStatusConstants.Open or InvoiceStatusConstants.Draft
                or InvoiceStatusConstants.Overdue or InvoiceStatusConstants.PartiallyPaid)
            {
                invoice.Status = InvoiceStatusConstants.Voided;
                invoice.VoidedAt = now;
                invoice.UpdatedAt = now;
            }

            foreach (var payment in invoice.Payments.Where(p => p.Status == PaymentConstants.StatusPending))
            {
                payment.Status = PaymentConstants.StatusCancelled;
                payment.FailureReason = "Reservation cancelled by customer.";
                payment.UpdatedAt = now;
            }
        }

        foreach (var red in reservation.PromotionRedemptions.Where(pr => pr.Status == PromotionRedemptionStatusConstants.Reserved))
        {
            red.Status = PromotionRedemptionStatusConstants.Released;
        }

        var agreements = await _dbContext.RentalAgreements
            .Where(a => a.ReservationId == reservation.Id)
            .ToListAsync(cancellationToken);

        foreach (var ag in agreements)
        {
            ag.Status = RentalAgreementStatusConstants.Cancelled;
            ag.UpdatedAt = now;
        }

        var settledPayments = reservation.Invoices
            .SelectMany(i => i.Payments)
            .Where(p => p.Status is PaymentConstants.StatusSucceeded or PaymentConstants.StatusPartiallyRefunded)
            .ToList();

        var settledPaymentIds = settledPayments.Select(p => p.Id).ToList();
        var existingRefundsByPayment = settledPaymentIds.Count == 0
            ? new Dictionary<long, decimal>()
            : await _dbContext.Refunds
                .AsNoTracking()
                .Where(r => settledPaymentIds.Contains(r.PaymentId) &&
                            r.Status != "failed" &&
                            r.Status != RefundStatusConstants.Rejected)
                .GroupBy(r => r.PaymentId)
                .Select(g => new { PaymentId = g.Key, Total = g.Sum(x => x.Amount) })
                .ToDictionaryAsync(x => x.PaymentId, x => x.Total, cancellationToken);

        foreach (var payment in settledPayments)
        {
            var alreadyRefunded = existingRefundsByPayment.GetValueOrDefault(payment.Id, 0m);
            var remainingRefundable = payment.Amount - alreadyRefunded;
            if (remainingRefundable <= 0m)
                continue;

            _dbContext.Refunds.Add(new Refund
            {
                PaymentId = payment.Id,
                AgreementId = agreements.FirstOrDefault()?.Id,
                Amount = remainingRefundable,
                Currency = payment.Currency ?? PaymentConstants.CurrencyVnd,
                Provider = payment.Provider ?? "Internal",
                IdempotencyKey = $"REFUND-CANCEL-RSV-{reservation.Id}-{payment.Id}",
                Reason = "Refund due to customer cancelling confirmed reservation before check-in",
                Status = RefundStatusConstants.Pending,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(ReservationLogMessages.CancelledByCustomer, reservation.ReservationCode, customerId);

        return new CancelReservationResponse(reservation.Id, reservation.Status, now);
    }

    private async Task<StorageUnit> AcquireStorageUnitAsync(CreateReservationRequest request, DateOnly endDate, CancellationToken ct)
    {
        if (request.StorageUnitId.HasValue)
        {
            var selectedUnit = _dbContext.Database.IsSqlServer()
                ? await _dbContext.StorageUnits
                    .FromSqlInterpolated($"SELECT * FROM [core].[storage_units] WITH (UPDLOCK, ROWLOCK) WHERE id = {request.StorageUnitId.Value}")
                    .FirstOrDefaultAsync(ct)
                : await _dbContext.StorageUnits.FirstOrDefaultAsync(u => u.Id == request.StorageUnitId.Value, ct);

            if (selectedUnit == null)
                throw AppException.FromError(ReservationErrors.StorageUnitNotFound);

            if (selectedUnit.FacilityId != request.FacilityId || selectedUnit.UnitTypeId != request.UnitTypeId)
                throw AppException.FromError(ReservationErrors.UnitIncompatible);

            if (!selectedUnit.IsListed)
                throw AppException.FromError(ReservationErrors.UnitNotAvailable);

            var isAvailableStatus = string.Equals(selectedUnit.PhysicalStatus, StorageUnitStatusConstants.Available, StringComparison.OrdinalIgnoreCase);
            var isReservedStatus = string.Equals(selectedUnit.PhysicalStatus, StorageUnitStatusConstants.Reserved, StringComparison.OrdinalIgnoreCase);

            if (!isAvailableStatus && !isReservedStatus)
                throw AppException.FromError(ReservationErrors.UnitNotAvailable);

            if (isReservedStatus)
            {
                var hasAnyActiveAllocation = await _dbContext.UnitAllocations.AnyAsync(ua =>
                    ua.StorageUnitId == selectedUnit.Id &&
                    ua.Status == AllocationStatusConstants.Active, ct);
                if (!hasAnyActiveAllocation)
                    throw AppException.FromError(ReservationErrors.UnitNotAvailable);
            }

            var hasActiveAllocation = await _dbContext.UnitAllocations.AnyAsync(ua =>
                ua.StorageUnitId == selectedUnit.Id &&
                ua.Status == AllocationStatusConstants.Active &&
                ua.AllocationStartDate < endDate &&
                ua.AllocationEndDate > request.StartDate, ct);

            if (hasActiveAllocation)
                throw AppException.FromError(ReservationErrors.UnitNotAvailable);

            return selectedUnit;
        }

        if (_dbContext.Database.IsSqlServer())
        {
            var selected = await _dbContext.StorageUnits
                .FromSqlInterpolated($"""
                    SELECT TOP 1 su.*
                    FROM [core].[storage_units] su WITH (UPDLOCK, ROWLOCK, READPAST)
                    WHERE su.facility_id = {request.FacilityId}
                      AND su.unit_type_id = {request.UnitTypeId}
                      AND su.is_listed = 1
                      AND (
                          su.physical_status = {StorageUnitStatusConstants.Available}
                          OR (
                              su.physical_status = {StorageUnitStatusConstants.Reserved}
                              AND EXISTS (
                                  SELECT 1 FROM [core].[unit_allocations] any_ua
                                  WHERE any_ua.storage_unit_id = su.id
                                    AND any_ua.status = {AllocationStatusConstants.Active}
                              )
                          )
                      )
                      AND NOT EXISTS (
                          SELECT 1 FROM [core].[unit_allocations] ua
                          WHERE ua.storage_unit_id = su.id
                            AND ua.status = {AllocationStatusConstants.Active}
                            AND ua.allocation_start_date < {endDate}
                            AND ua.allocation_end_date > {request.StartDate}
                      )
                    ORDER BY CASE WHEN su.physical_status = {StorageUnitStatusConstants.Available} THEN 0 ELSE 1 END, su.id
                    """)
                .FirstOrDefaultAsync(ct);

            return selected ?? throw AppException.FromError(ReservationErrors.NoAvailableUnits);
        }

        var unit = await _dbContext.StorageUnits
            .Where(u => u.FacilityId == request.FacilityId
                     && u.UnitTypeId == request.UnitTypeId
                     && u.IsListed
                     && (u.PhysicalStatus == StorageUnitStatusConstants.Available ||
                         (u.PhysicalStatus == StorageUnitStatusConstants.Reserved &&
                          u.UnitAllocations.Any(ua => ua.Status == AllocationStatusConstants.Active)))
                     && !u.UnitAllocations.Any(ua => ua.Status == AllocationStatusConstants.Active && ua.AllocationStartDate < endDate && ua.AllocationEndDate > request.StartDate))
            .OrderBy(u => u.PhysicalStatus == StorageUnitStatusConstants.Available ? 0 : 1)
            .ThenBy(u => u.Id)
            .FirstOrDefaultAsync(ct);

        return unit ?? throw AppException.FromError(ReservationErrors.NoAvailableUnits);
    }

    private IQueryable<Reservation> BuildMyReservationsQuery(long customerId, string? status)
    {
        var query = _dbContext.Reservations
            .AsNoTracking()
            .Include(r => r.Facility)
            .Include(r => r.UnitType)
            .Include(r => r.UnitAllocation)
                .ThenInclude(ua => ua!.StorageUnit)
            .Include(r => r.Invoices)
            .Where(r => r.CustomerId == customerId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status.Trim().ToLowerInvariant());

        return query;
    }

    private static ReservationSummaryDto MapReservationSummary(Reservation r, DateTimeOffset now)
    {
        var invoice = r.Invoices.OrderByDescending(i => i.CreatedAt).FirstOrDefault();
        return new ReservationSummaryDto
        {
            Id = r.Id,
            ReservationCode = r.ReservationCode,
            FacilityId = r.FacilityId,
            FacilityName = r.Facility.Name,
            FacilityAddress = r.Facility.AddressLine,
            UnitTypeId = r.UnitTypeId,
            UnitTypeName = r.UnitType.Name,
            UnitCode = r.UnitAllocation?.StorageUnit?.UnitCode,
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            MonthlyRateSnapshot = r.MonthlyRateSnapshot,
            DepositSnapshot = r.DepositSnapshot,
            QuotedTotal = r.QuotedTotal,
            HoldUntil = r.HoldUntil,
            ExpiresInSeconds = (int)Math.Max(0, (r.HoldUntil - now).TotalSeconds),
            Status = r.Status,
            DisplayStatus = ResolveDisplayStatus(r, now),
            CreatedAt = r.CreatedAt,
            ConfirmedAt = r.ConfirmedAt,
            CancelledAt = r.CancelledAt,
            CancellationReason = r.CancellationReason,
            InvoiceId = invoice?.Id,
            InvoiceStatus = invoice?.Status,
            FirstPaymentTotal = invoice?.TotalAmount ?? (r.MonthlyRateSnapshot + r.DepositSnapshot + r.BookingFeeSnapshot - r.DiscountSnapshot)
        };
    }

    private static string ResolveDisplayStatus(Reservation r, DateTimeOffset now) =>
        r.Status switch
        {
            ReservationStatusConstants.Confirmed => ReservationDisplayStatusConstants.Confirmed,
            ReservationStatusConstants.Expired => ReservationDisplayStatusConstants.Expired,
            ReservationStatusConstants.Cancelled => ReservationDisplayStatusConstants.Cancelled,
            ReservationStatusConstants.Pending => r.HoldUntil < now ? ReservationDisplayStatusConstants.Expired : ReservationDisplayStatusConstants.PendingPayment,
            _ => r.Status
        };

    private static bool TryExtractReservationId(string payload, out long reservationId)
    {
        reservationId = 0;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.TryGetProperty("reservationId", out var prop) && prop.TryGetInt64(out reservationId);
        }
        catch
        {
            return false;
        }
    }

    private async Task<CreateReservationResponse?> TryLoadByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
    {
        var existingEvent = await _dbContext.IntegrationEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Source == "reservation" && e.ExternalEventId == idempotencyKey && e.Status == "processed", ct);

        if (existingEvent != null && TryExtractReservationId(existingEvent.Payload, out var existingRsvId))
            return await LoadCreateResponseFromExistingAsync(existingRsvId, ct);

        return null;
    }

    private async Task<CreateReservationResponse?> LoadCreateResponseFromExistingAsync(long reservationId, CancellationToken ct)
    {
        var r = await _dbContext.Reservations
            .AsNoTracking()
            .Include(x => x.Facility)
            .Include(x => x.UnitType)
            .Include(x => x.UnitAllocation).ThenInclude(ua => ua!.StorageUnit)
            .Include(x => x.Invoices)
            .FirstOrDefaultAsync(x => x.Id == reservationId, ct);

        if (r == null) return null;
        var invoice = r.Invoices.OrderByDescending(i => i.CreatedAt).FirstOrDefault();
        var now = DateTimeOffset.UtcNow;

        return new CreateReservationResponse
        {
            ReservationId = r.Id,
            ReservationCode = r.ReservationCode,
            HoldUntil = r.HoldUntil,
            ExpiresInSeconds = (int)Math.Max(0, (r.HoldUntil - now).TotalSeconds),
            Status = r.Status,
            InvoiceId = invoice?.Id ?? 0,
            InvoiceNo = invoice?.InvoiceNo ?? string.Empty,
            FirstPaymentAmount = invoice?.TotalAmount ?? 0m,
            MonthlyRate = r.MonthlyRateSnapshot,
            SecurityDeposit = r.DepositSnapshot,
            BookingFee = r.BookingFeeSnapshot,
            DiscountAmount = r.DiscountSnapshot,
            TaxAmount = invoice?.TaxAmount ?? 0m,
            QuotedTotal = r.QuotedTotal,
            UnitId = r.UnitAllocation?.StorageUnitId,
            UnitCode = r.UnitAllocation?.StorageUnit?.UnitCode,
            FacilityName = r.Facility.Name,
            UnitTypeName = r.UnitType.Name
        };
    }

    private async Task<string> GenerateReservationCodeAsync(CancellationToken cancellationToken)
    {
        if (_dbContext.Database.IsSqlServer())
        {
            try
            {
                var conn = _dbContext.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync(cancellationToken);

                using var cmd = conn.CreateCommand();
                if (_dbContext.Database.CurrentTransaction != null)
                    cmd.Transaction = _dbContext.Database.CurrentTransaction.GetDbTransaction();

                cmd.CommandText = DbSequenceConstants.ReservationCodeSeqQuery;
                var seq = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
                return $"RSV-{DateTime.UtcNow:yyyyMMdd}-{seq:D5}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, ReservationLogMessages.FallbackReservationSeq);
            }
        }

        return $"RSV-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(10000, 99999)}";
    }

    private async Task<string> GenerateInvoiceNoAsync(CancellationToken cancellationToken)
    {
        if (_dbContext.Database.IsSqlServer())
        {
            try
            {
                var conn = _dbContext.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync(cancellationToken);

                using var cmd = conn.CreateCommand();
                if (_dbContext.Database.CurrentTransaction != null)
                    cmd.Transaction = _dbContext.Database.CurrentTransaction.GetDbTransaction();

                cmd.CommandText = DbSequenceConstants.InvoiceNoSeqQuery;
                var seq = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
                return $"INV-{DateTime.UtcNow:yyyyMMdd}-{seq:D5}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, ReservationLogMessages.FallbackInvoiceSeq);
            }
        }

        return $"INV-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(10000, 99999)}";
    }
}
