using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SelfStorageSystem.Contracts.Manager;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Common;

namespace SelfStorageSystem.Infrastructure.Services;

public partial class FacilityManagerService
{
    public async Task<ManagerLiquidateAgreementResultDto> LiquidateAgreementAsync(
        long agreementId,
        ManagerLiquidateAgreementRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        var agreement = await _dbContext.RentalAgreements
            .Include(a => a.Facility)
            .Include(a => a.UnitAllocations)
                .ThenInclude(ua => ua.StorageUnit)
            .Include(a => a.AccessCredentials)
            .Include(a => a.Invoices)
            .Include(a => a.DelinquencyCases)
            .FirstOrDefaultAsync(a => a.Id == agreementId, cancellationToken);

        if (agreement == null)
            throw AppException.FromError(ManagerErrors.AgreementNotFound);

        await EnsureFacilityAccessAsync(agreement.FacilityId, currentUserId, isSystemAdmin, cancellationToken);

        if (agreement.Status is RentalAgreementStatusConstants.Terminated or RentalAgreementStatusConstants.Completed or RentalAgreementStatusConstants.Cancelled or "closed")
            throw AppException.FromError(ManagerErrors.AlreadyLiquidated);

        if (agreement.Status is not (RentalAgreementStatusConstants.Overdue or "defaulted"))
            throw AppException.FromError(ManagerErrors.CannotLiquidateAgreement);

        var now = DateTimeOffset.UtcNow;
        var tz = TimeZoneHelper.ResolveTimeZone(agreement.Facility?.Timezone);
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, tz).DateTime);

        await using var tx = _dbContext.Database.IsSqlServer() && _dbContext.Database.CurrentTransaction == null
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var deductedDeposit = request.DeductDeposit ? Math.Max(0m, agreement.DepositBalance) : 0m;
            if (request.DeductDeposit)
            {
                agreement.DepositBalance = 0m;
            }

            var proceeds = Math.Max(0m, request.AuctionProceedsAmount ?? 0m);
            var availableRecovery = deductedDeposit + proceeds;

            var unpaidInvoices = agreement.Invoices
                .Where(i => i.Status is InvoiceStatusConstants.Open or InvoiceStatusConstants.PartiallyPaid or InvoiceStatusConstants.Overdue
                            && (i.TotalAmount - i.PaidAmount) > 0)
                .OrderBy(i => i.DueDate)
                .ThenBy(i => i.Id)
                .ToList();

            var totalDebtBefore = unpaidInvoices.Sum(i => Math.Max(0m, i.TotalAmount - i.PaidAmount));
            decimal settledAmount = 0m;

            foreach (var inv in unpaidInvoices)
            {
                var remaining = Math.Max(0m, inv.TotalAmount - inv.PaidAmount);
                if (remaining <= 0 || availableRecovery <= 0)
                    continue;

                var applied = Math.Min(remaining, availableRecovery);
                inv.PaidAmount += applied;
                inv.Status = inv.PaidAmount >= inv.TotalAmount
                    ? InvoiceStatusConstants.Paid
                    : InvoiceStatusConstants.PartiallyPaid;
                inv.UpdatedAt = now;

                availableRecovery -= applied;
                settledAmount += applied;
            }

            var remainingDebt = Math.Max(0m, totalDebtBefore - settledAmount);

            // Resolve open delinquency cases
            foreach (var dc in agreement.DelinquencyCases.Where(c => c.ResolvedAt == null))
            {
                dc.Status = remainingDebt == 0m ? DelinquencyCaseStatusConstants.Cured : DelinquencyCaseStatusConstants.Waived;
                dc.OutstandingSnapshot = 0m;
                dc.ResolvedAt = now;
                _dbContext.DelinquencyActions.Add(new DelinquencyAction
                {
                    DelinquencyCaseId = dc.Id,
                    ActionType = DelinquencyActionTypeConstants.TerminationStarted,
                    PerformedBy = currentUserId,
                    Details = JsonSerializer.Serialize(new
                    {
                        action = "liquidation",
                        reason = request.Reason.Trim(),
                        deductedDeposit,
                        auctionProceeds = proceeds,
                        settledAmount,
                        remainingDebt
                    }),
                    OccurredAt = now
                });
            }

            // Revoke all non-revoked access credentials
            var revokedCount = 0;
            foreach (var cred in agreement.AccessCredentials.Where(c => c.Status != CredentialStatusConstants.Revoked))
            {
                cred.Status = CredentialStatusConstants.Revoked;
                cred.RevokedAt = now;
                revokedCount++;
            }

            // Release active allocation and transition unit to pending_inspection
            var activeAlloc = agreement.UnitAllocations.FirstOrDefault(ua => ua.Status == AllocationStatusConstants.Active)
                              ?? agreement.UnitAllocations.OrderByDescending(ua => ua.Id).FirstOrDefault();
            var unit = activeAlloc?.StorageUnit;

            if (activeAlloc != null && activeAlloc.Status == AllocationStatusConstants.Active)
            {
                activeAlloc.Status = AllocationStatusConstants.Released;
                activeAlloc.EndedAt = now;
                if (localToday > activeAlloc.AllocationStartDate)
                    activeAlloc.AllocationEndDate = localToday;
                else if (activeAlloc.AllocationEndDate <= activeAlloc.AllocationStartDate)
                    activeAlloc.AllocationEndDate = activeAlloc.AllocationStartDate.AddDays(1);
            }

            if (unit != null)
            {
                unit.PhysicalStatus = StorageUnitStatusConstants.PendingInspection;
                unit.UpdatedAt = now;
            }

            agreement.Status = RentalAgreementStatusConstants.Terminated;
            agreement.ActualEndDate ??= localToday;
            agreement.UpdatedAt = now;

            // Ensure a MoveOutRequest in InspectionPending status exists so Manager can immediately assign Staff to inspect the unit
            var existingMoveOut = await _dbContext.MoveOutRequests
                .FirstOrDefaultAsync(m => m.AgreementId == agreement.Id, cancellationToken);
            if (existingMoveOut == null)
            {
                _dbContext.MoveOutRequests.Add(new MoveOutRequest
                {
                    AgreementId = agreement.Id,
                    RequestedBy = agreement.CustomerId,
                    RequestedMoveOutDate = localToday,
                    Status = MoveOutStatusConstants.InspectionPending,
                    Reason = $"Chờ nghiệm thu kho sau thanh lý hợp đồng #{agreement.AgreementNo}: {request.Reason.Trim()}",
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            else if (existingMoveOut.Status != MoveOutStatusConstants.Completed)
            {
                existingMoveOut.Status = MoveOutStatusConstants.InspectionPending;
                existingMoveOut.UpdatedAt = now;
            }

            _dbContext.AuditLogs.Add(new AuditLog
            {
                ActorUserId = currentUserId,
                ActorRole = "manager",
                EntitySchema = "core",
                Action = "agreement_liquidated",
                EntityType = "rental_agreement",
                EntityId = agreement.Id.ToString(),
                OldValues = "overdue/defaulted",
                NewValues = JsonSerializer.Serialize(new
                {
                    status = agreement.Status,
                    unitStatus = unit?.PhysicalStatus,
                    deductedDeposit,
                    auctionProceeds = proceeds,
                    settledAmount,
                    remainingDebt,
                    reason = request.Reason.Trim()
                }),
                OccurredAt = now
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            if (tx != null) await tx.CommitAsync(cancellationToken);

            return new ManagerLiquidateAgreementResultDto
            {
                AgreementId = agreement.Id,
                AgreementNo = agreement.AgreementNo,
                AgreementStatus = agreement.Status,
                StorageUnitId = unit?.Id ?? 0,
                UnitCode = unit?.UnitCode ?? string.Empty,
                UnitStatus = unit?.PhysicalStatus ?? StorageUnitStatusConstants.PendingInspection,
                RevokedCredentialsCount = revokedCount,
                DeductedDepositAmount = deductedDeposit,
                SettledDebtAmount = settledAmount,
                RemainingDebtAmount = remainingDebt,
                LiquidatedAt = now
            };
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
