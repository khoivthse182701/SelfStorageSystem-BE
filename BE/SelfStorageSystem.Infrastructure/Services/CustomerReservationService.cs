using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Customer.Reservations;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public class CustomerReservationService : ICustomerReservationService
{
    private readonly SelfStorageDbContext _dbContext;
    private readonly ReservationSettings _reservationSettings;
    private readonly ILogger<CustomerReservationService> _logger;

    public CustomerReservationService(
        SelfStorageDbContext dbContext,
        IOptions<ReservationSettings> reservationOptions,
        ILogger<CustomerReservationService> logger)
    {
        _dbContext = dbContext;
        _reservationSettings = reservationOptions.Value;
        _logger = logger;
    }

    public async Task<CreateReservationResponse> CreateReservationAsync(
        long customerId,
        CreateReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify customer profile
        var customerExists = await _dbContext.CustomerProfiles
            .AnyAsync(c => c.UserId == customerId, cancellationToken);
        if (!customerExists)
        {
            throw AppException.FromError(ReservationErrors.CustomerNotFound);
        }

        // 2. Validate duration per BR-RSV-02
        if (request.DurationMonths < _reservationSettings.MinDurationMonths ||
            request.DurationMonths > _reservationSettings.MaxDurationMonths)
        {
            throw AppException.FromError(ReservationErrors.InvalidDuration(
                _reservationSettings.MinDurationMonths, _reservationSettings.MaxDurationMonths));
        }

        // Validate start date is not in the past
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.StartDate < today)
        {
            throw AppException.FromError(ReservationErrors.StartDateInPast);
        }

        var endDate = request.StartDate.AddMonths(request.DurationMonths);

        // 3. Verify facility and unit type
        var facility = await _dbContext.Facilities
            .FirstOrDefaultAsync(f => f.Id == request.FacilityId && f.Status == FacilityStatusConstants.Active, cancellationToken);
        if (facility == null)
        {
            throw AppException.FromError(ReservationErrors.FacilityNotFound);
        }

        var unitType = await _dbContext.UnitTypes
            .FirstOrDefaultAsync(u => u.Id == request.UnitTypeId && u.IsActive, cancellationToken);
        if (unitType == null)
        {
            throw AppException.FromError(ReservationErrors.UnitTypeNotFound);
        }

        // 4. Retrieve applicable FacilityRate
        var facilityRate = await _dbContext.FacilityRates
            .Where(fr => fr.FacilityId == request.FacilityId && fr.UnitTypeId == request.UnitTypeId
                         && fr.ValidFrom <= request.StartDate
                         && (fr.ValidTo == null || fr.ValidTo >= request.StartDate))
            .OrderByDescending(fr => fr.ValidFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (facilityRate == null)
        {
            facilityRate = await _dbContext.FacilityRates
                .Where(fr => fr.FacilityId == request.FacilityId && fr.UnitTypeId == request.UnitTypeId)
                .OrderByDescending(fr => fr.ValidFrom)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (facilityRate == null)
        {
            throw AppException.FromError(ReservationErrors.NoPublishedRate);
        }

        var monthlyRate = facilityRate.MonthlyRate;
        // BR-FIN-01: Security deposit defaults to 100% of 1 month's rental rate
        var depositAmount = facilityRate.DepositAmount > 0 ? facilityRate.DepositAmount : monthlyRate;
        var bookingFee = facilityRate.BookingFee;

        // 5. Atomic Transaction & Concurrency Locking Execution
        var holdUntil = DateTimeOffset.UtcNow.AddMinutes(_reservationSettings.HoldDurationMinutes);
        var nowOffset = DateTimeOffset.UtcNow;

        IDbContextTransaction? transaction = null;
        if (_dbContext.Database.IsSqlServer())
        {
            transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        try
        {
            // Evaluate Promotion Voucher if provided (BR-FIN-04)
            decimal discountAmount = 0;
            Promotion? appliedPromo = null;
            if (!string.IsNullOrWhiteSpace(request.PromotionCode))
            {
                var now = DateTimeOffset.UtcNow;
                var promoCode = request.PromotionCode.Trim();

                if (_dbContext.Database.IsSqlServer())
                {
                    appliedPromo = await _dbContext.Promotions
                        .FromSqlInterpolated($"SELECT * FROM core.promotions WITH (UPDLOCK, ROWLOCK) WHERE code = {promoCode} AND is_active = 1")
                        .Include(p => p.PromotionRules)
                        .FirstOrDefaultAsync(cancellationToken);
                }
                else
                {
                    appliedPromo = await _dbContext.Promotions
                        .Include(p => p.PromotionRules)
                        .FirstOrDefaultAsync(p => p.Code == promoCode && p.IsActive, cancellationToken);
                }

                if (appliedPromo == null || appliedPromo.ValidFrom > now || appliedPromo.ValidTo < now)
                {
                    throw AppException.FromError(ReservationErrors.PromotionInvalidOrExpired);
                }

                if (appliedPromo.UsageLimit.HasValue)
                {
                    var usedCount = await _dbContext.PromotionRedemptions
                        .CountAsync(pr => pr.PromotionId == appliedPromo.Id && pr.Status != PromotionRedemptionStatusConstants.Released, cancellationToken);
                    if (usedCount >= appliedPromo.UsageLimit.Value)
                    {
                        throw AppException.FromError(ReservationErrors.PromotionUsageLimitReached);
                    }
                }

                if (appliedPromo.PerCustomerLimit.HasValue)
                {
                    var customerUsedCount = await _dbContext.PromotionRedemptions
                        .CountAsync(pr => pr.PromotionId == appliedPromo.Id && pr.CustomerId == customerId && pr.Status != PromotionRedemptionStatusConstants.Released, cancellationToken);
                    if (customerUsedCount >= appliedPromo.PerCustomerLimit.Value)
                    {
                        throw AppException.FromError(ReservationErrors.PromotionPerCustomerLimitReached(appliedPromo.PerCustomerLimit.Value));
                    }
                }

                if (appliedPromo.DiscountType == PromotionDiscountTypeConstants.Percentage)
                {
                    discountAmount = (monthlyRate * appliedPromo.DiscountValue) / 100m;
                    if (appliedPromo.MaxDiscountAmount.HasValue && discountAmount > appliedPromo.MaxDiscountAmount.Value)
                    {
                        discountAmount = appliedPromo.MaxDiscountAmount.Value;
                    }
                }
                else if (appliedPromo.DiscountType == PromotionDiscountTypeConstants.Fixed)
                {
                    discountAmount = appliedPromo.DiscountValue;
                }
            }

            if (discountAmount > monthlyRate)
            {
                discountAmount = monthlyRate;
            }

            var quotedTotal = (monthlyRate * request.DurationMonths) + depositAmount + bookingFee - discountAmount;
            var firstPaymentTotal = monthlyRate + depositAmount + bookingFee - discountAmount;

            // 6. Concurrency Locking & Specific Storage Unit check
            StorageUnit? selectedUnit = null;
            if (request.StorageUnitId.HasValue)
            {
                if (_dbContext.Database.IsSqlServer())
                {
                    selectedUnit = await _dbContext.StorageUnits
                        .FromSqlInterpolated($"SELECT * FROM core.storage_units WITH (UPDLOCK, ROWLOCK) WHERE id = {request.StorageUnitId.Value}")
                        .FirstOrDefaultAsync(cancellationToken);
                }
                else
                {
                    selectedUnit = await _dbContext.StorageUnits
                        .FirstOrDefaultAsync(u => u.Id == request.StorageUnitId.Value, cancellationToken);
                }

                if (selectedUnit == null)
                {
                    throw AppException.FromError(ReservationErrors.StorageUnitNotFound);
                }

                if (selectedUnit.FacilityId != request.FacilityId || selectedUnit.UnitTypeId != request.UnitTypeId)
                {
                    throw AppException.FromError(ReservationErrors.UnitIncompatible);
                }

                // BR-OPS-02: Must be in available status
                if (!string.Equals(selectedUnit.PhysicalStatus, StorageUnitStatusConstants.Available, StringComparison.OrdinalIgnoreCase))
                {
                    throw AppException.FromError(ReservationErrors.UnitNotAvailable);
                }

                var hasActiveAllocation = await _dbContext.UnitAllocations
                    .AnyAsync(ua => ua.StorageUnitId == selectedUnit.Id
                                 && ua.Status == AllocationStatusConstants.Active
                                 && ua.AllocationStartDate < endDate
                                 && ua.AllocationEndDate > request.StartDate, cancellationToken);
                if (hasActiveAllocation)
                {
                    throw AppException.FromError(ReservationErrors.UnitNotAvailable);
                }
            }
            else
            {
                // Auto-assign validation (BR-RSV-03)
                if (_dbContext.Database.IsSqlServer())
                {
                    selectedUnit = await _dbContext.StorageUnits
                        .FromSqlInterpolated($"SELECT TOP 1 * FROM core.storage_units WITH (UPDLOCK, ROWLOCK, READPAST) WHERE facility_id = {request.FacilityId} AND unit_type_id = {request.UnitTypeId} AND physical_status = {StorageUnitStatusConstants.Available} AND is_listed = 1")
                        .FirstOrDefaultAsync(cancellationToken);

                    if (selectedUnit == null)
                    {
                        throw AppException.FromError(ReservationErrors.NoAvailableUnits);
                    }
                }
                else
                {
                    var availableCount = await _dbContext.StorageUnits
                        .CountAsync(u => u.FacilityId == request.FacilityId
                                      && u.UnitTypeId == request.UnitTypeId
                                      && u.PhysicalStatus == StorageUnitStatusConstants.Available, cancellationToken);

                    if (availableCount <= 0)
                    {
                        throw AppException.FromError(ReservationErrors.NoAvailableUnits);
                    }
                }
            }
            // Generate sequence codes
            var reservationCode = await GenerateReservationCodeAsync(cancellationToken);
            var invoiceNo = await GenerateInvoiceNoAsync(cancellationToken);

            // Create Reservation record
            var reservation = new Reservation
            {
                CustomerId = customerId,
                FacilityId = request.FacilityId,
                UnitTypeId = request.UnitTypeId,
                FacilityRateId = facilityRate.Id,
                ReservationCode = reservationCode,
                StartDate = request.StartDate,
                EndDate = endDate,
                MonthlyRateSnapshot = monthlyRate,
                DepositSnapshot = depositAmount,
                BookingFeeSnapshot = bookingFee,
                DiscountSnapshot = discountAmount,
                QuotedTotal = quotedTotal,
                HoldUntil = holdUntil,
                Status = ReservationStatusConstants.Pending,
                CreatedAt = nowOffset,
                UpdatedAt = nowOffset
            };

            _dbContext.Reservations.Add(reservation);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Create UnitAllocation if specific unit was selected
            if (selectedUnit != null)
            {
                var allocation = new UnitAllocation
                {
                    StorageUnitId = selectedUnit.Id,
                    ReservationId = reservation.Id,
                    AllocationKind = AllocationKindConstants.Reservation,
                    AllocationStartDate = request.StartDate,
                    AllocationEndDate = endDate,
                    Status = AllocationStatusConstants.Active,
                    CreatedAt = nowOffset
                };
                _dbContext.UnitAllocations.Add(allocation);

                selectedUnit.PhysicalStatus = StorageUnitStatusConstants.Reserved;
                selectedUnit.UpdatedAt = nowOffset;
            }

            // Record promotion redemption in reserved state
            if (appliedPromo != null)
            {
                var redemption = new PromotionRedemption
                {
                    PromotionId = appliedPromo.Id,
                    CustomerId = customerId,
                    ReservationId = reservation.Id,
                    DiscountAmount = discountAmount,
                    Status = PromotionRedemptionStatusConstants.Reserved,
                    RedeemedAt = nowOffset
                };
                _dbContext.PromotionRedemptions.Add(redemption);
            }

            // Create initial invoice
            var invoice = new Invoice
            {
                CustomerId = customerId,
                ReservationId = reservation.Id,
                InvoiceNo = invoiceNo,
                IssueDate = today,
                DueDate = today,
                Currency = PaymentConstants.CurrencyVnd,
                SubtotalAmount = monthlyRate + depositAmount + bookingFee,
                DiscountAmount = discountAmount,
                TaxAmount = 0,
                TotalAmount = firstPaymentTotal,
                PaidAmount = 0,
                Status = InvoiceStatusConstants.Open,
                CreatedAt = nowOffset,
                UpdatedAt = nowOffset
            };

            _dbContext.Invoices.Add(invoice);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Add invoice lines
            var lineItems = new List<InvoiceLine>
            {
                new()
                {
                    InvoiceId = invoice.Id,
                    LineType = InvoiceLineTypeConstants.Rent,
                    Description = $"Rental fee for {unitType.Name} ({request.StartDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd})",
                    Quantity = 1,
                    UnitPrice = monthlyRate,
                    LineAmount = monthlyRate,
                    Metadata = "{}"
                },
                new()
                {
                    InvoiceId = invoice.Id,
                    LineType = InvoiceLineTypeConstants.Deposit,
                    Description = "Security deposit (1 month rent per BR-FIN-01)",
                    Quantity = 1,
                    UnitPrice = depositAmount,
                    LineAmount = depositAmount,
                    Metadata = "{}"
                }
            };

            if (bookingFee > 0)
            {
                lineItems.Add(new InvoiceLine
                {
                    InvoiceId = invoice.Id,
                    LineType = InvoiceLineTypeConstants.BookingFee,
                    Description = "Reservation booking fee",
                    Quantity = 1,
                    UnitPrice = bookingFee,
                    LineAmount = bookingFee,
                    Metadata = "{}"
                });
            }

            if (discountAmount > 0 && appliedPromo != null)
            {
                lineItems.Add(new InvoiceLine
                {
                    InvoiceId = invoice.Id,
                    LineType = InvoiceLineTypeConstants.Discount,
                    Description = $"Promotion voucher discount ({appliedPromo.Code})",
                    Quantity = 1,
                    UnitPrice = -discountAmount,
                    LineAmount = -discountAmount,
                    Metadata = "{}"
                });
            }

            _dbContext.InvoiceLines.AddRange(lineItems);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            _logger.LogInformation(
                ReservationLogMessages.HoldCreated,
                reservationCode, customerId, selectedUnit?.UnitCode ?? "Unassigned", holdUntil);

            var expiresInSeconds = (int)Math.Max(0, (holdUntil - nowOffset).TotalSeconds);

            return new CreateReservationResponse
            {
                ReservationId = reservation.Id,
                ReservationCode = reservation.ReservationCode,
                HoldUntil = holdUntil,
                ExpiresInSeconds = expiresInSeconds,
                Status = reservation.Status,
                InvoiceId = invoice.Id,
                InvoiceNo = invoice.InvoiceNo,
                FirstPaymentAmount = firstPaymentTotal,
                MonthlyRate = monthlyRate,
                SecurityDeposit = depositAmount,
                DiscountAmount = discountAmount,
                UnitCode = selectedUnit?.UnitCode,
                FacilityName = facility.Name,
                UnitTypeName = unitType.Name
            };
        }
        catch (Exception ex)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            _logger.LogError(ex, ReservationLogMessages.CreateFailed, customerId, ex.Message);
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

    public async Task<List<ReservationSummaryDto>> GetMyReservationsAsync(
        long customerId,
        string? status = null,
        CancellationToken cancellationToken = default)
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
        {
            query = query.Where(r => r.Status == status.Trim().ToLowerInvariant());
        }

        var list = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;

        return list.Select(r =>
        {
            var invoice = r.Invoices.OrderByDescending(i => i.CreatedAt).FirstOrDefault();
            var isExpired = r.Status == ReservationStatusConstants.Pending && r.HoldUntil < now;
            var displayStatus = r.Status switch
            {
                ReservationStatusConstants.Confirmed => ReservationDisplayStatusConstants.Confirmed,
                ReservationStatusConstants.Expired => ReservationDisplayStatusConstants.Expired,
                ReservationStatusConstants.Cancelled => ReservationDisplayStatusConstants.Cancelled,
                ReservationStatusConstants.Pending => isExpired ? ReservationDisplayStatusConstants.Expired : ReservationDisplayStatusConstants.PendingPayment,
                _ => r.Status
            };

            var expiresInSeconds = (int)Math.Max(0, (r.HoldUntil - now).TotalSeconds);

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
                ExpiresInSeconds = expiresInSeconds,
                Status = r.Status,
                DisplayStatus = displayStatus,
                CreatedAt = r.CreatedAt,
                ConfirmedAt = r.ConfirmedAt,
                CancelledAt = r.CancelledAt,
                CancellationReason = r.CancellationReason,
                InvoiceId = invoice?.Id,
                InvoiceStatus = invoice?.Status,
                FirstPaymentTotal = invoice?.TotalAmount ?? (r.MonthlyRateSnapshot + r.DepositSnapshot + r.BookingFeeSnapshot - r.DiscountSnapshot)
            };
        }).ToList();
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
            .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);

        if (reservation == null)
        {
            throw AppException.FromError(ReservationErrors.NotFound);
        }

        // BOLA / IDOR ownership validation
        if (reservation.CustomerId != customerId)
        {
            throw AppException.FromError(ReservationErrors.Unauthorized);
        }

        var now = DateTimeOffset.UtcNow;
        var isExpired = reservation.Status == ReservationStatusConstants.Pending && reservation.HoldUntil < now;
        var displayStatus = reservation.Status switch
        {
            ReservationStatusConstants.Confirmed => ReservationDisplayStatusConstants.Confirmed,
            ReservationStatusConstants.Expired => ReservationDisplayStatusConstants.Expired,
            ReservationStatusConstants.Cancelled => ReservationDisplayStatusConstants.Cancelled,
            ReservationStatusConstants.Pending => isExpired ? ReservationDisplayStatusConstants.Expired : ReservationDisplayStatusConstants.PendingPayment,
            _ => reservation.Status
        };

        var invoice = reservation.Invoices.OrderByDescending(i => i.CreatedAt).FirstOrDefault();
        var redemption = reservation.PromotionRedemptions.FirstOrDefault();
        var expiresInSeconds = (int)Math.Max(0, (reservation.HoldUntil - now).TotalSeconds);

        string? checkInQrToken = null;
        if (reservation.Status == ReservationStatusConstants.Confirmed)
        {
            checkInQrToken = $"CHECKIN:{reservation.ReservationCode}:{reservation.CustomerId}:{reservation.FacilityId}";
        }

        var detail = new ReservationDetailDto
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
            ExpiresInSeconds = expiresInSeconds,
            Status = reservation.Status,
            DisplayStatus = displayStatus,
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

        return detail;
    }

    public async Task<bool> CancelReservationAsync(
        long customerId,
        long reservationId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .Include(r => r.UnitAllocation)
                .ThenInclude(ua => ua!.StorageUnit)
            .Include(r => r.Invoices)
            .Include(r => r.PromotionRedemptions)
            .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);

        if (reservation == null)
        {
            throw AppException.FromError(ReservationErrors.NotFound);
        }

        if (reservation.CustomerId != customerId)
        {
            throw AppException.FromError(ReservationErrors.UnauthorizedCancel);
        }

        if (reservation.Status != ReservationStatusConstants.Pending &&
            reservation.Status != ReservationStatusConstants.AwaitingDeposit)
        {
            throw AppException.FromError(ReservationErrors.CannotCancelNonPending);
        }

        var now = DateTimeOffset.UtcNow;
        reservation.Status = ReservationStatusConstants.Cancelled;
        reservation.CancelledAt = now;
        reservation.CancellationReason = reason ?? DefaultMessageConstants.DefaultCancellationReason;
        reservation.UpdatedAt = now;

        // Release allocated storage unit back to available per BR-RSV-01
        if (reservation.UnitAllocation != null)
        {
            reservation.UnitAllocation.Status = AllocationStatusConstants.Cancelled;
            reservation.UnitAllocation.EndedAt = now;

            if (reservation.UnitAllocation.StorageUnit != null)
            {
                reservation.UnitAllocation.StorageUnit.PhysicalStatus = StorageUnitStatusConstants.Available;
                reservation.UnitAllocation.StorageUnit.UpdatedAt = now;
            }
        }

        // Cancel associated open invoices
        foreach (var invoice in reservation.Invoices.Where(i => i.Status == InvoiceStatusConstants.Open || i.Status == InvoiceStatusConstants.Draft))
        {
            invoice.Status = InvoiceStatusConstants.Voided;
            invoice.UpdatedAt = now;
        }

        // Release reserved promotion redemptions
        foreach (var red in reservation.PromotionRedemptions.Where(pr => pr.Status == PromotionRedemptionStatusConstants.Reserved))
        {
            red.Status = PromotionRedemptionStatusConstants.Released;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(ReservationLogMessages.CancelledByCustomer, reservation.ReservationCode, customerId);

        return true;
    }

    private async Task<string> GenerateReservationCodeAsync(CancellationToken cancellationToken)
    {
        if (_dbContext.Database.IsSqlServer())
        {
            try
            {
                var conn = _dbContext.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                {
                    await conn.OpenAsync(cancellationToken);
                }

                using var cmd = conn.CreateCommand();
                if (_dbContext.Database.CurrentTransaction != null)
                {
                    cmd.Transaction = _dbContext.Database.CurrentTransaction.GetDbTransaction();
                }
                cmd.CommandText = DbSequenceConstants.ReservationCodeSeqQuery;
                var seqObj = await cmd.ExecuteScalarAsync(cancellationToken);
                var seq = Convert.ToInt64(seqObj);
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
                if (conn.State != System.Data.ConnectionState.Open)
                {
                    await conn.OpenAsync(cancellationToken);
                }

                using var cmd = conn.CreateCommand();
                if (_dbContext.Database.CurrentTransaction != null)
                {
                    cmd.Transaction = _dbContext.Database.CurrentTransaction.GetDbTransaction();
                }
                cmd.CommandText = DbSequenceConstants.InvoiceNoSeqQuery;
                var seqObj = await cmd.ExecuteScalarAsync(cancellationToken);
                var seq = Convert.ToInt64(seqObj);
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
