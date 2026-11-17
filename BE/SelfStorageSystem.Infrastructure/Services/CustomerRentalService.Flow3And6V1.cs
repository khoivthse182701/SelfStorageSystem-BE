using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Rentals;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Common;

namespace SelfStorageSystem.Infrastructure.Services;

public partial class CustomerRentalService
{
    public Task<IReadOnlyList<RentalSummaryDto>> GetMyRentalsV1Async(
        long customerId,
        string? status = null,
        CancellationToken cancellationToken = default)
        => QueryMyRentalsAsync(customerId, status, cancellationToken);

    private static HashSet<string>? ParseRentalStatusFilter(string? statusFilter)
    {
        if (string.IsNullOrWhiteSpace(statusFilter))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                RentalAgreementStatusConstants.Active,
                RentalAgreementStatusConstants.ExpiringSoon,
                RentalAgreementStatusConstants.Draft,
                RentalAgreementStatusConstants.MoveOutScheduled,
                RentalAgreementStatusConstants.Overdue,
                RentalAgreementStatusConstants.CheckoutPending
            };
        }

        if (string.Equals(statusFilter.Trim(), "all", StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = statusFilter
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return parts.Count == 0 ? null : parts;
    }

    public async Task<RentalDetailDto> GetRentalDetailAsync(
        long customerId,
        long agreementId,
        CancellationToken cancellationToken = default)
    {
        var agreement = await _dbContext.RentalAgreements
            .AsNoTracking()
            .Include(a => a.Facility)
            .Include(a => a.UnitAllocations)
                .ThenInclude(ua => ua.StorageUnit)
                    .ThenInclude(su => su!.UnitType)
            .Include(a => a.Invoices)
            .Include(a => a.MoveOutRequest)
            .Include(a => a.HandoverRecords)
                .ThenInclude(h => h.Inspection)
            .Include(a => a.HandoverRecords)
                .ThenInclude(h => h.HandledByNavigation)
            .FirstOrDefaultAsync(a => a.Id == agreementId, cancellationToken);

        if (agreement == null)
            throw AppException.FromError(RentalErrors.AgreementNotFound);

        if (agreement.CustomerId != customerId)
            throw AppException.FromError(RentalErrors.AccessForbidden);

        var tz = TimeZoneHelper.ResolveTimeZone(agreement.Facility?.Timezone, _rentalSettings.DefaultTimezone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime);

        var activeAlloc = agreement.UnitAllocations.FirstOrDefault(ua => ua.Status == AllocationStatusConstants.Active)
                          ?? agreement.UnitAllocations.OrderByDescending(ua => ua.Id).FirstOrDefault();
        var unit = activeAlloc?.StorageUnit;
        var unitType = unit?.UnitType;

        var daysUntilExpiry = Math.Max(0, agreement.EndDate.DayNumber - today.DayNumber);
        var isExpiringSoon = string.Equals(agreement.Status, RentalAgreementStatusConstants.ExpiringSoon, StringComparison.OrdinalIgnoreCase)
                             || (daysUntilExpiry <= 7 && (agreement.Status == RentalAgreementStatusConstants.Active || agreement.Status == "extended"));

        var hasOverdueDebt = agreement.Invoices.Any(i => IsOverdueRentalDebt(i, today.AddDays(-1)))
                             || string.Equals(agreement.Status, RentalAgreementStatusConstants.Overdue, StringComparison.OrdinalIgnoreCase);

        var outstandingDebt = agreement.Invoices
            .Where(i => i.Status is InvoiceStatusConstants.Open or InvoiceStatusConstants.PartiallyPaid or InvoiceStatusConstants.Overdue)
            .Sum(i => Math.Max(0m, i.TotalAmount - i.PaidAmount));

        var latestHandover = agreement.HandoverRecords
            .OrderByDescending(h => h.CreatedAt)
            .FirstOrDefault();

        var latestInvoices = agreement.Invoices
            .OrderByDescending(i => i.IssueDate)
            .ThenByDescending(i => i.Id)
            .Take(10)
            .Select(i => new RentalDetailInvoiceDto
            {
                InvoiceId = i.Id,
                InvoiceNo = i.InvoiceNo,
                BillingPeriod = i.BillingPeriod,
                IssueDate = i.IssueDate,
                DueDate = i.DueDate,
                TotalAmount = i.TotalAmount,
                PaidAmount = i.PaidAmount,
                Status = i.Status
            })
            .ToList();

        var pendingRenewal = await _dbContext.RentalRenewals
            .AsNoTracking()
            .Where(r => r.AgreementId == agreementId && r.Status == RenewalStatusConstants.PendingPayment)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        RentalDetailRenewalDto? pendingRenewalDto = null;
        if (pendingRenewal != null)
        {
            var rnwPeriod = $"{PaymentConstants.RenewalInvoicePrefix}{pendingRenewal.Id}";
            var rnwInv = agreement.Invoices
                .FirstOrDefault(i => i.BillingPeriod == rnwPeriod && i.Status != InvoiceStatusConstants.Voided);
            var months = Math.Max(1, (pendingRenewal.RequestedEndDate.Year - pendingRenewal.OldEndDate.Year) * 12
                + pendingRenewal.RequestedEndDate.Month - pendingRenewal.OldEndDate.Month);
            var monthly = pendingRenewal.NewMonthlyRate ?? pendingRenewal.OldMonthlyRate;
            pendingRenewalDto = new RentalDetailRenewalDto
            {
                RenewalId = pendingRenewal.Id,
                OldEndDate = pendingRenewal.OldEndDate,
                RequestedEndDate = pendingRenewal.RequestedEndDate,
                MonthlyRate = monthly,
                Status = pendingRenewal.Status,
                InvoiceId = rnwInv?.Id,
                InvoiceNo = rnwInv?.InvoiceNo,
                TotalDueAmount = rnwInv?.TotalAmount ?? (monthly * months),
                CreatedAt = pendingRenewal.CreatedAt
            };
        }

        MoveOutResponseDto? pendingMoveOutDto = null;
        if (agreement.MoveOutRequest != null && agreement.MoveOutRequest.Status != MoveOutStatusConstants.Cancelled)
        {
            var mo = agreement.MoveOutRequest;
            pendingMoveOutDto = new MoveOutResponseDto(
                Id: mo.Id,
                AgreementId: mo.AgreementId,
                RequestedMoveOutDate: mo.RequestedMoveOutDate,
                Status: mo.Status,
                Reason: mo.Reason,
                CreatedAt: mo.CreatedAt,
                MoveOutRequestId: mo.Id,
                MoveOutQrToken: _qrTokenService?.IssueToken("move_out", agreement.ReservationId > 0 ? agreement.ReservationId : null, agreement.Id, customerId, agreement.FacilityId, TimeSpan.FromDays(14))
            );
        }

        var activeTransfer = await GetLatestTransferRequestAsync(customerId, agreementId, cancellationToken);

        return new RentalDetailDto
        {
            AgreementId = agreement.Id,
            AgreementNo = agreement.AgreementNo,
            ReservationId = agreement.ReservationId,
            Status = agreement.Status,
            StartDate = agreement.StartDate,
            EndDate = agreement.EndDate,
            ActualEndDate = agreement.ActualEndDate,
            MonthlyRate = agreement.MonthlyRateSnapshot,
            DepositSnapshot = agreement.DepositSnapshot,
            DepositBalance = agreement.DepositBalance,
            PolicyVersionId = agreement.PolicyVersionId,
            SignedAt = agreement.SignedAt,
            CheckedInAt = agreement.CheckedInAt,
            CheckedOutAt = agreement.CheckedOutAt,
            DaysUntilExpiry = daysUntilExpiry,
            IsExpiringSoon = isExpiringSoon,
            HasOverdueDebt = hasOverdueDebt,
            OutstandingDebtAmount = outstandingDebt,
            Facility = new RentalDetailFacilityDto
            {
                FacilityId = agreement.FacilityId,
                Code = agreement.Facility?.Code ?? string.Empty,
                Name = agreement.Facility?.Name ?? string.Empty,
                AddressLine = agreement.Facility?.AddressLine ?? string.Empty,
                City = agreement.Facility?.City ?? string.Empty,
                Timezone = agreement.Facility?.Timezone ?? _rentalSettings.DefaultTimezone,
                OpeningTime = agreement.Facility?.OpeningTime,
                ClosingTime = agreement.Facility?.ClosingTime
            },
            Unit = new RentalDetailUnitDto
            {
                StorageUnitId = unit?.Id ?? 0,
                UnitCode = unit?.UnitCode ?? string.Empty,
                UnitTypeName = unitType?.Name ?? string.Empty,
                FloorLabel = unit?.FloorLabel,
                ZoneLabel = unit?.ZoneLabel,
                Dimensions = unitType != null ? $"{unitType.WidthM:F1}m x {unitType.LengthM:F1}m x {unitType.HeightM:F1}m" : string.Empty,
                AreaM2 = unitType?.AreaM2,
                VolumeM3 = unitType?.VolumeM3,
                PhysicalStatus = unit?.PhysicalStatus ?? string.Empty,
                ClimateControlled = unitType?.ClimateControlled ?? false
            },
            Handover = latestHandover == null ? null : new HandoverRecordDto
            {
                HandoverId = latestHandover.Id,
                AgreementId = agreement.Id,
                AgreementNo = agreement.AgreementNo,
                HandoverType = latestHandover.HandoverType,
                HandledByName = latestHandover.HandledByNavigation?.FullName,
                CustomerSignedAt = latestHandover.CustomerSignedAt,
                StaffSignedAt = latestHandover.StaffSignedAt,
                HandoverNotes = latestHandover.Notes,
                CreatedAt = latestHandover.CreatedAt,
                Inspection = latestHandover.Inspection == null ? null : new HandoverInspectionDto
                {
                    InspectionId = latestHandover.Inspection.Id,
                    InspectionType = latestHandover.Inspection.InspectionType,
                    Status = latestHandover.Inspection.Status,
                    OverallCondition = latestHandover.Inspection.OverallCondition,
                    Summary = latestHandover.Inspection.Summary,
                    InspectedAt = latestHandover.Inspection.InspectedAt
                }
            },
            LatestInvoices = latestInvoices,
            PendingRenewal = pendingRenewalDto,
            PendingMoveOut = pendingMoveOutDto,
            ActiveTransfer = activeTransfer
        };
    }

    public async Task<PagedResult<AccessEventDto>> GetAccessLogsAsync(
        long customerId,
        long agreementId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var agreement = await _dbContext.RentalAgreements
            .AsNoTracking()
            .Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.Id == agreementId, cancellationToken);

        if (agreement == null)
            throw AppException.FromError(RentalErrors.AgreementNotFound);

        if (agreement.CustomerId != customerId)
            throw AppException.FromError(RentalErrors.AccessForbidden);

        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.AccessEvents
            .AsNoTracking()
            .Include(ae => ae.AccessPoint)
            .Include(ae => ae.Credential)
                .ThenInclude(c => c!.AuthorizedMember)
            .Where(ae => ae.Credential != null && ae.Credential.AgreementId == agreementId);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(ae => ae.OccurredAt)
            .ThenByDescending(ae => ae.Id)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToListAsync(cancellationToken);

        var customerName = agreement.Customer?.FullName ?? "Customer";
        var items = rows.Select(ae => new AccessEventDto(
            Id: ae.Id,
            OccurredAt: ae.OccurredAt,
            AccessPointName: ae.AccessPoint?.Name ?? "Storage Door",
            AccessPointType: ae.AccessPoint?.AccessPointType ?? "unit_controller",
            ActorName: ae.Credential?.AuthorizedMember?.FullName ?? customerName,
            Result: ae.Result,
            Reason: ae.Reason
        )).ToList();

        return new PagedResult<AccessEventDto>(items, totalCount, safePage, safeSize);
    }

    private async Task<AccessPoint?> ResolveAccessPointForUnlockAsync(
        long facilityId,
        long? requestedAccessPointId,
        CancellationToken cancellationToken)
    {
        if (requestedAccessPointId.HasValue)
        {
            var requested = await _dbContext.AccessPoints
                .FirstOrDefaultAsync(ap =>
                    ap.Id == requestedAccessPointId.Value &&
                    ap.FacilityId == facilityId &&
                    ap.Status == "active", cancellationToken);

            if (requested == null)
                throw AppException.FromError(RentalErrors.AccessPointNotFound);

            return requested;
        }

        return await _dbContext.AccessPoints
            .Where(ap => ap.FacilityId == facilityId && ap.Status == "active")
            .OrderBy(ap => ap.AccessPointType == "unit_controller" || ap.AccessPointType == "smart_lock" ? 0 : 1)
            .ThenBy(ap => ap.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<AccessEvent> RecordUnlockAccessEventAsync(
        long credentialId,
        long accessPointId,
        string result,
        string reason,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var ev = new AccessEvent
        {
            CredentialId = credentialId,
            AccessPointId = accessPointId,
            OccurredAt = occurredAt,
            Result = result,
            Reason = reason,
            Metadata = "{\"source\":\"customer_unlock\"}"
        };
        _dbContext.AccessEvents.Add(ev);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ev;
    }

    private async Task<AuthorizedMemberDto> AddAuthorizedMemberV1InternalAsync(
        long customerId,
        long agreementId,
        CreateAuthorizedMemberRequest request,
        CancellationToken cancellationToken)
    {
        var agreement = await _dbContext.RentalAgreements
            .Include(a => a.Facility)
            .FirstOrDefaultAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (agreement == null)
            throw AppException.FromError(RentalErrors.AgreementNotFound);

        if (agreement.Status is not (RentalAgreementStatusConstants.Active or RentalAgreementStatusConstants.ExpiringSoon or "extended"))
            throw AppException.FromError(RentalErrors.AgreementNotActive);

        if (string.IsNullOrWhiteSpace(request.FullName))
            throw AppException.FromError(RentalErrors.EmptyMemberName);

        var now = DateTimeOffset.UtcNow;
        if (request.ValidTo.HasValue && request.ValidTo.Value <= now)
            throw AppException.FromError(RentalErrors.InvalidAuthorizedMemberWindow);

        var member = new AuthorizedAccessMember
        {
            AgreementId = agreementId,
            FullName = request.FullName.Trim(),
            IdentityFingerprint = request.IdentityFingerprint?.Trim(),
            RelationshipToCustomer = request.RelationshipToCustomer?.Trim(),
            ValidFrom = now,
            ValidTo = request.ValidTo,
            Status = AuthorizedMemberStatusConstants.Active,
            CreatedBy = customerId,
            CreatedAt = now
        };

        _dbContext.AuthorizedAccessMembers.Add(member);
        await _dbContext.SaveChangesAsync(cancellationToken);

        string? issuedMemberPin = null;
        if (request.IssuePin)
        {
            issuedMemberPin = GenerateSecureNumericPin();
            var expiresAt = member.ValidTo.HasValue && member.ValidTo.Value > now
                ? member.ValidTo
                : null;
            _dbContext.AccessCredentials.Add(new AccessCredential
            {
                AgreementId = agreementId,
                AuthorizedMemberId = member.Id,
                CredentialType = CredentialTypeConstants.Pin,
                SecretDigest = BCrypt.Net.BCrypt.HashPassword(issuedMemberPin),
                DisplayHint = PinProtectionHelper.Encrypt(issuedMemberPin),
                Status = CredentialStatusConstants.Active,
                IssuedAt = now,
                ExpiresAt = expiresAt,
                CreatedAt = now
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new AuthorizedMemberDto(
            Id: member.Id,
            AgreementId: member.AgreementId,
            FullName: member.FullName,
            IdentityFingerprint: member.IdentityFingerprint,
            RelationshipToCustomer: member.RelationshipToCustomer,
            ValidFrom: member.ValidFrom,
            ValidTo: member.ValidTo,
            Status: member.Status,
            CreatedAt: member.CreatedAt,
            IssuedMemberPin: issuedMemberPin
        );
    }

    public async Task<RevokeAuthorizedMemberResponseDto> RevokeAuthorizedMemberV1Async(
        long customerId,
        long agreementId,
        long memberId,
        CancellationToken cancellationToken = default)
    {
        var agreementExists = await _dbContext.RentalAgreements
            .AnyAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (!agreementExists)
            throw AppException.FromError(RentalErrors.AgreementNotFound);

        var member = await _dbContext.AuthorizedAccessMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.AgreementId == agreementId, cancellationToken);

        if (member == null || member.Status != AuthorizedMemberStatusConstants.Active)
            throw AppException.FromError(RentalErrors.MemberNotFound);

        var now = DateTimeOffset.UtcNow;
        member.Status = AuthorizedMemberStatusConstants.Revoked;
        if (!member.ValidTo.HasValue || member.ValidTo.Value <= member.ValidFrom)
        {
            member.ValidTo = now > member.ValidFrom ? now : member.ValidFrom.AddSeconds(1);
        }

        var linkedCreds = await _dbContext.AccessCredentials
            .Where(c => c.AuthorizedMemberId == memberId && c.Status != CredentialStatusConstants.Revoked)
            .ToListAsync(cancellationToken);

        foreach (var cred in linkedCreds)
        {
            cred.Status = CredentialStatusConstants.Revoked;
            cred.RevokedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return new RevokeAuthorizedMemberResponseDto(member.Id, member.Status, now);
    }

    public async Task<RefundPreviewDto> GetRefundPreviewAsync(
        long customerId,
        long agreementId,
        DateOnly? requestedMoveOutDate,
        CancellationToken cancellationToken = default)
    {
        var agreement = await _dbContext.RentalAgreements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (agreement == null)
            throw AppException.FromError(RentalErrors.AgreementNotFound);

        var facilityTzStr = await _dbContext.Facilities
            .AsNoTracking()
            .Where(f => f.Id == agreement.FacilityId)
            .Select(f => f.Timezone)
            .FirstOrDefaultAsync(cancellationToken);
        var tz = TimeZoneHelper.ResolveTimeZone(facilityTzStr, _rentalSettings.DefaultTimezone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime);

        var originalDeposit = agreement.DepositSnapshot > 0 ? agreement.DepositSnapshot : agreement.DepositBalance;
        var depositBalance = agreement.DepositBalance;
        var estimatedCleaning = 0m;
        var estimatedRepair = 0m;

        var unpaidInvoices = await _dbContext.Invoices
            .AsNoTracking()
            .Where(i => i.AgreementId == agreementId &&
                        (i.Status == InvoiceStatusConstants.Overdue ||
                         i.Status == InvoiceStatusConstants.Open ||
                         i.Status == InvoiceStatusConstants.PartiallyPaid) &&
                        (i.BillingPeriod == null || !i.BillingPeriod.StartsWith(TransferConstants.BillingPeriodPrefix)))
            .ToListAsync(cancellationToken);

        var unpaidDebt = unpaidInvoices.Sum(i => Math.Max(0m, i.TotalAmount - i.PaidAmount));

        decimal estimatedUnusedRentRefund = 0m;
        decimal earlyTerminationFee = 0m;

        if (requestedMoveOutDate.HasValue && requestedMoveOutDate.Value < agreement.EndDate)
        {
            var effectiveDate = requestedMoveOutDate.Value > today ? requestedMoveOutDate.Value : today;
            var remainingDays = Math.Max(0, agreement.EndDate.DayNumber - effectiveDate.DayNumber);
            if (remainingDays > 0 && agreement.MonthlyRateSnapshot > 0)
            {
                var rawUnused = Math.Round((agreement.MonthlyRateSnapshot / 30m) * remainingDays, 0, MidpointRounding.AwayFromZero);
                var policyResult = await EvaluateEarlyMoveOutPolicyAsync(agreement, rawUnused, cancellationToken);
                estimatedUnusedRentRefund = policyResult.UnusedRentRefund;
                earlyTerminationFee = policyResult.EarlyTerminationFee;
            }
        }

        var estimatedNetRefund = Math.Max(0m, depositBalance + estimatedUnusedRentRefund - (estimatedCleaning + estimatedRepair + unpaidDebt + earlyTerminationFee));

        var note = unpaidInvoices.Any()
            ? $"{_rentalSettings.DefaultRefundPreviewNote} Khoản khấu trừ công nợ bao gồm tất cả các hóa đơn mở và quá hạn chưa thanh toán của hợp đồng."
            : _rentalSettings.DefaultRefundPreviewNote;

        return new RefundPreviewDto(
            AgreementId: agreement.Id,
            AgreementNo: agreement.AgreementNo,
            DepositBalance: depositBalance,
            EstimatedCleaningFee: estimatedCleaning,
            EstimatedRepairFee: estimatedRepair,
            EstimatedOverdueCharges: unpaidDebt,
            EstimatedNetRefund: estimatedNetRefund,
            Note: note,
            OriginalDeposit: originalDeposit,
            UnpaidDebt: unpaidDebt,
            EstimatedUnusedRentRefund: estimatedUnusedRentRefund,
            EarlyTerminationFee: earlyTerminationFee
        );
    }

    private async Task<(decimal UnusedRentRefund, decimal EarlyTerminationFee)> EvaluateEarlyMoveOutPolicyAsync(
        RentalAgreement agreement,
        decimal rawUnusedRent,
        CancellationToken cancellationToken)
    {
        var policy = await _dbContext.PolicyVersions
            .AsNoTracking()
            .Where(p => p.PolicyType == PolicyType.Cancellation.ToDbValue() || p.PolicyType == PolicyType.MoveOut.ToDbValue())
            .OrderByDescending(p => p.ValidFrom)
            .FirstOrDefaultAsync(cancellationToken);

        decimal unusedPercent = 100m;
        decimal earlyFeePercent = 0m;

        if (!string.IsNullOrWhiteSpace(policy?.Content))
        {
            try
            {
                using var doc = JsonDocument.Parse(policy.Content);
                if (doc.RootElement.TryGetProperty("unused_rent_refund_percent", out var up) && up.TryGetDecimal(out var upVal))
                    unusedPercent = Math.Clamp(upVal, 0m, 100m);
                if (doc.RootElement.TryGetProperty("early_termination_fee_percent", out var fp) && fp.TryGetDecimal(out var fpVal))
                    earlyFeePercent = Math.Clamp(fpVal, 0m, 100m);
            }
            catch (JsonException)
            {
                // Ignore malformed policy JSON and keep safe defaults.
            }
        }

        var unusedRefund = Math.Round(rawUnusedRent * (unusedPercent / 100m), 0, MidpointRounding.AwayFromZero);
        var earlyFee = Math.Round(rawUnusedRent * (earlyFeePercent / 100m), 0, MidpointRounding.AwayFromZero);
        return (unusedRefund, earlyFee);
    }

    private async Task<RenewAgreementResponseDto> RenewAgreementV1InternalAsync(
        long customerId,
        long agreementId,
        RenewAgreementRequest request,
        CancellationToken cancellationToken)
    {
        var months = request.ResolvedMonths;
        if (months < _rentalSettings.MinRenewalMonths || months > _rentalSettings.MaxRenewalMonths)
            throw AppException.FromError(RentalErrors.InvalidRenewalMonths);

        var agreement = await _dbContext.RentalAgreements
            .Include(a => a.Invoices)
                .ThenInclude(i => i.InvoiceLines)
            .Include(a => a.UnitAllocations)
                .ThenInclude(ua => ua.StorageUnit)
            .FirstOrDefaultAsync(a => a.Id == agreementId && a.CustomerId == customerId, cancellationToken);

        if (agreement == null)
            throw AppException.FromError(RentalErrors.AgreementNotFound);

        if (agreement.Status is RentalAgreementStatusConstants.Completed or RentalAgreementStatusConstants.Cancelled
            or RentalAgreementStatusConstants.Terminated or "closed")
        {
            throw AppException.FromError(RentalErrors.AgreementNotActive);
        }

        var now = DateTimeOffset.UtcNow;
        var rnwFacilityTz = await _dbContext.Facilities
            .AsNoTracking()
            .Where(f => f.Id == agreement.FacilityId)
            .Select(f => f.Timezone)
            .FirstOrDefaultAsync(cancellationToken);
        var tz = TimeZoneHelper.ResolveTimeZone(rnwFacilityTz, _rentalSettings.DefaultTimezone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, tz).DateTime);

        var activePendingRenewals = await _dbContext.RentalRenewals
            .Where(r => r.AgreementId == agreementId && r.Status == RenewalStatusConstants.PendingPayment)
            .ToListAsync(cancellationToken);

        if (activePendingRenewals.Count > 0)
        {
            var oldPeriods = activePendingRenewals.Select(r => $"{PaymentConstants.RenewalInvoicePrefix}{r.Id}").ToList();
            foreach (var oldRnw in activePendingRenewals)
            {
                oldRnw.Status = RenewalStatusConstants.Cancelled;
            }

            var oldInvoices = await _dbContext.Invoices
                .Where(i => i.AgreementId == agreementId && i.BillingPeriod != null && oldPeriods.Contains(i.BillingPeriod)
                            && i.Status != InvoiceStatusConstants.Paid && i.Status != InvoiceStatusConstants.Voided)
                .ToListAsync(cancellationToken);
            foreach (var oldInv in oldInvoices)
            {
                oldInv.Status = InvoiceStatusConstants.Voided;
                oldInv.VoidedAt = now;
                oldInv.UpdatedAt = now;
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var activeAlloc = agreement.UnitAllocations.FirstOrDefault(a => a.Status == AllocationStatusConstants.Active)
                          ?? agreement.UnitAllocations.OrderByDescending(a => a.Id).FirstOrDefault();
        var unitTypeId = activeAlloc?.StorageUnit?.UnitTypeId ?? 0;

        decimal currentRate = agreement.MonthlyRateSnapshot;
        if (unitTypeId > 0)
        {
            var activeRate = await _dbContext.FacilityRates
                .Where(fr => fr.FacilityId == agreement.FacilityId &&
                             fr.UnitTypeId == unitTypeId &&
                             fr.ValidFrom <= today &&
                             (fr.ValidTo == null || fr.ValidTo >= today))
                .OrderByDescending(fr => fr.ValidFrom)
                .ThenByDescending(fr => fr.Id)
                .Select(fr => fr.MonthlyRate)
                .FirstOrDefaultAsync(cancellationToken);

            if (activeRate > 0)
                currentRate = activeRate;
        }

        var baseRent = currentRate * months;
        decimal promoDiscount = 0m;
        Promotion? appliedPromo = null;

        if (!string.IsNullOrWhiteSpace(request.PromotionCode))
        {
            var code = request.PromotionCode.Trim().ToUpperInvariant();
            appliedPromo = await _dbContext.Promotions
                .Include(p => p.PromotionRules)
                .FirstOrDefaultAsync(p => p.Code.ToUpper() == code
                                          && p.IsActive
                                          && p.ValidFrom <= now
                                          && p.ValidTo >= now, cancellationToken);

            if (appliedPromo == null)
                throw AppException.FromError(ReservationErrors.PromotionInvalidOrExpired);

            if (appliedPromo.UsageLimit.HasValue)
            {
                var used = await _dbContext.PromotionRedemptions.CountAsync(pr =>
                    pr.PromotionId == appliedPromo.Id &&
                    pr.Status != PromotionRedemptionStatusConstants.Released, cancellationToken);
                if (used >= appliedPromo.UsageLimit.Value)
                    throw AppException.FromError(ReservationErrors.PromotionUsageLimitReached);
            }

            if (appliedPromo.PerCustomerLimit.HasValue)
            {
                var customerUsed = await _dbContext.PromotionRedemptions.CountAsync(pr =>
                    pr.PromotionId == appliedPromo.Id &&
                    pr.CustomerId == customerId &&
                    pr.Status != PromotionRedemptionStatusConstants.Released, cancellationToken);
                if (customerUsed >= appliedPromo.PerCustomerLimit.Value)
                    throw AppException.FromError(ReservationErrors.PromotionPerCustomerLimitReached(appliedPromo.PerCustomerLimit.Value));
            }

            promoDiscount = string.Equals(appliedPromo.DiscountType, PromotionDiscountType.Percentage.ToDbValue(), StringComparison.OrdinalIgnoreCase)
                ? Math.Round(baseRent * (appliedPromo.DiscountValue / 100m), 0, MidpointRounding.AwayFromZero)
                : appliedPromo.DiscountValue;

            if (appliedPromo.MaxDiscountAmount.HasValue && promoDiscount > appliedPromo.MaxDiscountAmount.Value)
                promoDiscount = appliedPromo.MaxDiscountAmount.Value;

            promoDiscount = Math.Clamp(promoDiscount, 0m, baseRent);
        }

        var discountedRent = Math.Max(0m, baseRent - promoDiscount);

        var overdueInvoices = agreement.Invoices
            .Where(i =>
                i.Status is InvoiceStatusConstants.Open or InvoiceStatusConstants.PartiallyPaid or InvoiceStatusConstants.Overdue
                && (i.Status == InvoiceStatusConstants.Overdue || i.DueDate < today)
                && (i.TotalAmount - i.PaidAmount) > 0
                && (i.BillingPeriod == null || (!i.BillingPeriod.StartsWith(TransferConstants.BillingPeriodPrefix, StringComparison.OrdinalIgnoreCase)
                                                 && !i.BillingPeriod.StartsWith(PaymentConstants.RenewalInvoicePrefix, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        var overdueDebtTotal = overdueInvoices.Sum(i => Math.Max(0m, i.TotalAmount - i.PaidAmount));
        var overdueLateFeesOnly = overdueInvoices
            .SelectMany(i => i.InvoiceLines)
            .Where(l => string.Equals(l.LineType, InvoiceLineTypeConstants.LateFee, StringComparison.OrdinalIgnoreCase))
            .Sum(l => l.LineAmount ?? 0m);
        var reportedLateFee = overdueDebtTotal > 0 ? overdueDebtTotal : overdueLateFeesOnly;

        var newEndDate = agreement.EndDate.AddMonths(months);
        var totalDueAmount = discountedRent + overdueDebtTotal;

        var renewal = new RentalRenewal
        {
            AgreementId = agreement.Id,
            RequestedBy = customerId,
            OldEndDate = agreement.EndDate,
            RequestedEndDate = newEndDate,
            OldMonthlyRate = agreement.MonthlyRateSnapshot,
            NewMonthlyRate = currentRate,
            Status = RenewalStatusConstants.PendingPayment,
            CreatedAt = now
        };

        _dbContext.RentalRenewals.Add(renewal);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var renewalInvoice = new Invoice
        {
            InvoiceNo = $"INV-RNW-{renewal.Id}-{now:yyMMdd}",
            AgreementId = agreement.Id,
            CustomerId = customerId,
            BillingPeriod = $"{PaymentConstants.RenewalInvoicePrefix}{renewal.Id}",
            IssueDate = today,
            DueDate = today.AddDays(3),
            Currency = PaymentConstants.CurrencyVnd,
            SubtotalAmount = baseRent + overdueDebtTotal,
            DiscountAmount = promoDiscount,
            TaxAmount = 0m,
            TotalAmount = totalDueAmount,
            PaidAmount = 0m,
            Status = InvoiceStatusConstants.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Invoices.Add(renewalInvoice);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var lines = new List<InvoiceLine>
        {
            new()
            {
                InvoiceId = renewalInvoice.Id,
                LineType = InvoiceLineTypeConstants.Rent,
                Description = $"Gia hạn hợp đồng #{agreement.AgreementNo} ({months} tháng, đến {newEndDate:dd/MM/yyyy})",
                Quantity = months,
                UnitPrice = currentRate,
                LineAmount = baseRent,
                Metadata = appliedPromo != null ? JsonSerializer.Serialize(new { promoCode = appliedPromo.Code }) : "{}"
            }
        };

        if (promoDiscount > 0 && appliedPromo != null)
        {
            lines.Add(new InvoiceLine
            {
                InvoiceId = renewalInvoice.Id,
                LineType = InvoiceLineTypeConstants.Discount,
                Description = $"Mã khuyến mãi {appliedPromo.Code}",
                Quantity = 1,
                UnitPrice = -promoDiscount,
                LineAmount = -promoDiscount,
                Metadata = JsonSerializer.Serialize(new { promotionId = appliedPromo.Id, code = appliedPromo.Code })
            });
        }

        if (overdueDebtTotal > 0)
        {
            lines.Add(new InvoiceLine
            {
                InvoiceId = renewalInvoice.Id,
                LineType = InvoiceLineTypeConstants.LateFee,
                Description = "Công nợ quá hạn & phí phạt trễ hạn tất toán kèm gia hạn",
                Quantity = 1,
                UnitPrice = overdueDebtTotal,
                LineAmount = overdueDebtTotal,
                Metadata = JsonSerializer.Serialize(new { invoiceIds = overdueInvoices.Select(i => i.Id).ToArray() })
            });
        }

        _dbContext.InvoiceLines.AddRange(lines);
        await _dbContext.SaveChangesAsync(cancellationToken);

        renewalInvoice.Status = InvoiceStatusConstants.Open;
        renewalInvoice.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RenewAgreementResponseDto(
            RenewalId: renewal.Id,
            AgreementId: agreement.Id,
            OldEndDate: renewal.OldEndDate,
            NewEndDate: renewal.RequestedEndDate,
            MonthlyRate: currentRate,
            TotalRenewalAmount: discountedRent,
            Status: renewal.Status,
            CreatedAt: renewal.CreatedAt,
            InvoiceId: renewalInvoice.Id,
            InvoiceNo: renewalInvoice.InvoiceNo,
            RequestedEndDate: renewal.RequestedEndDate,
            OverdueLateFeeAmount: reportedLateFee,
            TotalDueAmount: totalDueAmount,
            CheckoutRequired: totalDueAmount > 0m
        );
    }
}
