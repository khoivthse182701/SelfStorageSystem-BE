using Microsoft.EntityFrameworkCore;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Staff;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public class FacilityStaffService : IFacilityStaffService
{
    private readonly SelfStorageDbContext _dbContext;

    public FacilityStaffService(SelfStorageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // ==========================================
    // 1. DAILY STAFF TASKS
    // ==========================================

    public async Task<IReadOnlyList<StaffTaskDto>> GetStaffTasksAsync(
        long employeeUserId,
        long? facilityId = null,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);

        var query = _dbContext.StaffTasks
            .AsNoTracking()
            .Include(t => t.Facility)
            .Include(t => t.AssignedEmployee)
            .AsQueryable();

        if (facilityId.HasValue)
        {
            query = query.Where(t => t.FacilityId == facilityId.Value);
        }
        else
        {
            // Default to tasks assigned to this employee or in employee's assigned facilities
            var now = DateTimeOffset.UtcNow;
            var assignedFacilityIds = await _dbContext.StaffFacilityAssignments
                .Where(s => s.EmployeeId == employee.UserId && (!s.EndsAt.HasValue || s.EndsAt > now))
                .Select(s => s.FacilityId)
                .ToListAsync(cancellationToken);

            query = query.Where(t => t.AssignedEmployeeId == employee.UserId || 
                                     (assignedFacilityIds.Contains(t.FacilityId)));
        }

        var tasks = await query.OrderByDescending(t => t.CreatedAt).ToListAsync(cancellationToken);

        return tasks.Select(t => new StaffTaskDto(
            Id: t.Id,
            FacilityId: t.FacilityId,
            FacilityCode: t.Facility.Code,
            AssignedEmployeeId: t.AssignedEmployeeId,
            AssignedEmployeeName: t.AssignedEmployee?.FullName,
            TaskType: t.TaskType,
            Title: t.Title,
            DueAt: t.DueAt,
            Status: t.Status,
            ProgressPercent: t.ProgressPercent,
            CreatedAt: t.CreatedAt,
            UpdatedAt: t.UpdatedAt
        )).ToList();
    }

    public async Task<StaffTaskDto> UpdateStaffTaskStatusAsync(
        long employeeUserId,
        long taskId,
        UpdateStaffTaskStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);

        var task = await _dbContext.StaffTasks
            .Include(t => t.Facility)
            .Include(t => t.AssignedEmployee)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task == null)
            throw AppException.FromError(StaffErrors.TaskNotFound);

        task.Status = request.Status.ToLowerInvariant();
        if (request.ProgressPercent.HasValue)
        {
            task.ProgressPercent = request.ProgressPercent.Value;
        }
        task.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new StaffTaskDto(
            Id: task.Id,
            FacilityId: task.FacilityId,
            FacilityCode: task.Facility.Code,
            AssignedEmployeeId: task.AssignedEmployeeId,
            AssignedEmployeeName: task.AssignedEmployee?.FullName,
            TaskType: task.TaskType,
            Title: task.Title,
            DueAt: task.DueAt,
            Status: task.Status,
            ProgressPercent: task.ProgressPercent,
            CreatedAt: task.CreatedAt,
            UpdatedAt: task.UpdatedAt
        );
    }

    // ==========================================
    // 2. CHECK-IN & HANDOVER
    // ==========================================

    public async Task<IReadOnlyList<StaffReservationLookupDto>> LookupReservationsAsync(
        string? query,
        long? facilityId = null,
        CancellationToken cancellationToken = default)
    {
        var reservationsQuery = _dbContext.Reservations
            .AsNoTracking()
            .Include(r => r.Customer)
                .ThenInclude(c => c.User)
            .Include(r => r.Facility)
            .Include(r => r.UnitType)
            .Include(r => r.UnitAllocation)
                .ThenInclude(u => u!.StorageUnit)
            .AsQueryable();

        if (facilityId.HasValue)
        {
            reservationsQuery = reservationsQuery.Where(r => r.FacilityId == facilityId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var trimmed = query.Trim().ToLower();
            reservationsQuery = reservationsQuery.Where(r =>
                r.ReservationCode.ToLower().Contains(trimmed) ||
                (r.Customer.User.PhoneNumber != null && r.Customer.User.PhoneNumber.Contains(trimmed)) ||
                r.Customer.FullName.ToLower().Contains(trimmed));
        }

        var reservations = await reservationsQuery
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        return reservations.Select(r => new StaffReservationLookupDto(
            ReservationId: r.Id,
            ReservationCode: r.ReservationCode,
            CustomerId: r.CustomerId,
            CustomerName: r.Customer.FullName,
            CustomerPhone: r.Customer.User.PhoneNumber,
            FacilityId: r.FacilityId,
            FacilityCode: r.Facility.Code,
            UnitTypeId: r.UnitTypeId,
            UnitTypeName: r.UnitType.Name,
            AssignedUnitId: r.UnitAllocation?.StorageUnitId,
            AssignedUnitCode: r.UnitAllocation?.StorageUnit?.UnitCode,
            StartDate: r.StartDate,
            EndDate: r.EndDate,
            Status: r.Status,
            QuotedTotal: r.QuotedTotal,
            DepositSnapshot: r.DepositSnapshot,
            CreatedAt: r.CreatedAt
        )).ToList();
    }

    public async Task AssignUnitToReservationAsync(
        long employeeUserId,
        long reservationId,
        AssignUnitToReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);

        var reservation = await _dbContext.Reservations
            .Include(r => r.UnitAllocation)
            .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);

        if (reservation == null)
            throw AppException.FromError(StaffErrors.ReservationNotFound);

        if (reservation.Status != ReservationStatusConstants.Confirmed &&
            reservation.Status != ReservationStatusConstants.Pending &&
            reservation.Status != ReservationStatusConstants.AwaitingDeposit)
        {
            throw AppException.FromError(StaffErrors.ReservationNotConfirmed);
        }

        var unit = await _dbContext.StorageUnits
            .FirstOrDefaultAsync(u => u.Id == request.StorageUnitId && u.FacilityId == reservation.FacilityId, cancellationToken);

        if (unit == null)
            throw AppException.FromError(StaffErrors.UnitNotFound);

        if (unit.UnitTypeId != reservation.UnitTypeId)
            throw AppException.FromError(StaffErrors.UnitTypeMismatch);

        // Check if unit is available
        var isAllocated = await _dbContext.UnitAllocations
            .AnyAsync(ua => ua.StorageUnitId == unit.Id &&
                            (ua.Status == AllocationStatusConstants.Active) &&
                            ua.ReservationId != reservation.Id, cancellationToken);

        if (isAllocated || unit.PhysicalStatus != StorageUnitStatusConstants.Available)
            throw AppException.FromError(StaffErrors.UnitNotAvailable);

        if (reservation.UnitAllocation != null)
        {
            reservation.UnitAllocation.StorageUnitId = unit.Id;
            reservation.UnitAllocation.Status = AllocationStatusConstants.Active;
            reservation.UnitAllocation.AssignedBy = employee.UserId;
        }
        else
        {
            var allocation = new UnitAllocation
            {
                StorageUnitId = unit.Id,
                ReservationId = reservation.Id,
                AllocationKind = AllocationKindConstants.ReservationHold,
                AllocationStartDate = reservation.StartDate,
                AllocationEndDate = reservation.EndDate,
                Status = AllocationStatusConstants.Active,
                AssignedBy = employee.UserId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _dbContext.UnitAllocations.Add(allocation);
        }

        unit.PhysicalStatus = StorageUnitStatusConstants.Reserved;
        unit.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<StaffHandoverResultDto> CreateHandoverAsync(
        long employeeUserId,
        CreateStaffHandoverRequest request,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);

        var reservation = await _dbContext.Reservations
            .Include(r => r.Customer)
            .Include(r => r.RentalAgreement)
            .Include(r => r.UnitAllocation)
                .ThenInclude(u => u!.StorageUnit)
            .FirstOrDefaultAsync(r => r.Id == request.ReservationId, cancellationToken);

        if (reservation == null)
            throw AppException.FromError(StaffErrors.ReservationNotFound);

        if (reservation.UnitAllocation == null)
            throw AppException.FromError(StaffErrors.UnitNotFound);

        var unit = reservation.UnitAllocation.StorageUnit;
        var agreement = reservation.RentalAgreement;

        // If RentalAgreement doesn't exist yet, activate it from reservation
        var now = DateTimeOffset.UtcNow;
        if (agreement == null)
        {
            var agreementNo = $"AGR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
            agreement = new RentalAgreement
            {
                AgreementNo = agreementNo,
                CustomerId = reservation.CustomerId,
                FacilityId = reservation.FacilityId,
                ReservationId = reservation.Id,
                PolicyVersionId = 1, // Default baseline policy
                StartDate = reservation.StartDate,
                EndDate = reservation.EndDate,
                MonthlyRateSnapshot = reservation.MonthlyRateSnapshot,
                DepositSnapshot = reservation.DepositSnapshot,
                DepositBalance = reservation.DepositSnapshot,
                Status = RentalAgreementStatusConstants.Active,
                SignedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.RentalAgreements.Add(agreement);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            agreement.Status = RentalAgreementStatusConstants.Active;
            agreement.UpdatedAt = now;
        }

        // Link unit allocation to agreement
        reservation.UnitAllocation.AgreementId = agreement.Id;
        reservation.UnitAllocation.AllocationKind = AllocationKindConstants.Rental;
        reservation.UnitAllocation.Status = AllocationStatusConstants.Active;

        // Create inspection record
        var inspection = new Inspection
        {
            FacilityId = reservation.FacilityId,
            StorageUnitId = unit.Id,
            ReservationId = reservation.Id,
            AgreementId = agreement.Id,
            InspectedBy = employee.UserId,
            InspectionType = request.HandoverType ?? HandoverTypeConstants.CheckIn,
            Status = "passed",
            OverallCondition = request.OverallCondition ?? "Good / Ready for Move-In",
            Summary = request.InspectionSummary ?? "Check-in inspection completed by staff.",
            InspectedAt = now,
            CreatedAt = now
        };

        if (request.InspectionItems != null && request.InspectionItems.Any())
        {
            foreach (var it in request.InspectionItems)
            {
                inspection.InspectionItems.Add(new InspectionItem
                {
                    ItemName = it.ItemName,
                    Condition = it.Condition,
                    Notes = it.Notes,
                    PhotoUrl = it.PhotoUrl,
                    ChargeAmount = it.ChargeAmount
                });
            }
        }
        _dbContext.Inspections.Add(inspection);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Create HandoverRecord
        var handover = new HandoverRecord
        {
            AgreementId = agreement.Id,
            UnitAllocationId = reservation.UnitAllocation.Id,
            InspectionId = inspection.Id,
            HandledBy = employee.UserId,
            HandoverType = request.HandoverType ?? HandoverTypeConstants.CheckIn,
            CustomerSignatureRef = request.CustomerSignatureRef,
            StaffSignatureRef = request.StaffSignatureRef,
            CustomerSignedAt = string.IsNullOrEmpty(request.CustomerSignatureRef) ? null : now,
            StaffSignedAt = now,
            Notes = request.Notes,
            CreatedAt = now
        };
        _dbContext.HandoverRecords.Add(handover);

        // Update unit and reservation status
        unit.PhysicalStatus = StorageUnitStatusConstants.Occupied;
        unit.UpdatedAt = now;

        reservation.Status = ReservationStatusConstants.Converted;
        reservation.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new StaffHandoverResultDto(
            HandoverId: handover.Id,
            AgreementId: agreement.Id,
            AgreementNo: agreement.AgreementNo,
            ReservationId: reservation.Id,
            StorageUnitId: unit.Id,
            UnitCode: unit.UnitCode,
            HandoverType: handover.HandoverType,
            AgreementStatus: agreement.Status,
            HandoverTime: handover.CreatedAt
        );
    }

    // ==========================================
    // 3. MOVE-OUT & INSPECTION
    // ==========================================

    public async Task<IReadOnlyList<StaffMoveOutSummaryDto>> GetMoveOutsAsync(
        long? facilityId = null,
        DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.MoveOutRequests
            .AsNoTracking()
            .Include(m => m.Agreement)
                .ThenInclude(a => a.Customer)
            .Include(m => m.Agreement)
                .ThenInclude(a => a.UnitAllocations)
                    .ThenInclude(u => u.StorageUnit)
            .AsQueryable();

        if (facilityId.HasValue)
        {
            query = query.Where(m => m.Agreement.FacilityId == facilityId.Value);
        }

        if (date.HasValue)
        {
            query = query.Where(m => m.RequestedMoveOutDate == date.Value);
        }

        var list = await query.OrderByDescending(m => m.CreatedAt).ToListAsync(cancellationToken);

        return list.Select(m =>
        {
            var unitCode = m.Agreement.UnitAllocations
                .FirstOrDefault(u => u.Status == AllocationStatusConstants.Active)?.StorageUnit?.UnitCode ?? "N/A";

            return new StaffMoveOutSummaryDto(
                Id: m.Id,
                AgreementId: m.AgreementId,
                AgreementNo: m.Agreement.AgreementNo,
                CustomerId: m.RequestedBy,
                CustomerName: m.Agreement.Customer?.FullName ?? "Customer",
                FacilityId: m.Agreement.FacilityId,
                UnitCode: unitCode,
                RequestedMoveOutDate: m.RequestedMoveOutDate,
                Status: m.Status,
                Reason: m.Reason,
                CreatedAt: m.CreatedAt
            );
        }).ToList();
    }

    public async Task<InspectMoveOutResultDto> InspectMoveOutAsync(
        long employeeUserId,
        long moveOutId,
        InspectMoveOutRequest request,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);

        var moveOut = await _dbContext.MoveOutRequests
            .Include(m => m.Agreement)
                .ThenInclude(a => a.UnitAllocations)
                    .ThenInclude(u => u.StorageUnit)
            .FirstOrDefaultAsync(m => m.Id == moveOutId, cancellationToken);

        if (moveOut == null)
            throw AppException.FromError(StaffErrors.MoveOutNotFound);

        var agreement = moveOut.Agreement;
        var activeAllocation = agreement.UnitAllocations.FirstOrDefault(u => u.Status == AllocationStatusConstants.Active);
        var unit = activeAllocation?.StorageUnit;

        var now = DateTimeOffset.UtcNow;

        // Create inspection
        var inspection = new Inspection
        {
            FacilityId = agreement.FacilityId,
            StorageUnitId = unit?.Id,
            AgreementId = agreement.Id,
            InspectedBy = employee.UserId,
            InspectionType = HandoverTypeConstants.CheckOut,
            Status = "completed",
            OverallCondition = request.OverallCondition,
            Summary = request.Summary ?? "Check-out return inspection performed by staff.",
            InspectedAt = now,
            CreatedAt = now
        };

        decimal totalDamageCharges = 0m;
        if (request.InspectionItems != null && request.InspectionItems.Any())
        {
            foreach (var it in request.InspectionItems)
            {
                inspection.InspectionItems.Add(new InspectionItem
                {
                    ItemName = it.ItemName,
                    Condition = it.Condition,
                    Notes = it.Notes,
                    PhotoUrl = it.PhotoUrl,
                    ChargeAmount = it.ChargeAmount
                });
                totalDamageCharges += it.ChargeAmount;
            }
        }
        _dbContext.Inspections.Add(inspection);

        // Deduct damages from agreement deposit
        agreement.DepositBalance = Math.Max(0m, agreement.DepositBalance - totalDamageCharges);
        agreement.Status = RentalAgreementStatusConstants.Terminated;
        agreement.UpdatedAt = now;

        if (activeAllocation != null)
        {
            activeAllocation.Status = AllocationStatusConstants.Released;
            activeAllocation.EndedAt = now;
        }

        if (unit != null)
        {
            unit.PhysicalStatus = request.NextUnitStatus == StorageUnitStatusConstants.UnderMaintenance
                ? StorageUnitStatusConstants.UnderMaintenance
                : StorageUnitStatusConstants.Available;
            unit.UpdatedAt = now;
        }

        moveOut.Status = MoveOutStatusConstants.Completed;
        moveOut.FinalizedAt = now;
        moveOut.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new InspectMoveOutResultDto(
            MoveOutId: moveOut.Id,
            InspectionId: inspection.Id,
            AgreementId: agreement.Id,
            TotalDamageCharges: totalDamageCharges,
            AgreementStatus: agreement.Status,
            UnitStatus: unit?.PhysicalStatus ?? StorageUnitStatusConstants.Available,
            InspectedAt: now
        );
    }

    // ==========================================
    // 4. SUPPORT TICKETS
    // ==========================================

    public async Task<IReadOnlyList<StaffSupportTicketDto>> GetFacilityTicketsAsync(
        long? facilityId = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SupportTickets
            .AsNoTracking()
            .Include(t => t.Facility)
            .Include(t => t.Customer)
            .Include(t => t.Agreement)
            .Include(t => t.StorageUnit)
            .AsQueryable();

        if (facilityId.HasValue)
        {
            query = query.Where(t => t.FacilityId == facilityId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status == status);
        }

        var tickets = await query.OrderByDescending(t => t.CreatedAt).ToListAsync(cancellationToken);

        return tickets.Select(t => new StaffSupportTicketDto(
            Id: t.Id,
            TicketNo: t.TicketNo,
            FacilityId: t.FacilityId,
            FacilityCode: t.Facility.Code,
            CustomerId: t.CustomerId,
            CustomerName: t.Customer.FullName,
            AgreementId: t.AgreementId,
            AgreementNo: t.Agreement?.AgreementNo,
            StorageUnitId: t.StorageUnitId,
            UnitCode: t.StorageUnit?.UnitCode,
            Category: t.Category,
            Priority: t.Priority,
            Subject: t.Subject,
            Description: t.Description,
            Status: t.Status,
            CreatedAt: t.CreatedAt,
            UpdatedAt: t.UpdatedAt
        )).ToList();
    }

    public async Task AssignTicketToMeAsync(
        long employeeUserId,
        long ticketId,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);

        var ticket = await _dbContext.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket == null)
            throw AppException.FromError(TicketErrors.TicketNotFound);

        var assignment = new TicketAssignment
        {
            TicketId = ticket.Id,
            EmployeeId = employee.UserId,
            AssignedBy = employee.UserId,
            AssignedAt = DateTimeOffset.UtcNow
        };
        _dbContext.TicketAssignments.Add(assignment);

        ticket.Status = TicketStatusConstants.InProgress;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<StaffTicketMessageDto> AddStaffTicketMessageAsync(
        long employeeUserId,
        long ticketId,
        AddStaffTicketMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);

        var ticket = await _dbContext.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket == null)
            throw AppException.FromError(TicketErrors.TicketNotFound);

        if (string.IsNullOrWhiteSpace(request.Body))
            throw AppException.FromError(TicketErrors.EmptyMessage);

        var message = new TicketMessage
        {
            TicketId = ticket.Id,
            AuthorUserId = employee.UserId,
            Body = request.Body.Trim(),
            IsInternal = request.IsInternal,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _dbContext.TicketMessages.Add(message);

        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        if (!request.IsInternal && ticket.Status == TicketStatusConstants.InProgress)
        {
            ticket.Status = TicketStatusConstants.WaitingForCustomer;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new StaffTicketMessageDto(
            Id: message.Id,
            TicketId: message.TicketId,
            AuthorUserId: message.AuthorUserId,
            AuthorName: employee.FullName,
            AuthorRole: RoleConstants.StaffDisplay,
            Body: message.Body,
            IsInternal: message.IsInternal,
            CreatedAt: message.CreatedAt
        );
    }

    public async Task<TicketChargeProposalDto> ProposeTicketChargeAsync(
        long employeeUserId,
        long ticketId,
        ProposeTicketChargeRequest request,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);

        var ticket = await _dbContext.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket == null)
            throw AppException.FromError(TicketErrors.TicketNotFound);

        var proposal = new TicketChargeProposal
        {
            TicketId = ticket.Id,
            ProposedBy = employee.UserId,
            Description = request.Description.Trim(),
            Amount = request.Amount,
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.TicketChargeProposals.Add(proposal);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TicketChargeProposalDto(
            Id: proposal.Id,
            TicketId: proposal.TicketId,
            ProposedBy: proposal.ProposedBy,
            ProposedByName: employee.FullName,
            Description: proposal.Description,
            Amount: proposal.Amount,
            Status: proposal.Status,
            CreatedAt: proposal.CreatedAt
        );
    }

    public async Task ResolveTicketAsync(
        long employeeUserId,
        long ticketId,
        ResolveTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);

        var ticket = await _dbContext.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket == null)
            throw AppException.FromError(TicketErrors.TicketNotFound);

        ticket.Status = TicketStatusConstants.Resolved;
        ticket.Resolution = request.Resolution.Trim();
        ticket.ResolvedAt = DateTimeOffset.UtcNow;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // ==========================================
    // 5. UNIT MANAGEMENT & MAINTENANCE
    // ==========================================

    public async Task<IReadOnlyList<StaffFacilityUnitDto>> GetFacilityUnitsAsync(
        long facilityId,
        CancellationToken cancellationToken = default)
    {
        var units = await _dbContext.StorageUnits
            .AsNoTracking()
            .Include(u => u.UnitType)
            .Include(u => u.UnitAllocations.Where(a => a.Status == AllocationStatusConstants.Active))
                .ThenInclude(a => a.Agreement)
                    .ThenInclude(ag => ag!.Customer)
            .Where(u => u.FacilityId == facilityId)
            .OrderBy(u => u.UnitCode)
            .ToListAsync(cancellationToken);

        return units.Select(u =>
        {
            var activeAlloc = u.UnitAllocations.FirstOrDefault();
            return new StaffFacilityUnitDto(
                UnitId: u.Id,
                UnitCode: u.UnitCode,
                UnitTypeId: u.UnitTypeId,
                UnitTypeName: u.UnitType.Name,
                Status: u.PhysicalStatus,
                CurrentRate: activeAlloc?.Agreement?.MonthlyRateSnapshot,
                CurrentAgreementNo: activeAlloc?.Agreement?.AgreementNo,
                CustomerName: activeAlloc?.Agreement?.Customer?.FullName
            );
        }).ToList();
    }

    public async Task UpdateUnitStatusAsync(
        long employeeUserId,
        long unitId,
        UpdateUnitStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        await GetEmployeeProfileAsync(employeeUserId, cancellationToken);

        var unit = await _dbContext.StorageUnits
            .FirstOrDefaultAsync(u => u.Id == unitId, cancellationToken);

        if (unit == null)
            throw AppException.FromError(StaffErrors.UnitNotFound);

        unit.PhysicalStatus = request.Status.ToLowerInvariant();
        unit.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<MaintenanceWorkOrderDto> CreateMaintenanceOrderAsync(
        long employeeUserId,
        long unitId,
        CreateMaintenanceWorkOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);

        var unit = await _dbContext.StorageUnits
            .FirstOrDefaultAsync(u => u.Id == unitId, cancellationToken);

        if (unit == null)
            throw AppException.FromError(StaffErrors.UnitNotFound);

        var workOrderNo = $"MWO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";

        var order = new MaintenanceWorkOrder
        {
            WorkOrderNo = workOrderNo,
            FacilityId = unit.FacilityId,
            StorageUnitId = unit.Id,
            OpenedBy = employee.UserId,
            AssignedEmployeeId = employee.UserId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Priority = request.Priority.ToLowerInvariant(),
            BlocksBooking = request.BlocksBooking,
            Status = "open",
            EstimatedCost = request.EstimatedCost,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (request.BlocksBooking)
        {
            unit.PhysicalStatus = StorageUnitStatusConstants.UnderMaintenance;
            unit.UpdatedAt = DateTimeOffset.UtcNow;
        }

        _dbContext.MaintenanceWorkOrders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new MaintenanceWorkOrderDto(
            Id: order.Id,
            WorkOrderNo: order.WorkOrderNo,
            FacilityId: order.FacilityId,
            StorageUnitId: order.StorageUnitId,
            UnitCode: unit.UnitCode,
            Title: order.Title,
            Description: order.Description,
            Priority: order.Priority,
            BlocksBooking: order.BlocksBooking,
            Status: order.Status,
            EstimatedCost: order.EstimatedCost,
            CreatedAt: order.CreatedAt
        );
    }

    // ==========================================
    // 6. OVERDUE ENFORCEMENT
    // ==========================================

    public async Task<IReadOnlyList<OverdueAgreementDto>> GetOverdueAgreementsAsync(
        long? facilityId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.Agreement)
                .ThenInclude(a => a!.Customer)
                    .ThenInclude(c => c.User)
            .Include(i => i.Agreement)
                .ThenInclude(a => a!.UnitAllocations)
                    .ThenInclude(u => u.StorageUnit)
            .Include(i => i.Agreement)
                .ThenInclude(a => a!.AccessCredentials)
            .Where(i => i.Status == InvoiceStatusConstants.Overdue && i.AgreementId != null)
            .AsQueryable();

        if (facilityId.HasValue)
        {
            query = query.Where(i => i.Agreement!.FacilityId == facilityId.Value);
        }

        var overdueInvoices = await query.ToListAsync(cancellationToken);

        var grouped = overdueInvoices
            .GroupBy(i => i.Agreement!)
            .Select(g =>
            {
                var ag = g.Key;
                var unitCode = ag.UnitAllocations.FirstOrDefault(u => u.Status == AllocationStatusConstants.Active)?.StorageUnit?.UnitCode ?? "N/A";
                var storageUnitId = ag.UnitAllocations.FirstOrDefault(u => u.Status == AllocationStatusConstants.Active)?.StorageUnitId ?? 0;
                var totalBalance = g.Sum(inv => inv.TotalAmount - inv.PaidAmount);
                var credStatus = ag.AccessCredentials.FirstOrDefault(c => c.CredentialType == CredentialTypeConstants.Pin)?.Status ?? "none";

                return new OverdueAgreementDto(
                    AgreementId: ag.Id,
                    AgreementNo: ag.AgreementNo,
                    CustomerId: ag.CustomerId,
                    CustomerName: ag.Customer?.FullName ?? "Customer",
                    CustomerPhone: ag.Customer?.User.PhoneNumber,
                    StorageUnitId: storageUnitId,
                    UnitCode: unitCode,
                    OutstandingBalance: totalBalance,
                    StartDate: ag.StartDate,
                    EndDate: ag.EndDate,
                    AgreementStatus: ag.Status,
                    CredentialStatus: credStatus
                );
            }).ToList();

        return grouped;
    }

    public async Task LockAgreementAccessAsync(
        long employeeUserId,
        long agreementId,
        LockAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        await GetEmployeeProfileAsync(employeeUserId, cancellationToken);

        var credentials = await _dbContext.AccessCredentials
            .Where(c => c.AgreementId == agreementId)
            .ToListAsync(cancellationToken);

        foreach (var cred in credentials)
        {
            cred.Status = CredentialStatusConstants.Suspended;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // ==========================================
    // HELPERS
    // ==========================================

    private async Task<EmployeeProfile> GetEmployeeProfileAsync(long employeeUserId, CancellationToken cancellationToken)
    {
        var employee = await _dbContext.EmployeeProfiles
            .FirstOrDefaultAsync(e => e.UserId == employeeUserId, cancellationToken);

        if (employee == null)
            throw AppException.FromError(StaffErrors.EmployeeProfileNotFound);

        return employee;
    }
}
