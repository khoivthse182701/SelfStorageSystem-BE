using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Staff;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;

namespace SelfStorageSystem.Infrastructure.Services;

public partial class FacilityStaffService
{
    public async Task<PagedResult<StaffAccessEventDto>> GetAccessEventsAsync(
        long employeeUserId,
        long? facilityId = null,
        long? agreementId = null,
        long? unitId = null,
        string? result = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var targetFacilityId = await EnsureStrictFacilityAccessAsync(employeeUserId, facilityId, cancellationToken);

        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.AccessEvents
            .AsNoTracking()
            .Include(ae => ae.AccessPoint)
            .Include(ae => ae.Credential)
                .ThenInclude(c => c!.AuthorizedMember)
            .Include(ae => ae.Credential)
                .ThenInclude(c => c!.Agreement)
                    .ThenInclude(a => a.Customer)
            .Include(ae => ae.Credential)
                .ThenInclude(c => c!.Agreement)
                    .ThenInclude(a => a.UnitAllocations)
                        .ThenInclude(ua => ua.StorageUnit)
            .Where(ae => ae.AccessPoint.FacilityId == targetFacilityId);

        if (agreementId.HasValue)
            query = query.Where(ae => ae.Credential != null && ae.Credential.AgreementId == agreementId.Value);

        if (unitId.HasValue)
            query = query.Where(ae => ae.Credential != null
                && ae.Credential.Agreement.UnitAllocations.Any(ua => ua.StorageUnitId == unitId.Value));

        if (!string.IsNullOrWhiteSpace(result))
        {
            var r = result.Trim().ToLowerInvariant();
            query = query.Where(ae => ae.Result.ToLower() == r);
        }

        if (from.HasValue)
            query = query.Where(ae => ae.OccurredAt >= from.Value);

        if (to.HasValue)
            query = query.Where(ae => ae.OccurredAt <= to.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(ae => ae.OccurredAt)
            .ThenByDescending(ae => ae.Id)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(ae =>
        {
            var agr = ae.Credential?.Agreement;
            var alloc = agr?.UnitAllocations.FirstOrDefault(ua => ua.Status == AllocationStatusConstants.Active)
                        ?? agr?.UnitAllocations.OrderByDescending(ua => ua.Id).FirstOrDefault();
            var unit = alloc?.StorageUnit;
            var actorName = ae.Credential?.AuthorizedMember?.FullName
                            ?? agr?.Customer?.FullName
                            ?? "Guest / Counter";
            return new StaffAccessEventDto(
                Id: ae.Id,
                FacilityId: targetFacilityId,
                AgreementId: agr?.Id,
                AgreementNo: agr?.AgreementNo,
                StorageUnitId: unit?.Id,
                UnitCode: unit?.UnitCode,
                AccessPointId: ae.AccessPointId,
                AccessPointName: ae.AccessPoint.Name,
                AccessPointType: ae.AccessPoint.AccessPointType,
                CredentialId: ae.CredentialId,
                ActorName: actorName,
                Result: ae.Result,
                Reason: ae.Reason,
                OccurredAt: ae.OccurredAt
            );
        }).ToList();

        return new PagedResult<StaffAccessEventDto>(items, totalCount, safePage, safeSize);
    }

    public async Task<StaffAccessEventDto> RecordAccessEventAsync(
        long employeeUserId,
        CreateStaffAccessEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedResult = (request.Result ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedResult is not ("granted" or "denied" or "error"))
            throw AppException.FromError(StaffErrors.InvalidAccessEventResult);

        var accessPoint = await _dbContext.AccessPoints
            .FirstOrDefaultAsync(ap => ap.Id == request.AccessPointId, cancellationToken);

        if (accessPoint == null)
            throw AppException.FromError(StaffErrors.AccessPointNotFound);

        var targetFacilityId = await EnsureStrictFacilityAccessAsync(employeeUserId, accessPoint.FacilityId, cancellationToken);

        AccessCredential? credential = null;
        RentalAgreement? agreement = null;

        if (request.CredentialId.HasValue)
        {
            credential = await _dbContext.AccessCredentials
                .Include(c => c.AuthorizedMember)
                .Include(c => c.Agreement)
                    .ThenInclude(a => a.Customer)
                .Include(c => c.Agreement)
                    .ThenInclude(a => a.UnitAllocations)
                        .ThenInclude(ua => ua.StorageUnit)
                .FirstOrDefaultAsync(c => c.Id == request.CredentialId.Value, cancellationToken);

            if (credential == null || credential.Agreement.FacilityId != targetFacilityId)
                throw AppException.FromError(StaffErrors.AgreementNotFound);

            agreement = credential.Agreement;
        }
        else if (request.AgreementId.HasValue)
        {
            agreement = await _dbContext.RentalAgreements
                .Include(a => a.Customer)
                .Include(a => a.UnitAllocations)
                    .ThenInclude(ua => ua.StorageUnit)
                .Include(a => a.AccessCredentials)
                .FirstOrDefaultAsync(a => a.Id == request.AgreementId.Value && a.FacilityId == targetFacilityId, cancellationToken);

            if (agreement == null)
                throw AppException.FromError(StaffErrors.AgreementNotFound);

            credential = agreement.AccessCredentials
                .OrderByDescending(c => c.Status == CredentialStatusConstants.Active ? 1 : 0)
                .ThenByDescending(c => c.Id)
                .FirstOrDefault();
        }

        var now = DateTimeOffset.UtcNow;
        var ev = new AccessEvent
        {
            AccessPointId = accessPoint.Id,
            CredentialId = credential?.Id,
            OccurredAt = now,
            Result = normalizedResult,
            Reason = Truncate(request.Reason, 255) ?? $"Recorded by staff #{employeeUserId}",
            Metadata = JsonSerializer.Serialize(new { recordedByEmployeeId = employeeUserId, source = "staff_counter" })
        };

        _dbContext.AccessEvents.Add(ev);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var alloc = agreement?.UnitAllocations.FirstOrDefault(ua => ua.Status == AllocationStatusConstants.Active)
                    ?? agreement?.UnitAllocations.OrderByDescending(ua => ua.Id).FirstOrDefault();
        var unit = alloc?.StorageUnit;

        return new StaffAccessEventDto(
            Id: ev.Id,
            FacilityId: targetFacilityId,
            AgreementId: agreement?.Id,
            AgreementNo: agreement?.AgreementNo,
            StorageUnitId: unit?.Id,
            UnitCode: unit?.UnitCode,
            AccessPointId: accessPoint.Id,
            AccessPointName: accessPoint.Name,
            AccessPointType: accessPoint.AccessPointType,
            CredentialId: credential?.Id,
            ActorName: credential?.AuthorizedMember?.FullName ?? agreement?.Customer?.FullName ?? "Staff Counter",
            Result: ev.Result,
            Reason: ev.Reason,
            OccurredAt: ev.OccurredAt
        );
    }

    public async Task<StaffLockAccessResponseDto> LockAgreementAccessV1Async(
        long employeeUserId,
        long agreementId,
        LockAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        var agreement = await _dbContext.RentalAgreements
            .Include(a => a.Facility)
            .Include(a => a.DelinquencyCases)
            .FirstOrDefaultAsync(a => a.Id == agreementId, cancellationToken);

        if (agreement == null)
            throw AppException.FromError(StaffErrors.AgreementNotFound);

        await EnsureStrictFacilityAccessAsync(employeeUserId, agreement.FacilityId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var creds = await _dbContext.AccessCredentials
            .Where(c => c.AgreementId == agreementId &&
                        (c.Status == CredentialStatusConstants.Active || c.Status == CredentialStatusConstants.Pending))
            .ToListAsync(cancellationToken);

        foreach (var c in creds)
        {
            c.Status = CredentialStatusConstants.Suspended;
            c.RevokedAt = null;
        }

        if (agreement.Status is RentalAgreementStatusConstants.Active or RentalAgreementStatusConstants.ExpiringSoon)
        {
            agreement.Status = RentalAgreementStatusConstants.Overdue;
            agreement.UpdatedAt = now;
        }

        var openCase = agreement.DelinquencyCases
            .Where(dc => dc.ResolvedAt == null)
            .OrderByDescending(dc => dc.OpenedAt)
            .FirstOrDefault();

        if (openCase != null)
        {
            _dbContext.DelinquencyActions.Add(new DelinquencyAction
            {
                DelinquencyCaseId = openCase.Id,
                ActionType = DelinquencyActionTypeConstants.CredentialSuspended,
                PerformedBy = employeeUserId,
                Details = JsonSerializer.Serialize(new
                {
                    note = request.Reason?.Trim() ?? "Access suspended manually by facility staff.",
                    suspendedCredentialsCount = creds.Count
                }),
                OccurredAt = now
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_realtimeNotifier != null)
        {
            await _realtimeNotifier.SendNotificationToUserAsync(
                agreement.CustomerId,
                $"Khóa quyền truy cập HĐ #{agreement.AgreementNo}",
                request.Reason?.Trim() ?? "Quyền truy cập mã PIN đã bị nhân viên tạm khóa do quá hạn.",
                new { agreementId = agreement.Id, lockedAt = now });
        }

        return new StaffLockAccessResponseDto(agreement.Id, creds.Count, now);
    }

    private async Task<InspectMoveOutResultDto> InspectMoveOutV1InternalAsync(
        long employeeUserId,
        long moveOutId,
        InspectMoveOutRequest request,
        CancellationToken cancellationToken)
    {
        var moveOut = await _dbContext.MoveOutRequests
            .Include(m => m.Agreement)
                .ThenInclude(a => a.Customer)
                    .ThenInclude(c => c.User)
            .Include(m => m.Agreement)
                .ThenInclude(a => a.Facility)
            .Include(m => m.Agreement)
                .ThenInclude(a => a.UnitAllocations)
                    .ThenInclude(ua => ua.StorageUnit)
            .Include(m => m.Agreement)
                .ThenInclude(a => a.AccessCredentials)
            .FirstOrDefaultAsync(m => m.Id == moveOutId, cancellationToken);

        if (moveOut == null)
            throw AppException.FromError(StaffErrors.MoveOutNotFound);

        var agreement = moveOut.Agreement;
        await EnsureStrictFacilityAccessAsync(employeeUserId, agreement.FacilityId, cancellationToken);

        if (moveOut.Status == MoveOutStatusConstants.Completed || moveOut.Status == MoveOutStatusConstants.Cancelled)
            throw AppException.FromError(StaffErrors.MoveOutNotSchedulable);

        var activeAlloc = agreement.UnitAllocations
            .OrderByDescending(ua => ua.Status == AllocationStatusConstants.Active ? 1 : 0)
            .ThenByDescending(ua => ua.Id)
            .FirstOrDefault();

        var unit = activeAlloc?.StorageUnit ?? throw AppException.FromError(StaffErrors.UnitNotFound);
        var now = DateTimeOffset.UtcNow;

        await using var tx = _dbContext.Database.IsSqlServer() && _dbContext.Database.CurrentTransaction == null
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var normalizedOverall = (request.OverallCondition ?? "good").Trim().ToLowerInvariant() switch
            {
                "good" => OverallConditionConstants.Good,
                "acceptable" => OverallConditionConstants.Acceptable,
                "damaged" or "dirty" or "cleaning" => OverallConditionConstants.Damaged,
                "unsafe" => OverallConditionConstants.Unsafe,
                _ => OverallConditionConstants.Good
            };

            var inspection = await _dbContext.Inspections
                .FirstOrDefaultAsync(i => i.AgreementId == agreement.Id
                                       && i.InspectionType == InspectionTypeConstants.CheckOut
                                       && i.Status != InspectionStatusConstants.Completed, cancellationToken);

            if (inspection == null)
            {
                inspection = new Inspection
                {
                    FacilityId = agreement.FacilityId,
                    AgreementId = agreement.Id,
                    StorageUnitId = unit.Id,
                    InspectedBy = employeeUserId,
                    InspectionType = InspectionTypeConstants.CheckOut,
                    Status = InspectionStatusConstants.Completed,
                    OverallCondition = normalizedOverall,
                    Summary = Truncate(request.Summary, 255),
                    InspectedAt = now,
                    CreatedAt = now
                };
                _dbContext.Inspections.Add(inspection);
            }
            else
            {
                inspection.InspectedBy = employeeUserId;
                inspection.StorageUnitId = unit.Id;
                inspection.OverallCondition = normalizedOverall;
                inspection.Summary = Truncate(request.Summary, 255) ?? inspection.Summary;
                inspection.Status = InspectionStatusConstants.Completed;
                inspection.InspectedAt = now;
            }
            await _dbContext.SaveChangesAsync(cancellationToken);

            decimal totalDamage = 0m;
            bool hasNonZeroRepairDamage = false;

            if (request.DamageItems != null && request.DamageItems.Count > 0)
            {
                foreach (var di in request.DamageItems)
                {
                    var charge = Math.Max(0m, di.ChargeAmount > 0 ? di.ChargeAmount : di.Cost);
                    totalDamage += charge;
                    var condLower = (di.Condition ?? string.Empty).Trim().ToLowerInvariant();
                    var isCleaningItem = di.ItemName.Contains("clean", StringComparison.OrdinalIgnoreCase)
                                         || di.ItemName.Contains("vệ sinh", StringComparison.OrdinalIgnoreCase)
                                         || di.ItemName.Contains("dọn", StringComparison.OrdinalIgnoreCase);
                    if (condLower is "damaged" or "missing" || (charge > 0 && !isCleaningItem && condLower is not ("good" or "acceptable")))
                        hasNonZeroRepairDamage = true;

                    _dbContext.InspectionItems.Add(new InspectionItem
                    {
                        InspectionId = inspection.Id,
                        ItemName = string.IsNullOrWhiteSpace(di.ItemName) ? "Inspection Item" : Truncate(di.ItemName, 150)!,
                        Condition = NormalizeInspectionItemCondition(di.Condition, charge),
                        Notes = Truncate(di.Notes, 255),
                        PhotoUrl = Truncate(di.PhotoUrl, 255),
                        ChargeAmount = charge
                    });
                }
            }
            else if (request.InspectionItems != null && request.InspectionItems.Count > 0)
            {
                foreach (var ii in request.InspectionItems)
                {
                    var isDmg = string.Equals(ii.Condition, "damaged", StringComparison.OrdinalIgnoreCase);
                    if (isDmg) hasNonZeroRepairDamage = true;
                    _dbContext.InspectionItems.Add(new InspectionItem
                    {
                        InspectionId = inspection.Id,
                        ItemName = Truncate(ii.ItemName, 150)!,
                        Condition = NormalizeInspectionItemCondition(ii.Condition, 0m),
                        Notes = Truncate(ii.Notes, 255),
                        PhotoUrl = Truncate(ii.PhotoUrl, 255),
                        ChargeAmount = 0m
                    });
                }
            }

            if (activeAlloc != null)
            {
                var existingHandover = await _dbContext.HandoverRecords
                    .FirstOrDefaultAsync(h => h.AgreementId == agreement.Id && h.HandoverType == HandoverTypeConstants.CheckOut, cancellationToken);
                if (existingHandover == null)
                {
                    _dbContext.HandoverRecords.Add(new HandoverRecord
                    {
                        AgreementId = agreement.Id,
                        UnitAllocationId = activeAlloc.Id,
                        InspectionId = inspection.Id,
                        HandoverType = HandoverTypeConstants.CheckOut,
                        HandledBy = employeeUserId,
                        StaffSignedAt = now,
                        CustomerSignedAt = now,
                        Notes = Truncate(request.Summary, 255) ?? "Move-out handover completed",
                        CreatedAt = now
                    });
                }
            }

            var deposit = agreement.DepositBalance;
            var netBalance = deposit - totalDamage;
            long? refundId = null;
            decimal refundAmount = 0m;
            decimal extraPenalty = 0m;
            long? penaltyInvoiceId = null;

            moveOut.DamageDeductionAmount = totalDamage;
            moveOut.OverdueDeductionAmount = 0m;
            moveOut.NetRefundAmount = Math.Max(0m, netBalance);

            if (netBalance > 0)
            {
                refundAmount = netBalance;
                agreement.DepositBalance = refundAmount;

                var agreementPayments = await _dbContext.Payments
                    .Include(p => p.TargetInvoice)
                    .Where(p => p.Status == PaymentConstants.StatusSucceeded
                                && (p.TargetInvoice.AgreementId == agreement.Id
                                    || (agreement.ReservationId > 0 && p.TargetInvoice.ReservationId == agreement.ReservationId)))
                    .OrderByDescending(p => p.Amount)
                    .ToListAsync(cancellationToken);

                var validPayment = agreementPayments.FirstOrDefault();
                long resolvedPaymentId = validPayment?.Id ?? 0;

                if (resolvedPaymentId == 0)
                {
                    var depInv = new Invoice
                    {
                        InvoiceNo = $"INV-DEP-{agreement.Id}-{now.ToUnixTimeSeconds()}",
                        AgreementId = agreement.Id,
                        CustomerId = agreement.CustomerId,
                        BillingPeriod = $"DEP-{now:yyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
                        IssueDate = DateOnly.FromDateTime(now.UtcDateTime),
                        DueDate = DateOnly.FromDateTime(now.UtcDateTime),
                        Currency = PaymentConstants.CurrencyVnd,
                        SubtotalAmount = deposit,
                        TaxAmount = 0m,
                        TotalAmount = deposit,
                        PaidAmount = deposit,
                        Status = InvoiceStatusConstants.Paid,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _dbContext.Invoices.Add(depInv);
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    var depPay = new Payment
                    {
                        CustomerId = agreement.CustomerId,
                        TargetInvoiceId = depInv.Id,
                        Amount = deposit,
                        Currency = PaymentConstants.CurrencyVnd,
                        Method = PaymentConstants.MethodBankTransfer,
                        Provider = "system_deposit_reconciliation",
                        IdempotencyKey = $"PAY-DEP-{agreement.Id}-{now.ToUnixTimeSeconds()}",
                        Status = PaymentConstants.StatusSucceeded,
                        Metadata = "{}",
                        PaidAt = now,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _dbContext.Payments.Add(depPay);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    resolvedPaymentId = depPay.Id;
                }

                string refundReason;
                if (!string.IsNullOrWhiteSpace(moveOut.BankName) || !string.IsNullOrWhiteSpace(moveOut.BankAccountNo))
                {
                    var bankJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        bank = moveOut.BankName,
                        acc = moveOut.BankAccountNo,
                        holder = moveOut.BankAccountHolder,
                        note = $"Hoàn cọc HĐ #{agreement.AgreementNo} (Khấu trừ thiệt hại: {totalDamage:N0} đ)"
                    });
                    refundReason = bankJson.Length <= 250
                        ? bankJson
                        : $"BANK:{moveOut.BankName}|ACC:{moveOut.BankAccountNo}|NAME:{moveOut.BankAccountHolder}";
                }
                else if (!string.IsNullOrWhiteSpace(moveOut.Reason) && (moveOut.Reason.StartsWith("{") || moveOut.Reason.StartsWith("BANK:")))
                {
                    refundReason = moveOut.Reason;
                }
                else
                {
                    refundReason = $"Hoàn cọc sau nghiệm thu trả kho HĐ #{agreement.AgreementNo} (Khấu trừ thiệt hại: {totalDamage:N0} đ).";
                }

                var refund = new Refund
                {
                    PaymentId = resolvedPaymentId,
                    AgreementId = agreement.Id,
                    Amount = refundAmount,
                    Currency = PaymentConstants.CurrencyVnd,
                    Reason = Truncate(refundReason, 250) ?? "Hoàn cọc trả kho.",
                    Provider = (!string.IsNullOrWhiteSpace(moveOut.BankName) || !string.IsNullOrWhiteSpace(moveOut.BankAccountNo))
                        ? RefundPayoutMethodConstants.BankTransfer
                        : "pending_choice",
                    IdempotencyKey = $"REFUND-MOVEOUT-{moveOut.Id}-{now.ToUnixTimeSeconds()}",
                    Status = RefundStatusConstants.Requested,
                    RequestedBy = employeeUserId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.Refunds.Add(refund);
                await _dbContext.SaveChangesAsync(cancellationToken);
                refundId = refund.Id;
            }
            else if (netBalance < 0)
            {
                extraPenalty = Math.Abs(netBalance);
                agreement.DepositBalance = netBalance; // Retain negative deposit balance (< 0) for debt alert
                agreement.NegativeDebtNotifiedAt = now;

                var penInv = new Invoice
                {
                    InvoiceNo = $"INV-PEN-{now:yyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
                    AgreementId = agreement.Id,
                    CustomerId = agreement.CustomerId,
                    BillingPeriod = $"PEN-{now:yyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
                    IssueDate = DateOnly.FromDateTime(now.UtcDateTime),
                    DueDate = DateOnly.FromDateTime(now.UtcDateTime),
                    Currency = PaymentConstants.CurrencyVnd,
                    SubtotalAmount = extraPenalty,
                    TaxAmount = 0m,
                    TotalAmount = extraPenalty,
                    PaidAmount = 0m,
                    Status = InvoiceStatusConstants.Draft,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.Invoices.Add(penInv);
                await _dbContext.SaveChangesAsync(cancellationToken);

                _dbContext.InvoiceLines.Add(new InvoiceLine
                {
                    InvoiceId = penInv.Id,
                    LineType = InvoiceLineTypeConstants.Damage,
                    Description = $"Phí hư hại vượt tiền cọc khi trả kho HĐ #{agreement.AgreementNo} (Thiếu hụt: {extraPenalty:N0} đ)",
                    Quantity = 1,
                    UnitPrice = extraPenalty,
                    LineAmount = extraPenalty,
                    Metadata = "{}"
                });
                await _dbContext.SaveChangesAsync(cancellationToken);

                penInv.Status = InvoiceStatusConstants.Overdue;
                penInv.UpdatedAt = DateTimeOffset.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                penaltyInvoiceId = penInv.Id;

                _dbContext.Notifications.Add(new Notification
                {
                    UserId = agreement.CustomerId,
                    Channel = "in_app",
                    TemplateCode = "NEGATIVE_DEPOSIT_DEBT_ALERT",
                    Subject = $"[CẢNH BÁO CÔNG NỢ] Tiền cọc âm sau nghiệm thu trả kho HĐ #{agreement.AgreementNo}",
                    DeduplicationKey = $"neg-deposit-{agreement.Id}-{penInv.Id}",
                    Payload = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        agreementId = agreement.Id,
                        agreementNo = agreement.AgreementNo,
                        unitCode = unit.UnitCode,
                        negativeDepositBalance = agreement.DepositBalance,
                        deficitAmount = extraPenalty,
                        invoiceId = penInv.Id,
                        invoiceNo = penInv.InvoiceNo
                    }),
                    Status = "sent",
                    ScheduledAt = now,
                    SentAt = now,
                    Attempts = 1,
                    CreatedAt = now
                });
            }
            else
            {
                agreement.DepositBalance = 0m;
            }

            foreach (var cred in agreement.AccessCredentials.Where(c => c.Status != CredentialStatusConstants.Revoked))
            {
                cred.Status = CredentialStatusConstants.Revoked;
                cred.RevokedAt = now;
            }

            if (activeAlloc != null && activeAlloc.Status == AllocationStatusConstants.Active)
            {
                activeAlloc.Status = AllocationStatusConstants.Released;
                activeAlloc.EndedAt = now;
                var todayLocal = DateOnly.FromDateTime(now.UtcDateTime);
                if (todayLocal > activeAlloc.AllocationStartDate)
                    activeAlloc.AllocationEndDate = todayLocal;
                else if (activeAlloc.AllocationEndDate <= activeAlloc.AllocationStartDate)
                    activeAlloc.AllocationEndDate = activeAlloc.AllocationStartDate.AddDays(1);
            }

            var nextUnitStatus = (hasNonZeroRepairDamage || normalizedOverall is OverallConditionConstants.Damaged or OverallConditionConstants.Unsafe)
                ? StorageUnitStatusConstants.Maintenance
                : StorageUnitStatusConstants.Cleaning;

            unit.PhysicalStatus = nextUnitStatus;
            unit.UpdatedAt = now;

            moveOut.Status = MoveOutStatusConstants.Completed;
            moveOut.FinalizedAt = now;
            moveOut.UpdatedAt = now;

            // Follow DomainStateMachine strictly for RentalAgreement status transitions
            if (refundAmount > 0m && agreement.Status != RentalAgreementStatusConstants.Overdue && agreement.Status != RentalAgreementStatusConstants.Terminated)
            {
                agreement.Status = RentalAgreementStatusConstants.CheckoutPending;
            }
            else if (agreement.Status is RentalAgreementStatusConstants.MoveOutScheduled or RentalAgreementStatusConstants.CheckoutPending)
            {
                agreement.Status = RentalAgreementStatusConstants.Completed;
            }
            else
            {
                agreement.Status = RentalAgreementStatusConstants.Terminated;
            }
            agreement.ActualEndDate ??= DateOnly.FromDateTime(now.UtcDateTime);
            agreement.CheckedOutAt ??= now;
            agreement.UpdatedAt = now;

            await _dbContext.SaveChangesAsync(cancellationToken);
            if (tx != null) await tx.CommitAsync(cancellationToken);

            if (_realtimeNotifier != null)
            {
                await _realtimeNotifier.SendUnitStatusChangeAsync(agreement.FacilityId, new
                {
                    unitId = unit.Id,
                    unitCode = unit.UnitCode,
                    physicalStatus = unit.PhysicalStatus
                });
                await _realtimeNotifier.SendNotificationToUserAsync(
                    agreement.CustomerId,
                    $"Hoàn tất kiểm tra trả kho #{agreement.AgreementNo}",
                    refundAmount > 0
                        ? $"Kho đã được nghiệm thu. Số tiền hoàn cọc dự kiến: {refundAmount:N0} đ."
                        : (extraPenalty > 0
                            ? $"[CẢNH BÁO CÔNG NỢ] Phí hư hại vượt tiền cọc {extraPenalty:N0} đ. Vui lòng thanh toán ngay trên hệ thống."
                            : "Kho đã được nghiệm thu trả kho hoàn tất."),
                    new { moveOutId = moveOut.Id, agreementId = agreement.Id, refundId, refundAmount, penaltyInvoiceId, negativeDepositBalance = agreement.DepositBalance });
            }

            if (extraPenalty > 0 && _emailService != null && !string.IsNullOrWhiteSpace(agreement.Customer?.User?.Email))
            {
                try
                {
                    await _emailService.SendEmailAsync(
                        agreement.Customer.User.Email,
                        $"[Self-Storage] CẢNH BÁO CÔNG NỢ TRẢ KHO - Hợp đồng #{agreement.AgreementNo}",
                        $"<h3>Kính gửi {agreement.Customer.FullName},</h3>" +
                        $"<p>Biên bản nghiệm thu trả kho cho hợp đồng <strong>{agreement.AgreementNo}</strong> (Ô kho <strong>{unit.UnitCode}</strong>) đã ghi nhận chi phí thiệt hại vượt quá số dư tiền cọc.</p>" +
                        $"<p><strong>Số tiền nợ thiếu hụt cần thanh toán ngay: {extraPenalty:N0} VND</strong> (Số dư tiền cọc hiện tại: {agreement.DepositBalance:N0} VND).</p>" +
                        $"<p>Vui lòng đăng nhập vào hệ thống và quét mã VietQR trên thanh cảnh báo đỏ để tất toán công nợ.</p>",
                        cancellationToken);
                }
                catch
                {
                    // Non-blocking email notification
                }
            }

            return new InspectMoveOutResultDto(
                MoveOutId: moveOut.Id,
                InspectionId: inspection.Id,
                AgreementId: agreement.Id,
                TotalDamageCharges: totalDamage,
                AgreementStatus: agreement.Status,
                UnitStatus: unit.PhysicalStatus,
                InspectedAt: now,
                RefundId: refundId,
                RefundAmount: refundAmount,
                ExtraPenalty: extraPenalty,
                PenaltyInvoiceId: penaltyInvoiceId
            );
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
