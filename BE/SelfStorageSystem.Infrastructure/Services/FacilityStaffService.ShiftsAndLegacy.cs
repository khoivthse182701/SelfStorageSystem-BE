using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Staff;
using SelfStorageSystem.Domain.Common;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public partial class FacilityStaffService : IFacilityStaffService
{
    // ==========================================



    // 🗓️ NHÓM 5: CA TRỰC & LỊCH LÀM VIỆC (Shifts Schedule)



    // ==========================================







    public async Task<IReadOnlyList<StaffShiftScheduleDto>> GetStaffShiftsAsync(



        long employeeUserId,



        long? facilityId = null,



        DateTimeOffset? fromDate = null,



        DateTimeOffset? toDate = null,



        CancellationToken cancellationToken = default)



    {



        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);







        var query = _dbContext.ShiftAssignments



            .AsNoTracking()



            .Include(sa => sa.Shift)



                .ThenInclude(s => s.Facility)



            .Include(sa => sa.Employee)



            .Where(sa => sa.EmployeeId == employee.UserId)



            .AsQueryable();







        if (facilityId.HasValue)



        {



            query = query.Where(sa => sa.Shift.FacilityId == facilityId.Value);



        }







        if (fromDate.HasValue)



        {



            query = query.Where(sa => sa.Shift.EndsAt >= fromDate.Value);



        }







        if (toDate.HasValue)



        {



            query = query.Where(sa => sa.Shift.StartsAt <= toDate.Value);



        }







        var assignments = await query



            .OrderBy(sa => sa.Shift.StartsAt)



            .ToListAsync(cancellationToken);







        return assignments.Select(sa => new StaffShiftScheduleDto(



            ShiftId: sa.ShiftId,



            FacilityId: sa.Shift.FacilityId,



            FacilityName: sa.Shift.Facility.Name,



            ShiftName: sa.Shift.ShiftName,



            StartsAt: sa.Shift.StartsAt,



            EndsAt: sa.Shift.EndsAt,



            EmployeeId: sa.EmployeeId,



            EmployeeCode: sa.Employee.EmployeeCode,



            EmployeeName: sa.Employee.FullName,



            DutyRole: sa.DutyRole,



            Status: sa.Status,



            CheckInAt: sa.CheckInAt,



            CheckOutAt: sa.CheckOutAt



        )).ToList();



    }







    public async Task<StaffShiftScheduleDto> CheckInShiftAsync(long employeeUserId, long shiftId, CancellationToken cancellationToken = default)



    {



        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);



        var assignment = await _dbContext.ShiftAssignments



            .Include(sa => sa.Shift).ThenInclude(s => s.Facility)



            .Include(sa => sa.Employee)



            .FirstOrDefaultAsync(sa => sa.ShiftId == shiftId && sa.EmployeeId == employee.UserId, cancellationToken);







        if (assignment == null)



            throw AppException.FromError(StaffErrors.ShiftAssignmentNotFound);







        assignment.CheckInAt = DateTimeOffset.UtcNow;



        assignment.Status = "checked_in";



        await _dbContext.SaveChangesAsync(cancellationToken);







        return new StaffShiftScheduleDto(



            assignment.ShiftId, assignment.Shift.FacilityId, assignment.Shift.Facility.Name, assignment.Shift.ShiftName,



            assignment.Shift.StartsAt, assignment.Shift.EndsAt, assignment.EmployeeId, assignment.Employee.EmployeeCode,



            assignment.Employee.FullName, assignment.DutyRole, assignment.Status, assignment.CheckInAt, assignment.CheckOutAt



        );



    }







    public async Task<StaffShiftScheduleDto> CheckOutShiftAsync(long employeeUserId, long shiftId, CancellationToken cancellationToken = default)



    {



        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);



        var assignment = await _dbContext.ShiftAssignments



            .Include(sa => sa.Shift).ThenInclude(s => s.Facility)



            .Include(sa => sa.Employee)



            .FirstOrDefaultAsync(sa => sa.ShiftId == shiftId && sa.EmployeeId == employee.UserId, cancellationToken);







        if (assignment == null)



            throw AppException.FromError(StaffErrors.ShiftAssignmentNotFound);







        assignment.CheckOutAt = DateTimeOffset.UtcNow;



        assignment.Status = ShiftAssignmentStatusConstants.CheckedOut;



        await _dbContext.SaveChangesAsync(cancellationToken);







        return new StaffShiftScheduleDto(



            assignment.ShiftId, assignment.Shift.FacilityId, assignment.Shift.Facility.Name, assignment.Shift.ShiftName,



            assignment.Shift.StartsAt, assignment.Shift.EndsAt, assignment.EmployeeId, assignment.Employee.EmployeeCode,



            assignment.Employee.FullName, assignment.DutyRole, assignment.Status, assignment.CheckInAt, assignment.CheckOutAt



        );



    }







    // ==========================================



    // BACKWARD-COMPATIBILITY / LEGACY METHODS



    // ==========================================







    public async Task<IReadOnlyList<StaffTaskDto>> GetStaffTasksAsync(long employeeUserId, long? facilityId = null, CancellationToken cancellationToken = default)



    {



        var employee = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);



        var query = _dbContext.StaffTasks.AsNoTracking().Include(t => t.Facility).Include(t => t.AssignedEmployee).AsQueryable();



        if (facilityId.HasValue) query = query.Where(t => t.FacilityId == facilityId.Value);



        else query = query.Where(t => t.AssignedEmployeeId == employee.UserId);







        var tasks = await query.OrderByDescending(t => t.CreatedAt).ToListAsync(cancellationToken);



        return tasks.Select(t => new StaffTaskDto(t.Id, t.FacilityId, t.Facility.Code, t.AssignedEmployeeId, t.AssignedEmployee?.FullName, t.TaskType, t.Title, t.DueAt, t.Status, t.ProgressPercent, t.CreatedAt, t.UpdatedAt)).ToList();



    }







    public async Task<StaffTaskDto> UpdateStaffTaskStatusAsync(long employeeUserId, long taskId, UpdateStaffTaskStatusRequest request, CancellationToken cancellationToken = default)



    {



        var task = await _dbContext.StaffTasks.Include(t => t.Facility).Include(t => t.AssignedEmployee).FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);



        if (task == null) throw AppException.FromError(StaffErrors.TaskNotFound);



        task.Status = request.Status;



        if (request.ProgressPercent.HasValue) task.ProgressPercent = request.ProgressPercent.Value;



        task.UpdatedAt = DateTimeOffset.UtcNow;



        await _dbContext.SaveChangesAsync(cancellationToken);



        return new StaffTaskDto(task.Id, task.FacilityId, task.Facility.Code, task.AssignedEmployeeId, task.AssignedEmployee?.FullName, task.TaskType, task.Title, task.DueAt, task.Status, task.ProgressPercent, task.CreatedAt, task.UpdatedAt);



    }







    public async Task<IReadOnlyList<StaffReservationLookupDto>> LookupReservationsAsync(string? query, long? facilityId = null, CancellationToken cancellationToken = default)



    {



        var q = _dbContext.Reservations.AsNoTracking().Include(r => r.Customer).Include(r => r.Facility).Include(r => r.UnitType).Include(r => r.UnitAllocation).ThenInclude(a => a!.StorageUnit).AsQueryable();



        if (facilityId.HasValue) q = q.Where(r => r.FacilityId == facilityId.Value);



        if (!string.IsNullOrWhiteSpace(query))



        {



            var term = query.Trim().ToLowerInvariant();



            q = q.Where(r => r.ReservationCode.ToLower().Contains(term) || r.Customer.FullName.ToLower().Contains(term));



        }



        var list = await q.OrderByDescending(r => r.CreatedAt).Take(50).ToListAsync(cancellationToken);



        return list.Select(r => new StaffReservationLookupDto(r.Id, r.ReservationCode, r.CustomerId, r.Customer.FullName, null, r.FacilityId, r.Facility.Code, r.UnitTypeId, r.UnitType.Name, r.UnitAllocation?.StorageUnitId, r.UnitAllocation?.StorageUnit?.UnitCode, r.StartDate, r.EndDate, r.Status, r.QuotedTotal, r.DepositSnapshot, r.CreatedAt)).ToList();



    }







    public async Task AssignUnitToReservationAsync(long employeeUserId, long reservationId, AssignUnitToReservationRequest request, CancellationToken cancellationToken = default)



    {



        var reservation = await _dbContext.Reservations.Include(r => r.UnitAllocation).FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);



        if (reservation == null) throw AppException.FromError(StaffErrors.ReservationNotFound);



        var unit = await _dbContext.StorageUnits.FirstOrDefaultAsync(u => u.Id == request.StorageUnitId, cancellationToken);



        if (unit == null) throw AppException.FromError(StaffErrors.UnitNotFound);



        if (!string.Equals(unit.PhysicalStatus, StorageUnitStatusConstants.Available, StringComparison.OrdinalIgnoreCase))



            throw AppException.FromError(StaffErrors.UnitNotAvailable);







        if (reservation.UnitAllocation != null)



        {



            var oldUnit = await _dbContext.StorageUnits.FindAsync(reservation.UnitAllocation.StorageUnitId);



            if (oldUnit != null) oldUnit.PhysicalStatus = StorageUnitStatusConstants.Available;



            reservation.UnitAllocation.StorageUnitId = unit.Id;



        }



        else



        {



            _dbContext.UnitAllocations.Add(new UnitAllocation



            {



                StorageUnitId = unit.Id,



                ReservationId = reservation.Id,



                AllocationKind = AllocationKindConstants.Reservation,



                AllocationStartDate = reservation.StartDate,



                AllocationEndDate = reservation.EndDate,



                Status = AllocationStatusConstants.Active,



                CreatedAt = DateTimeOffset.UtcNow



            });



        }



        unit.PhysicalStatus = StorageUnitStatusConstants.Reserved;



        await _dbContext.SaveChangesAsync(cancellationToken);



    }







    public async Task<StaffHandoverResultDto> CreateHandoverAsync(long employeeUserId, CreateStaffHandoverRequest request, CancellationToken cancellationToken = default)

    {

        var rawHandoverType = request.HandoverType?.Trim().ToLowerInvariant() ?? string.Empty;

        var normalizedHandoverType = (rawHandoverType == "move_in" || string.IsNullOrWhiteSpace(rawHandoverType))

            ? HandoverTypeConstants.CheckIn

            : rawHandoverType;



        var res = await ConfirmQrActionAsync(employeeUserId, new StaffConfirmQrActionRequest(StaffActionConstants.CheckIn, ReservationId: request.ReservationId, Notes: request.Notes), cancellationToken);

        var agrId = res.AgreementId ?? 0;

        var rsv = await _dbContext.Reservations.Include(r => r.RentalAgreement).Include(r => r.UnitAllocation).ThenInclude(a => a!.StorageUnit).FirstOrDefaultAsync(r => r.Id == request.ReservationId, cancellationToken);

        if (agrId == 0) agrId = rsv?.RentalAgreement?.Id ?? 0;



        var handover = await _dbContext.HandoverRecords

            .Include(h => h.Agreement)

            .Include(h => h.UnitAllocation).ThenInclude(ua => ua.StorageUnit)

            .FirstOrDefaultAsync(h => h.AgreementId == agrId && h.HandoverType == normalizedHandoverType, cancellationToken);



        var unitId = handover?.UnitAllocation?.StorageUnitId ?? rsv?.UnitAllocation?.StorageUnitId ?? res.UnitId ?? 0;

        var unitCode = handover?.UnitAllocation?.StorageUnit?.UnitCode ?? rsv?.UnitAllocation?.StorageUnit?.UnitCode ?? string.Empty;

        var agrNo = handover?.Agreement?.AgreementNo ?? rsv?.RentalAgreement?.AgreementNo ?? string.Empty;



        return new StaffHandoverResultDto(

            handover?.Id ?? 0,

            agrId,

            agrNo,

            request.ReservationId,

            unitId,

            unitCode,

            normalizedHandoverType,

            RentalAgreementStatusConstants.Active,

            DateTimeOffset.UtcNow);

    }







    public async Task<IReadOnlyList<StaffMoveOutSummaryDto>> GetMoveOutsAsync(long? facilityId = null, DateOnly? date = null, CancellationToken cancellationToken = default)



    {



        var q = _dbContext.MoveOutRequests.AsNoTracking()



            .Include(m => m.Agreement)



                .ThenInclude(a => a.Customer)



            .Include(m => m.Agreement)



                .ThenInclude(a => a.Facility)



            .Include(m => m.Agreement)



                .ThenInclude(a => a.UnitAllocations)



                    .ThenInclude(ua => ua.StorageUnit)



            .AsQueryable();







        if (facilityId.HasValue) q = q.Where(m => m.Agreement.FacilityId == facilityId.Value);



        if (date.HasValue) q = q.Where(m => m.RequestedMoveOutDate == date.Value);



        var list = await q.OrderByDescending(m => m.RequestedMoveOutDate).ToListAsync(cancellationToken);







        return list.Select(m =>



        {



            var alloc = m.Agreement.UnitAllocations



                .OrderByDescending(ua => ua.Status == AllocationStatusConstants.Active ? 1 : 0)



                .ThenByDescending(ua => ua.Id)



                .FirstOrDefault();



            var unitCode = alloc?.StorageUnit?.UnitCode ?? "N/A";







            return new StaffMoveOutSummaryDto(



                m.Id,



                m.AgreementId,



                m.Agreement.AgreementNo,



                m.Agreement.CustomerId,



                m.Agreement.Customer.FullName,



                m.Agreement.FacilityId,



                unitCode,



                m.RequestedMoveOutDate,



                m.Status,



                m.Reason,



                m.CreatedAt



            );



        }).ToList();



    }







    public async Task<InspectMoveOutResultDto> InspectMoveOutAsync(long employeeUserId, long moveOutId, InspectMoveOutRequest request, CancellationToken cancellationToken = default)



    {



        var res = await ConfirmQrActionAsync(employeeUserId, new StaffConfirmQrActionRequest("move_out", MoveOutId: moveOutId), cancellationToken);



        return new InspectMoveOutResultDto(moveOutId, 1, res.AgreementId ?? 1, 0m, RentalAgreementStatusConstants.Completed, StorageUnitStatusConstants.Cleaning, DateTimeOffset.UtcNow);



    }







    public async Task<IReadOnlyList<StaffSupportTicketDto>> GetFacilityTicketsAsync(long? facilityId = null, string? status = null, CancellationToken cancellationToken = default)



    {



        var q = _dbContext.SupportTickets.AsNoTracking().Include(t => t.Facility).Include(t => t.Customer).Include(t => t.StorageUnit).Include(t => t.Agreement).AsQueryable();



        if (facilityId.HasValue) q = q.Where(t => t.FacilityId == facilityId.Value);



        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(t => t.Status == status);



        var list = await q.OrderByDescending(t => t.CreatedAt).ToListAsync(cancellationToken);



        return list.Select(t => new StaffSupportTicketDto(t.Id, t.TicketNo, t.FacilityId, t.Facility.Code, t.CustomerId, t.Customer.FullName, t.AgreementId, t.Agreement?.AgreementNo, t.StorageUnitId, t.StorageUnit?.UnitCode, t.Category, t.Priority, t.Subject, t.Description, t.Status, t.CreatedAt, t.UpdatedAt)).ToList();



    }







    public Task AssignTicketToMeAsync(long employeeUserId, long ticketId, CancellationToken cancellationToken = default)



        => AssignTicketToStaffAsync(employeeUserId, ticketId, cancellationToken);







    public Task<IReadOnlyList<StaffTicketMessageDto>> GetTicketMessagesAsync(long ticketId, CancellationToken cancellationToken = default)



        => GetTicketMessagesAsync(0, ticketId, cancellationToken);







    public async Task<IReadOnlyList<StaffTicketMessageDto>> GetTicketMessagesAsync(long employeeUserId, long ticketId, CancellationToken cancellationToken = default)



    {



        var ticket = await _dbContext.SupportTickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);



        if (ticket == null)



            throw AppException.FromError(StaffErrors.TicketNotFound);







        var now = DateTimeOffset.UtcNow;



        if (employeeUserId > 0)



        {



            var isAssigned = await _dbContext.StaffFacilityAssignments



                .AnyAsync(sfa => sfa.EmployeeId == employeeUserId && sfa.FacilityId == ticket.FacilityId && (sfa.EndsAt == null || sfa.EndsAt > now), cancellationToken);







            if (!isAssigned)



            {



                var isEmp = await _dbContext.EmployeeProfiles.AnyAsync(ep => ep.UserId == employeeUserId, cancellationToken);



                if (!isEmp)



                {



                    throw AppException.FromError(StaffErrors.StaffNotAssignedToFacility);



                }



            }



        }







        var messages = await _dbContext.TicketMessages



            .AsNoTracking()



            .Include(m => m.AuthorUser)



                .ThenInclude(u => u.EmployeeProfile)



            .Include(m => m.AuthorUser)



                .ThenInclude(u => u.CustomerProfile)



            .Include(m => m.AuthorUser)



                .ThenInclude(u => u.UserRoleUsers)



                    .ThenInclude(ur => ur.Role)



            .Where(m => m.TicketId == ticketId)



            .OrderBy(m => m.CreatedAt)



            .ToListAsync(cancellationToken);







        return messages.Select(msg =>



        {



            var authorName = msg.AuthorUser?.EmployeeProfile?.FullName



                ?? msg.AuthorUser?.CustomerProfile?.FullName



                ?? msg.AuthorUser?.Email



                ?? "Hệ thống";







            var roleCode = msg.AuthorUser?.UserRoleUsers.FirstOrDefault()?.Role?.Code ?? "User";



            var roleDisplayName = roleCode switch



            {



                RoleConstants.FacilityStaff => "Nhân viên vận hành",



                RoleConstants.FacilityManager => "Quản lý cơ sở",



                RoleConstants.SystemAdministrator => "Quản trị viên",



                RoleConstants.Customer => "Khách hàng",



                _ => roleCode



            };







            return new StaffTicketMessageDto(



                msg.Id,



                ticketId,



                msg.AuthorUserId,



                authorName,



                roleDisplayName,



                msg.Body,



                msg.IsInternal,



                msg.CreatedAt



            );



        }).ToList();



    }







    public async Task<StaffTicketMessageDto> AddStaffTicketMessageAsync(long employeeUserId, long ticketId, AddStaffTicketMessageRequest request, CancellationToken cancellationToken = default)



    {



        var emp = await GetEmployeeProfileAsync(employeeUserId, cancellationToken);



        var ticket = await _dbContext.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);



        if (ticket == null)



            throw AppException.FromError(StaffErrors.TicketNotFound);







        var msg = new TicketMessage { TicketId = ticketId, AuthorUserId = employeeUserId, Body = request.Body, IsInternal = request.IsInternal, CreatedAt = DateTimeOffset.UtcNow };



        _dbContext.TicketMessages.Add(msg);



        await _dbContext.SaveChangesAsync(cancellationToken);



        return new StaffTicketMessageDto(msg.Id, ticketId, employeeUserId, emp.FullName, "Staff", request.Body, request.IsInternal, msg.CreatedAt);



    }







    public Task<TicketChargeProposalDto> ProposeTicketChargeAsync(long employeeUserId, long ticketId, ProposeTicketChargeRequest request, CancellationToken cancellationToken = default)



        => ProposeTicketChargeAsync(employeeUserId, ticketId, new StaffProposeChargeRequest(request.Amount, request.Description), cancellationToken);







    public async Task ResolveTicketAsync(long employeeUserId, long ticketId, ResolveTicketRequest request, CancellationToken cancellationToken = default)



        => await CompleteTicketAsync(employeeUserId, ticketId, new StaffCompleteTicketRequest(request.Resolution), cancellationToken);







    public async Task<IReadOnlyList<StaffFacilityUnitDto>> GetFacilityUnitsAsync(long facilityId, CancellationToken cancellationToken = default)



    {



        var units = await GetUnitsAsync(1, facilityId, cancellationToken: cancellationToken);



        return units.Select(u => new StaffFacilityUnitDto(u.UnitId, u.UnitCode, u.UnitTypeId, u.UnitTypeName, u.PhysicalStatus, u.CurrentRate, u.CurrentAgreementNo, u.CustomerName, u.Notes)).ToList();



    }







    public async Task UpdateUnitStatusAsync(long employeeUserId, long unitId, UpdateUnitStatusRequest request, CancellationToken cancellationToken = default)



    {



        var unit = await _dbContext.StorageUnits.FirstOrDefaultAsync(u => u.Id == unitId, cancellationToken);



        if (unit == null) throw AppException.FromError(StaffErrors.UnitNotFound);







        await ResolveFacilityIdAsync(employeeUserId, unit.FacilityId, cancellationToken);







        var targetStatus = !string.IsNullOrWhiteSpace(request.PhysicalStatus) ? request.PhysicalStatus : request.Status;



        if (!string.IsNullOrWhiteSpace(targetStatus))



        {



            var normalized = targetStatus.Trim().ToLowerInvariant();



            if (normalized == "under_inspection") normalized = StorageUnitStatusConstants.PendingInspection;



            if (normalized == "under_maintenance") normalized = StorageUnitStatusConstants.UnderMaintenanceValue;



            if (normalized == "damaged") normalized = StorageUnitStatusConstants.Maintenance;



            if (normalized == "vacant") normalized = StorageUnitStatusConstants.Available;







            // 1. Khóa khi kho đang có khách (Occupied / Reserved)



            if (unit.PhysicalStatus == StorageUnitStatusConstants.Occupied || unit.PhysicalStatus == StorageUnitStatusConstants.Reserved)



            {



                throw new AppException(Error.Validation("StorageUnit.Occupied", "Kho đang được thuê/giữ chỗ. Vui lòng xử lý qua luồng Check-in hoặc Move-out."));



            }







            // 2. Task Ownership Lock (Ai nhận việc người đó giữ khóa)



            var user = await _dbContext.Users



                .Include(u => u.EmployeeProfile)



                .Include(u => u.UserRoleUsers)



                    .ThenInclude(ur => ur.Role)



                .FirstOrDefaultAsync(u => u.Id == employeeUserId, cancellationToken);



            var isManager = user?.UserRoleUsers.Any(ur => ur.Role.Code == RoleConstants.FacilityManager || ur.Role.Code == RoleConstants.SystemAdministrator) ?? false;







            var existingNote = unit.Notes ?? string.Empty;



            long? lockedByUserId = null;



            string? lockedByName = null;



            if (existingNote.StartsWith("[LOCKED_BY:"))



            {



                var endBracket = existingNote.IndexOf(']');



                if (endBracket > 11)



                {



                    var inner = existingNote.Substring(11, endBracket - 11);



                    var colonIdx = inner.IndexOf(':');



                    var idStr = colonIdx >= 0 ? inner.Substring(0, colonIdx) : inner;



                    if (long.TryParse(idStr, out var parsedId))



                    {



                        lockedByUserId = parsedId;



                        if (colonIdx >= 0 && colonIdx < inner.Length - 1)



                        {



                            lockedByName = inner.Substring(colonIdx + 1);



                        }



                    }



                }



            }







            // Nếu kho đang ở Cleaning hoặc Maintenance: chỉ người đang giữ khóa hoặc Manager mới được thao tác



            var isCurrentlyBusy = unit.PhysicalStatus == StorageUnitStatusConstants.Cleaning ||



                                  unit.PhysicalStatus == StorageUnitStatusConstants.Maintenance ||



                                  unit.PhysicalStatus == StorageUnitStatusConstants.UnderMaintenanceValue;







            if (isCurrentlyBusy && lockedByUserId.HasValue && lockedByUserId.Value != employeeUserId && !isManager)



            {



                var lockerDisplay = !string.IsNullOrWhiteSpace(lockedByName) ? lockedByName : "nhân viên khác";



                throw new AppException(Error.Forbidden("StorageUnit.Locked", $"Kho này đang được xử lý bởi {lockerDisplay}. Bạn không thể thay đổi trạng thái."));



            }







            var noteReason = (request.Notes ?? request.Reason ?? string.Empty).Trim();



            var staffName = user?.EmployeeProfile?.FullName ?? $"Staff #{employeeUserId}";







            if (normalized == StorageUnitStatusConstants.Cleaning || normalized == StorageUnitStatusConstants.Maintenance || normalized == StorageUnitStatusConstants.UnderMaintenanceValue)



            {



                if (string.IsNullOrWhiteSpace(noteReason) && string.IsNullOrWhiteSpace(existingNote))



                {



                    throw new AppException(Error.Validation("StorageUnit.ReasonRequired", "Vui lòng nhập lý do/ghi chú khi chuyển kho sang trạng thái bảo trì hoặc dọn dẹp."));



                }



                var effectiveReason = !string.IsNullOrWhiteSpace(noteReason) ? noteReason : (existingNote.Contains(']') ? existingNote.Substring(existingNote.IndexOf(']') + 1).Trim() : existingNote);



                var formattedNote = $"[LOCKED_BY:{employeeUserId}:{staffName}] {effectiveReason}".Trim();



                if (formattedNote.Length > 250) formattedNote = formattedNote.Substring(0, 250);



                unit.Notes = formattedNote;



            }



            else if (normalized == StorageUnitStatusConstants.Available)



            {



                unit.Notes = null;



            }







            if (unit.PhysicalStatus != normalized)



            {



                unit.PhysicalStatus = normalized;



                unit.UpdatedAt = DateTimeOffset.UtcNow;



            }







            await _dbContext.SaveChangesAsync(cancellationToken);



        }



    }







    public async Task<MaintenanceWorkOrderDto> CreateMaintenanceOrderAsync(long employeeUserId, long unitId, CreateMaintenanceWorkOrderRequest request, CancellationToken cancellationToken = default)



    {



        var unit = await _dbContext.StorageUnits.Include(u => u.Facility).FirstOrDefaultAsync(u => u.Id == unitId, cancellationToken);



        if (unit == null) throw AppException.FromError(StaffErrors.UnitNotFound);



        var order = new MaintenanceWorkOrder



        {



            WorkOrderNo = $"WO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",



            FacilityId = unit.FacilityId,



            StorageUnitId = unit.Id,



            OpenedBy = employeeUserId,



            Title = request.Title,



            Description = request.Description,



            Priority = request.Priority,



            BlocksBooking = request.BlocksBooking,



            Status = "pending",



            EstimatedCost = request.EstimatedCost,



            CreatedAt = DateTimeOffset.UtcNow,



            UpdatedAt = DateTimeOffset.UtcNow



        };



        _dbContext.MaintenanceWorkOrders.Add(order);



        if (request.BlocksBooking) unit.PhysicalStatus = StorageUnitStatusConstants.Maintenance;



        await _dbContext.SaveChangesAsync(cancellationToken);



        return new MaintenanceWorkOrderDto(order.Id, order.WorkOrderNo, order.FacilityId, order.StorageUnitId, unit.UnitCode, order.Title, order.Description, order.Priority, order.BlocksBooking, order.Status, order.EstimatedCost, order.CreatedAt);



    }







    public async Task<IReadOnlyList<OverdueAgreementDto>> GetOverdueAgreementsAsync(long? facilityId = null, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = _dbContext.RentalAgreements
            .AsNoTracking()
            .Include(a => a.Customer)
                .ThenInclude(c => c.User)
            .Include(a => a.UnitAllocations)
                .ThenInclude(u => u.StorageUnit)
            .Include(a => a.AccessCredentials)
            .Include(a => a.Invoices)
            .Where(a => a.Status == RentalAgreementStatusConstants.Active || a.Status == RentalAgreementStatusConstants.Overdue);

        if (facilityId.HasValue && facilityId.Value > 0)
        {
            query = query.Where(a => a.FacilityId == facilityId.Value);
        }

        var agreements = await query
            .Where(a => a.EndDate < today || a.Invoices.Any(i => i.Status == InvoiceStatusConstants.Overdue || (i.Status == InvoiceStatusConstants.Open && i.DueDate < today)))
            .OrderBy(a => a.EndDate)
            .Take(50)
            .ToListAsync(cancellationToken);

        return agreements.Select(a =>
        {
            var activeAllocation = a.UnitAllocations.FirstOrDefault(ua => ua.Status == AllocationStatusConstants.Active)
                ?? a.UnitAllocations.OrderByDescending(ua => ua.Id).FirstOrDefault();
            var unit = activeAllocation?.StorageUnit;
            var cred = a.AccessCredentials.OrderByDescending(c => c.Id).FirstOrDefault();
            var overdueBalance = a.Invoices
                .Where(i => i.Status == InvoiceStatusConstants.Overdue || (i.Status == InvoiceStatusConstants.Open && i.DueDate < today))
                .Sum(i => i.TotalAmount);

            return new OverdueAgreementDto(
                AgreementId: a.Id,
                AgreementNo: a.AgreementNo,
                CustomerId: a.CustomerId,
                CustomerName: a.Customer?.FullName ?? a.Customer?.User?.Email ?? "Khách hàng",
                CustomerPhone: a.Customer?.User?.PhoneNumber,
                StorageUnitId: unit?.Id ?? 0,
                UnitCode: unit?.UnitCode ?? "—",
                OutstandingBalance: overdueBalance > 0 ? overdueBalance : 0m,
                StartDate: a.StartDate,
                EndDate: a.EndDate,
                AgreementStatus: a.Status,
                CredentialStatus: cred?.Status ?? CredentialStatusConstants.Active
            );
        }).ToList();
    }







    public async Task LockAgreementAccessAsync(long employeeUserId, long agreementId, LockAccessRequest request, CancellationToken cancellationToken = default)



    {



        var creds = await _dbContext.AccessCredentials.Where(c => c.AgreementId == agreementId).ToListAsync(cancellationToken);



        foreach (var c in creds) c.Status = CredentialStatusConstants.Suspended;



        await _dbContext.SaveChangesAsync(cancellationToken);



    }
}
