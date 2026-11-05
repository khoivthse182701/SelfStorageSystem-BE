using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Rentals;
using SelfStorageSystem.Contracts.Manager;
using SelfStorageSystem.Contracts.Staff;
using SelfStorageSystem.Domain.Common;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public partial class FacilityManagerService : IFacilityManagerService
{
    // ==========================================

    // 4. Giám Sát Hợp Đồng & Xử Lý Quá Hạn Kéo Dài

    // ==========================================



    public async Task<PagedResult<ManagerAgreementSummaryDto>> GetAgreementsAsync(

        long facilityId,

        string? status,

        string? search,

        int pageNumber,

        int pageSize,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);



        pageNumber = pageNumber < 1 ? AdminConstants.DefaultPageNumber : pageNumber;

        pageSize = pageSize < 1 ? AdminConstants.DefaultPageSize 

            : Math.Min(pageSize, AdminConstants.MaxPageSize);



        var query = _dbContext.RentalAgreements

            .AsNoTracking()

            .Include(a => a.Customer)

                .ThenInclude(c => c.User)

            .Include(a => a.UnitAllocations)

                .ThenInclude(ua => ua.StorageUnit)

            .Include(a => a.Invoices)

            .Where(a => a.FacilityId == facilityId)

            .AsQueryable();



        if (!string.IsNullOrWhiteSpace(status))

        {

            var s = status.Trim().ToLowerInvariant();

            query = query.Where(a => a.Status.ToLower() == s);

        }



        if (!string.IsNullOrWhiteSpace(search))

        {

            var term = search.Trim().ToLowerInvariant();

            query = query.Where(a => a.AgreementNo.ToLower().Contains(term) ||

                                     a.Customer.FullName.ToLower().Contains(term) ||

                                     (a.Customer.User.PhoneNumber != null && a.Customer.User.PhoneNumber.Contains(term)) ||

                                     a.UnitAllocations.Any(ua => ua.StorageUnit.UnitCode.ToLower().Contains(term)));

        }



        var totalCount = await query.CountAsync(cancellationToken);



        var rawItems = await query

            .OrderByDescending(a => a.CreatedAt)

            .Skip((pageNumber - 1) * pageSize)

            .Take(pageSize)

            .ToListAsync(cancellationToken);



        var items = rawItems.Select(a =>

        {

            var activeAlloc = a.UnitAllocations.FirstOrDefault(u => u.Status == AllocationStatusConstants.Active)

                ?? a.UnitAllocations.OrderByDescending(u => u.Id).FirstOrDefault();



            return new ManagerAgreementSummaryDto

            {

                Id = a.Id,

                AgreementNo = a.AgreementNo,

                CustomerId = a.CustomerId,

                CustomerName = a.Customer.FullName,

                CustomerPhone = a.Customer.User.PhoneNumber,

                CustomerEmail = a.Customer.User.Email,

                StorageUnitId = activeAlloc?.StorageUnitId ?? 0,

                UnitCode = activeAlloc?.StorageUnit?.UnitCode ?? string.Empty,

                StartDate = a.StartDate,

                EndDate = a.EndDate,

                MonthlyRent = a.MonthlyRateSnapshot,

                DepositAmount = a.DepositSnapshot,

                Status = a.Status,

                OutstandingBalance = a.Invoices

                    .Where(i => i.Status != InvoiceStatusConstants.Paid && i.Status != InvoiceStatusConstants.Voided)

                    .Sum(i => i.TotalAmount - i.PaidAmount),

                UnpaidInvoicesCount = a.Invoices.Count(i => i.Status != InvoiceStatusConstants.Paid && i.Status != InvoiceStatusConstants.Voided),

                CreatedAt = a.CreatedAt

            };

        }).ToList();



        return new PagedResult<ManagerAgreementSummaryDto>(items, totalCount, pageNumber, pageSize);

    }



    public async Task<ManagerAgreementDetailDto> GetAgreementDetailAsync(

        long agreementId,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        var agreement = await _dbContext.RentalAgreements

            .AsNoTracking()

            .Include(a => a.Customer)

                .ThenInclude(c => c.User)

            .Include(a => a.UnitAllocations)

                .ThenInclude(ua => ua.StorageUnit)

            .Include(a => a.Invoices)

            .Include(a => a.StoredItems)

            .FirstOrDefaultAsync(a => a.Id == agreementId, cancellationToken);



        if (agreement == null)

        {

            throw AppException.FromError(ManagerErrors.AgreementNotFound);

        }



        await EnsureFacilityAccessAsync(agreement.FacilityId, currentUserId, isSystemAdmin, cancellationToken);



        var activeAlloc = agreement.UnitAllocations.FirstOrDefault(u => u.Status == AllocationStatusConstants.Active)

            ?? agreement.UnitAllocations.OrderByDescending(u => u.Id).FirstOrDefault();



        var totalDebt = agreement.Invoices

            .Where(i => i.Status != InvoiceStatusConstants.Paid && i.Status != InvoiceStatusConstants.Voided)

            .Sum(i => i.TotalAmount - i.PaidAmount);



        var invoices = agreement.Invoices

            .OrderByDescending(i => i.IssueDate)

            .Select(i => new ManagerAgreementInvoiceItemDto

            {

                Id = i.Id,

                InvoiceNo = i.InvoiceNo,

                IssueDate = i.IssueDate,

                DueDate = i.DueDate,

                TotalAmount = i.TotalAmount,

                PaidAmount = i.PaidAmount,

                Status = i.Status

            })

            .ToList();



        var storedItems = agreement.StoredItems

            .OrderBy(s => s.ItemName)

            .Select(s => new StoredItemDto(

                s.Id,

                s.AgreementId,

                s.ItemName,

                s.Category,

                s.Description,

                s.Quantity,

                s.EstimatedValue,

                s.RiskClassification,

                s.PhotoUrl,

                s.DeclaredAt,

                s.UpdatedAt,

                s.LocationNote

            ))

            .ToList();



        return new ManagerAgreementDetailDto

        {

            Id = agreement.Id,

            AgreementNo = agreement.AgreementNo,

            CustomerId = agreement.CustomerId,

            CustomerName = agreement.Customer.FullName,

            CustomerPhone = agreement.Customer.User.PhoneNumber,

            CustomerEmail = agreement.Customer.User.Email,

            StorageUnitId = activeAlloc?.StorageUnitId ?? 0,

            UnitCode = activeAlloc?.StorageUnit?.UnitCode ?? string.Empty,

            StartDate = agreement.StartDate,

            EndDate = agreement.EndDate,

            MonthlyRent = agreement.MonthlyRateSnapshot,

            DepositAmount = agreement.DepositSnapshot,

            Status = agreement.Status,

            TotalDebt = totalDebt,

            Invoices = invoices,

            StoredItems = storedItems

        };

    }



    public async Task<ManagerReassignUnitResponseDto> ReassignAgreementUnitAsync(

        long agreementId,

        ManagerReassignUnitRequest request,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        var agreement = await _dbContext.RentalAgreements

            .Include(a => a.Customer)

                .ThenInclude(c => c.User)

            .Include(a => a.UnitAllocations.Where(ua => ua.Status == AllocationStatusConstants.Active))

                .ThenInclude(ua => ua.StorageUnit)

                    .ThenInclude(u => u.UnitType)

            .FirstOrDefaultAsync(a => a.Id == agreementId, cancellationToken);



        if (agreement == null)

            throw AppException.FromError(ManagerErrors.AgreementNotFound);



        await EnsureFacilityAccessAsync(agreement.FacilityId, currentUserId, isSystemAdmin, cancellationToken);



        if (agreement.Status != RentalAgreementStatusConstants.Active &&

            agreement.Status != RentalAgreementStatusConstants.ExpiringSoon)

        {

            throw AppException.FromError(ManagerErrors.AgreementNotActive);

        }



        var activeAlloc = agreement.UnitAllocations.FirstOrDefault(ua => ua.Status == AllocationStatusConstants.Active);

        if (activeAlloc == null || activeAlloc.StorageUnit == null)

        {

            throw AppException.FromError(ManagerErrors.UnitNotFound);

        }



        var oldUnit = activeAlloc.StorageUnit;



        var newUnit = await _dbContext.StorageUnits

            .Include(u => u.UnitType)

            .FirstOrDefaultAsync(u => u.Id == request.NewStorageUnitId, cancellationToken);



        if (newUnit == null)

            throw AppException.FromError(ManagerErrors.UnitNotFound);



        if (newUnit.FacilityId != agreement.FacilityId)

            throw AppException.FromError(ManagerErrors.TargetUnitDifferentFacility);



        if (!string.Equals(newUnit.PhysicalStatus, StorageUnitStatusConstants.Available, StringComparison.OrdinalIgnoreCase))

            throw AppException.FromError(ManagerErrors.TargetUnitNotAvailable);



        var now = DateTimeOffset.UtcNow;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);



        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);



        // 1. Release old allocation and set old unit status

        activeAlloc.Status = AllocationStatusConstants.Released;

        activeAlloc.EndedAt = now;

        if (today > activeAlloc.AllocationStartDate)

        {

            activeAlloc.AllocationEndDate = today;

        }

        else if (activeAlloc.AllocationEndDate <= activeAlloc.AllocationStartDate)

        {

            activeAlloc.AllocationEndDate = activeAlloc.AllocationStartDate.AddDays(1);

        }



        var oldUnitNewStatus = StorageUnitStatusConstants.Maintenance;



        if (oldUnit.PhysicalStatus != StorageUnitStatusConstants.Maintenance &&

            oldUnit.PhysicalStatus != StorageUnitStatusConstants.UnderMaintenanceValue)

        {

            var previousOldUnitStatus = oldUnit.PhysicalStatus;

            oldUnit.PhysicalStatus = oldUnitNewStatus;

            oldUnit.UpdatedAt = now;



            _dbContext.UnitStatusHistories.Add(new UnitStatusHistory

            {

                StorageUnitId = oldUnit.Id,

                OldStatus = previousOldUnitStatus,

                NewStatus = oldUnitNewStatus,

                Reason = $"Manager unit re-assignment for agreement #{agreement.AgreementNo}: {request.Reason}",

                ChangedBy = currentUserId,

                ChangedAt = now

            });

        }

        else

        {

            oldUnit.UpdatedAt = now;

        }



        // Persist the release of the old allocation first inside the transaction

        await _dbContext.SaveChangesAsync(cancellationToken);



        // 2. Occupy new unit and allocate to agreement

        var previousNewUnitStatus = newUnit.PhysicalStatus;

        newUnit.PhysicalStatus = StorageUnitStatusConstants.Occupied;

        newUnit.UpdatedAt = now;



        _dbContext.UnitStatusHistories.Add(new UnitStatusHistory

        {

            StorageUnitId = newUnit.Id,

            OldStatus = previousNewUnitStatus,

            NewStatus = StorageUnitStatusConstants.Occupied,

            Reason = $"Manager unit re-assignment from unit {oldUnit.UnitCode} for agreement #{agreement.AgreementNo}",

            ChangedBy = currentUserId,

            ChangedAt = now

        });



        var newAllocStartDate = today;

        var newAllocEndDate = agreement.EndDate > newAllocStartDate ? agreement.EndDate : newAllocStartDate.AddDays(1);



        _dbContext.UnitAllocations.Add(new UnitAllocation

        {

            StorageUnitId = newUnit.Id,

            AgreementId = agreement.Id,

            AllocationKind = AllocationKindConstants.Rental,

            AllocationStartDate = newAllocStartDate,

            AllocationEndDate = newAllocEndDate,

            Status = AllocationStatusConstants.Active,

            CreatedAt = now,

            EndedAt = null

        });



        // 3. Issue fresh 6-digit PIN credential for new unit

        var randomPin = Random.Shared.Next(100000, 999999).ToString("D6");

        var pinCred = await _dbContext.AccessCredentials

            .FirstOrDefaultAsync(c => c.AgreementId == agreement.Id && c.CredentialType == CredentialTypeConstants.Pin, cancellationToken);



        if (pinCred != null)

        {

            pinCred.SecretDigest = BCrypt.Net.BCrypt.HashPassword(randomPin);

            pinCred.DisplayHint = PinProtectionHelper.Encrypt(randomPin);

            pinCred.IssuedAt = now;

            pinCred.Status = CredentialStatusConstants.Active;

        }

        else

        {

            _dbContext.AccessCredentials.Add(new AccessCredential

            {

                AgreementId = agreement.Id,

                CredentialType = CredentialTypeConstants.Pin,

                SecretDigest = BCrypt.Net.BCrypt.HashPassword(randomPin),

                DisplayHint = PinProtectionHelper.Encrypt(randomPin),

                IssuedAt = now,

                ExpiresAt = now.AddYears(1),

                Status = CredentialStatusConstants.Active,

                CreatedAt = now

            });

        }



        if (request.TicketId.HasValue && request.TicketId.Value > 0)

        {

            var ticket = await _dbContext.SupportTickets.FirstOrDefaultAsync(t => t.Id == request.TicketId.Value, cancellationToken);

            if (ticket != null)

            {

                ticket.Status = TicketStatusConstants.Resolved;

                ticket.ResolvedAt = now;

                ticket.UpdatedAt = now;

            }

        }



        agreement.UpdatedAt = now;



        await _dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);



        _logger.LogInformation("Manager {UserId} reassigned agreement #{AgreementNo} from unit {OldUnit} to unit {NewUnit}. Reason: {Reason}",

            currentUserId, agreement.AgreementNo, oldUnit.UnitCode, newUnit.UnitCode, request.Reason);



        return new ManagerReassignUnitResponseDto

        {

            AgreementId = agreement.Id,

            AgreementNo = agreement.AgreementNo,

            CustomerId = agreement.CustomerId,

            CustomerName = agreement.Customer.FullName,

            FacilityId = agreement.FacilityId,

            OldStorageUnitId = oldUnit.Id,

            OldUnitCode = oldUnit.UnitCode,

            OldUnitStatus = oldUnit.PhysicalStatus,

            NewStorageUnitId = newUnit.Id,

            NewUnitCode = newUnit.UnitCode,

            NewUnitStatus = newUnit.PhysicalStatus,

            NewUnitTypeName = newUnit.UnitType.Name,

            NewPinCode = randomPin,

            Reason = request.Reason,

            ReassignedAt = now,

            Message = $"Customer successfully reassigned from unit {oldUnit.UnitCode} to equivalent unit {newUnit.UnitCode}. New PIN: {randomPin}."

        };

    }



    public async Task<ManagerEmergencyCancelRefundResponseDto> EmergencyCancelAndRefundAgreementAsync(

        long agreementId,

        ManagerEmergencyCancelRefundRequest request,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        var agreement = await _dbContext.RentalAgreements

            .Include(a => a.Customer)

                .ThenInclude(c => c.User)

            .Include(a => a.UnitAllocations.Where(ua => ua.Status == AllocationStatusConstants.Active))

                .ThenInclude(ua => ua.StorageUnit)

            .FirstOrDefaultAsync(a => a.Id == agreementId, cancellationToken);



        if (agreement == null)

            throw AppException.FromError(ManagerErrors.AgreementNotFound);



        await EnsureFacilityAccessAsync(agreement.FacilityId, currentUserId, isSystemAdmin, cancellationToken);



        var activeAlloc = agreement.UnitAllocations.FirstOrDefault(ua => ua.Status == AllocationStatusConstants.Active);

        var oldUnit = activeAlloc?.StorageUnit ?? await _dbContext.StorageUnits.FirstOrDefaultAsync(u => u.Id == (activeAlloc != null ? activeAlloc.StorageUnitId : 0), cancellationToken);



        var now = DateTimeOffset.UtcNow;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);



        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);



        // 1. Release active allocation

        if (activeAlloc != null)

        {

            activeAlloc.Status = AllocationStatusConstants.Released;

            activeAlloc.EndedAt = now;

            if (today > activeAlloc.AllocationStartDate)

            {

                activeAlloc.AllocationEndDate = today;

            }

            else if (activeAlloc.AllocationEndDate <= activeAlloc.AllocationStartDate)

            {

                activeAlloc.AllocationEndDate = activeAlloc.AllocationStartDate.AddDays(1);

            }

        }



        // 2. Put unit into maintenance with lock notes

        if (oldUnit != null)

        {

            if (oldUnit.PhysicalStatus != StorageUnitStatusConstants.Maintenance &&

                oldUnit.PhysicalStatus != StorageUnitStatusConstants.UnderMaintenanceValue)

            {

                var previousStatus = oldUnit.PhysicalStatus;

                oldUnit.PhysicalStatus = StorageUnitStatusConstants.Maintenance;

                _dbContext.UnitStatusHistories.Add(new UnitStatusHistory

                {

                    StorageUnitId = oldUnit.Id,

                    OldStatus = previousStatus,

                    NewStatus = StorageUnitStatusConstants.Maintenance,

                    Reason = $"Emergency cancel & full refund: {request.Reason}",

                    ChangedBy = currentUserId,

                    ChangedAt = now

                });

            }

            var cancelNote = $"[LOCKED_BY:{currentUserId}:Manager] Sự cố Check-in - Hết kho thay thế: {request.Reason}".Trim();

            if (cancelNote.Length > 250) cancelNote = cancelNote.Substring(0, 250);

            oldUnit.Notes = cancelNote;

            oldUnit.UpdatedAt = now;

        }



        // 3. Mark agreement cancelled

        agreement.Status = RentalAgreementStatusConstants.Cancelled;

        agreement.ActualEndDate = today;

        agreement.UpdatedAt = now;



        // 4. Revoke active access credentials

        var credentials = await _dbContext.AccessCredentials

            .Where(c => c.AgreementId == agreement.Id && c.Status == CredentialStatusConstants.Active)

            .ToListAsync(cancellationToken);

        foreach (var cred in credentials)

        {

            cred.Status = CredentialStatusConstants.Revoked;

            cred.RevokedAt = now;

        }



        // 5. Resolve ticket if provided

        if (request.TicketId.HasValue && request.TicketId.Value > 0)

        {

            var ticket = await _dbContext.SupportTickets.FirstOrDefaultAsync(t => t.Id == request.TicketId.Value, cancellationToken);

            if (ticket != null)

            {

                ticket.Status = TicketStatusConstants.Resolved;

                ticket.Resolution = $"Đã xử lý: Hủy hợp đồng #{agreement.AgreementNo} và phê duyệt hoàn tiền 100% cho khách do cơ sở không còn kho thay thế ({request.Reason}).";

                ticket.ResolvedAt = now;

                ticket.UpdatedAt = now;

            }

        }



        // 6. Calculate total paid amount and create refund record satisfying trg_refunds_guard

        var validPayment = await _dbContext.Payments

            .Include(p => p.TargetInvoice)

            .FirstOrDefaultAsync(p => p.Status == PaymentConstants.StatusSucceeded &&

                ((p.TargetInvoice != null && p.TargetInvoice.AgreementId == agreement.Id) ||

                 (agreement.ReservationId > 0 && p.TargetInvoice != null && p.TargetInvoice.ReservationId == agreement.ReservationId)), cancellationToken);



        decimal refundAmount = 0m;

        if (validPayment != null)

        {

            refundAmount = validPayment.Amount;

        }

        else

        {

            var deposit = agreement.DepositSnapshot > 0 ? agreement.DepositSnapshot : agreement.DepositBalance;

            var rent = agreement.MonthlyRateSnapshot;

            refundAmount = Math.Max(0m, deposit + rent);

        }



        long resolvedPaymentId = validPayment?.Id ?? 0;

        if (resolvedPaymentId == 0)

        {

            var depInvoice = new Invoice

            {

                InvoiceNo = $"INV-CANC-{agreement.Id}-{now.ToUnixTimeSeconds()}",

                AgreementId = agreement.Id,

                CustomerId = agreement.CustomerId,

                BillingPeriod = $"REF-{now:yyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",

                IssueDate = today,

                DueDate = today,

                Currency = PaymentConstants.CurrencyVnd,

                SubtotalAmount = refundAmount,

                TaxAmount = 0m,

                TotalAmount = refundAmount,

                PaidAmount = refundAmount,

                Status = InvoiceStatusConstants.Paid,

                CreatedAt = now,

                UpdatedAt = now

            };

            _dbContext.Invoices.Add(depInvoice);

            await _dbContext.SaveChangesAsync(cancellationToken);



            var payment = new Payment

            {

                CustomerId = agreement.CustomerId,

                TargetInvoiceId = depInvoice.Id,

                Amount = refundAmount,

                Currency = PaymentConstants.CurrencyVnd,

                Method = PaymentConstants.MethodBankTransfer,

                Provider = PaymentConstants.ProviderManual,

                IdempotencyKey = $"PAY-CANC-{agreement.Id}-{now.ToUnixTimeSeconds()}",

                Status = PaymentConstants.StatusSucceeded,

                PaidAt = now,

                Metadata = "{}",

                CreatedAt = now,

                UpdatedAt = now

            };

            _dbContext.Payments.Add(payment);

            await _dbContext.SaveChangesAsync(cancellationToken);

            resolvedPaymentId = payment.Id;

        }



        var refund = new Refund

        {

            PaymentId = resolvedPaymentId,

            AgreementId = agreement.Id,

            Amount = refundAmount,

            Currency = PaymentConstants.CurrencyVnd,

            Reason = $"Hủy HĐ #{agreement.AgreementNo} & hoàn tiền 100% do sự cố kho và cơ sở hết kho thay thế: {request.Reason}",

            Provider = PaymentConstants.ProviderManual,

            IdempotencyKey = $"REFUND-EMERGENCY-CANCEL-{agreement.Id}-{now.ToUnixTimeSeconds()}",

            Status = RefundStatusConstants.Approved,

            RequestedBy = currentUserId,

            CreatedAt = now,

            UpdatedAt = now

        };

        _dbContext.Refunds.Add(refund);

        await _dbContext.SaveChangesAsync(cancellationToken);



        var empProfile = await _dbContext.EmployeeProfiles.FirstOrDefaultAsync(ep => ep.UserId == currentUserId, cancellationToken);

        long decidedByEmpId = empProfile != null ? currentUserId : (await _dbContext.EmployeeProfiles.Select(ep => ep.UserId).FirstOrDefaultAsync(cancellationToken));



        var refundApproval = new RefundApproval

        {

            RefundId = refund.Id,

            Decision = "approved",

            DecidedBy = decidedByEmpId,

            Reason = System.Text.Json.JsonSerializer.Serialize(new

            {

                decision = "approved",

                emergencyCancel = true,

                reason = request.Reason,

                notes = request.Notes

            }),

            DecidedAt = now

        };

        _dbContext.RefundApprovals.Add(refundApproval);



        // 7. Send in-app notification to customer

        var customerUserId = agreement.Customer?.UserId;

        if (customerUserId.HasValue)

        {

            _dbContext.Notifications.Add(new Notification

            {

                UserId = customerUserId.Value,

                Channel = "in_app",

                TemplateCode = "AGREEMENT_CANCELLED_FULL_REFUND",

                Subject = $"[Hủy hợp đồng & Hoàn tiền 100%] HĐ #{agreement.AgreementNo}",

                Payload = System.Text.Json.JsonSerializer.Serialize(new

                {

                    agreementId = agreement.Id,

                    agreementNo = agreement.AgreementNo,

                    amount = refundAmount,

                    message = $"Hợp đồng #{agreement.AgreementNo} đã được hủy do cơ sở không còn kho thay thế sự cố. Khoản tiền {refundAmount:N0} VND đã được duyệt hoàn 100% cho bạn."

                }),

                Status = "pending",

                ScheduledAt = now,

                CreatedAt = now

            });

        }



        await _dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);



        _logger.LogInformation("Manager {UserId} emergency-cancelled agreement #{AgreementNo} with 100% refund {Amount} VND due to no replacement units. Unit {UnitCode} put into maintenance.",

            currentUserId, agreement.AgreementNo, refundAmount, oldUnit?.UnitCode);



        return new ManagerEmergencyCancelRefundResponseDto

        {

            AgreementId = agreement.Id,

            AgreementNo = agreement.AgreementNo,

            CustomerId = agreement.CustomerId,

            CustomerName = agreement.Customer?.FullName ?? "Khách hàng",

            StorageUnitId = oldUnit?.Id ?? 0,

            UnitCode = oldUnit?.UnitCode ?? "—",

            TotalRefundAmount = refundAmount,

            RefundId = refund.Id,

            AgreementStatus = agreement.Status,

            UnitStatus = oldUnit?.PhysicalStatus ?? StorageUnitStatusConstants.Maintenance,

            CancelledAt = now,

            Message = $"Đã hủy hợp đồng #{agreement.AgreementNo} và phê duyệt hoàn trả 100% ({refundAmount:N0} VNĐ) cho khách hàng thành công."

        };

    }



    public async Task<IReadOnlyList<ManagerDelinquencyCaseDto>> GetDelinquentCasesAsync(

        long facilityId,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);

        var nowSync = DateTimeOffset.UtcNow;

        var todaySync = DateOnly.FromDateTime(DateTime.UtcNow);



        // Auto-sync delinquent cases for overdue agreements with unpaid invoices or past-due end date

        var existingAgreementIds = await _dbContext.DelinquencyCases

            .Where(dc => dc.Agreement.FacilityId == facilityId)

            .Select(dc => dc.AgreementId)

            .ToListAsync(cancellationToken);



        var candidateAgreements = await _dbContext.RentalAgreements

            .Include(a => a.Invoices)

            .Where(a => a.FacilityId == facilityId &&

                        (a.Status == RentalAgreementStatusConstants.Active || a.Status == RentalAgreementStatusConstants.ExpiringSoon || a.Status == RentalAgreementStatusConstants.Overdue) &&

                        !existingAgreementIds.Contains(a.Id))

            .ToListAsync(cancellationToken);



        bool hasNewCases = false;

        foreach (var agr in candidateAgreements)

        {

            var unpaid = agr.Invoices

                .Where(i => i.DueDate < todaySync && i.Status != InvoiceStatusConstants.Paid && i.Status != InvoiceStatusConstants.Voided && (i.TotalAmount - i.PaidAmount) > 0)

                .ToList();

            var isPastEndDate = agr.EndDate < todaySync;

            if (unpaid.Count > 0 || isPastEndDate)

            {

                var debt = unpaid.Sum(i => i.TotalAmount - i.PaidAmount);

                if (debt <= 0 && isPastEndDate)

                {

                    debt = agr.MonthlyRateSnapshot;

                }

                var earliestDueDate = unpaid.OrderBy(i => i.DueDate).Select(i => (DateOnly?)i.DueDate).FirstOrDefault() ?? agr.EndDate;

                var graceEnd = new DateTimeOffset(earliestDueDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(7);



                var targetInvoiceId = unpaid.OrderBy(i => i.DueDate).Select(i => i.Id).FirstOrDefault();
                if (targetInvoiceId == 0)
                {
                    targetInvoiceId = agr.Invoices.OrderByDescending(i => i.Id).Select(i => i.Id).FirstOrDefault();
                }
                if (targetInvoiceId > 0)
                {
                    _dbContext.DelinquencyCases.Add(new DelinquencyCase
                    {
                        AgreementId = agr.Id,
                        InvoiceId = targetInvoiceId,
                        OutstandingSnapshot = debt,
                        GraceEndsAt = graceEnd,
                        Status = "open",
                        OpenedAt = nowSync,
                        Notes = $"Tự động mở hồ sơ công nợ quá hạn do nợ chưa thanh toán hoặc hợp đồng đã kết thúc."
                    });
                    hasNewCases = true;
                }

            }

        }

        if (hasNewCases)

        {

            await _dbContext.SaveChangesAsync(cancellationToken);

        }



        var cases = await _dbContext.DelinquencyCases

            .AsNoTracking()

            .Include(d => d.Agreement)

                .ThenInclude(a => a.Customer)

                    .ThenInclude(c => c.User)

            .Include(d => d.Agreement)

                .ThenInclude(a => a.UnitAllocations)

                    .ThenInclude(ua => ua.StorageUnit)

            .Include(d => d.DelinquencyActions)

                .ThenInclude(da => da.PerformedByNavigation)

            .Where(d => d.Agreement.FacilityId == facilityId)

            .OrderByDescending(d => d.OpenedAt)

            .ToListAsync(cancellationToken);



        var now = DateTimeOffset.UtcNow;

        var result = cases.Select(d =>

        {

            var overdueDays = (int)(now - d.GraceEndsAt).TotalDays;

            var activeAlloc = d.Agreement.UnitAllocations.FirstOrDefault(u => u.Status == AllocationStatusConstants.Active)

                ?? d.Agreement.UnitAllocations.OrderByDescending(u => u.Id).FirstOrDefault();



            return new ManagerDelinquencyCaseDto

            {

                Id = d.Id,

                AgreementId = d.AgreementId,

                AgreementNo = d.Agreement.AgreementNo,

                UnitCode = activeAlloc?.StorageUnit?.UnitCode ?? string.Empty,

                CustomerId = d.Agreement.CustomerId,

                CustomerName = d.Agreement.Customer.FullName,

                CustomerPhone = d.Agreement.Customer.User.PhoneNumber,

                Status = d.Status,

                OutstandingSnapshot = d.OutstandingSnapshot,

                CurrentDebt = d.OutstandingSnapshot,

                OverdueDays = Math.Max(0, overdueDays),

                OpenedAt = d.OpenedAt,

                GraceEndsAt = d.GraceEndsAt,

                ResolvedAt = d.ResolvedAt,

                Notes = d.Notes,

                Actions = d.DelinquencyActions

                    .OrderByDescending(a => a.OccurredAt)

                    .Select(a => new ManagerDelinquencyActionDto

                    {

                        Id = a.Id,

                        ActionType = a.ActionType,

                        PerformedBy = a.PerformedBy,

                        PerformedByName = a.PerformedByNavigation?.Email,

                        Details = a.Details,

                        OccurredAt = a.OccurredAt

                    })

                    .ToList()

            };

        }).ToList();



        return result;

    }



    public async Task<ManagerDelinquencyActionDto> AddDelinquencyActionAsync(

        long caseId,

        ManagerAddDelinquencyActionRequest request,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        var dCase = await _dbContext.DelinquencyCases

            .Include(d => d.Agreement)

            .FirstOrDefaultAsync(d => d.Id == caseId, cancellationToken);



        if (dCase == null)

        {

            throw AppException.FromError(ManagerErrors.DelinquencyCaseNotFound);

        }



        await EnsureFacilityAccessAsync(dCase.Agreement.FacilityId, currentUserId, isSystemAdmin, cancellationToken);



        var actionType = request.ActionType.Trim().ToLowerInvariant();

        if (!DelinquencyActionTypeConstants.All.Contains(actionType))

        {

            throw AppException.FromError(ManagerErrors.InvalidActionType);

        }



        var detailsObj = new { note = request.Note ?? string.Empty, registeredBy = currentUserId };

        var jsonDetails = JsonSerializer.Serialize(detailsObj);



        var now = DateTimeOffset.UtcNow;

        var action = new DelinquencyAction

        {

            DelinquencyCaseId = dCase.Id,

            ActionType = actionType,

            PerformedBy = currentUserId,

            Details = jsonDetails,

            OccurredAt = now

        };



        _dbContext.DelinquencyActions.Add(action);



        if (!string.IsNullOrWhiteSpace(request.UpdateCaseStatus))

        {

            var newStatus = request.UpdateCaseStatus.Trim().ToLowerInvariant();

            if (DelinquencyCaseStatusConstants.All.Contains(newStatus))

            {

                dCase.Status = newStatus;

                if (newStatus == DelinquencyCaseStatusConstants.Cured || newStatus == DelinquencyCaseStatusConstants.Waived)

                {

                    dCase.ResolvedAt = now;

                }

            }

        }



        await _dbContext.SaveChangesAsync(cancellationToken);



        return new ManagerDelinquencyActionDto

        {

            Id = action.Id,

            ActionType = action.ActionType,

            PerformedBy = currentUserId,

            Details = action.Details,

            OccurredAt = action.OccurredAt

        };

    }
}
