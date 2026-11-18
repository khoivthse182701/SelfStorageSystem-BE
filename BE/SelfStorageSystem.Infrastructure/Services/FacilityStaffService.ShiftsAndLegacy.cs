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

        var now = DateTimeOffset.UtcNow;
        var assignments = await query
            .OrderBy(sa => sa.Shift.StartsAt)
            .ToListAsync(cancellationToken);

        // Tự động đóng các ca trực trong quá khứ mà nhân viên đã check-in nhưng quên check-out
        bool hasAutoCloses = false;
        foreach (var sa in assignments)
        {
            if (sa.CheckInAt.HasValue && !sa.CheckOutAt.HasValue && sa.Shift.EndsAt < now)
            {
                sa.CheckOutAt = sa.Shift.EndsAt;
                sa.Status = ShiftAssignmentStatusConstants.CheckedOut;
                hasAutoCloses = true;
            }
        }
        if (hasAutoCloses)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

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

        var now = DateTimeOffset.UtcNow;
        // Chỉ cho phép điểm danh vào ca của ngày hôm nay (từ trước 60 phút đến trước khi kết thúc ca)
        if (assignment.Shift.EndsAt < now)
        {
            throw AppException.FromError(Error.Validation("Staff.ShiftExpired", "Ca trực này đã kết thúc trong quá khứ, không thể điểm danh vào ca."));
        }
        if (assignment.Shift.StartsAt > now.AddMinutes(60))
        {
            throw AppException.FromError(Error.Validation("Staff.ShiftTooEarly", "Chưa đến giờ vào ca trực (chỉ cho phép điểm danh trước giờ bắt đầu tối đa 60 phút)."));
        }

        assignment.CheckInAt = now;
        assignment.Status = ShiftAssignmentStatusConstants.CheckedIn;
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

        // Khử trùng lặp: Nếu nhiều task gắn cùng 1 TicketId hoặc có cùng mã sự cố [Sự cố #TCK-...], giữ lại 1 bản ghi mới nhất
        var deduped = tasks
            .GroupBy(t =>
            {
                if (t.TicketId.HasValue) return $"ticket_{t.TicketId.Value}";
                if (!string.IsNullOrWhiteSpace(t.Title) && t.Title.StartsWith("[Sự cố #"))
                {
                    var endBracket = t.Title.IndexOf(']');
                    if (endBracket > 8) return t.Title.Substring(0, endBracket);
                }
                if (!string.IsNullOrWhiteSpace(t.Title) && t.Title.StartsWith("[Nghiệm thu trả kho"))
                {
                    var endBracket = t.Title.IndexOf(']');
                    if (endBracket > 10) return t.Title.Substring(0, endBracket);
                }
                return $"task_{t.Id}";
            })
            .Select(g => g.OrderByDescending(t => t.UpdatedAt).ThenByDescending(t => t.CreatedAt).First())
            .OrderByDescending(t => t.CreatedAt)
            .ToList();

        return deduped.Select(t => new StaffTaskDto(t.Id, t.FacilityId, t.Facility.Code, t.AssignedEmployeeId, t.AssignedEmployee?.FullName, t.TaskType, t.Title, t.DueAt, t.Status, t.ProgressPercent, t.CreatedAt, t.UpdatedAt)).ToList();
    }

    public async Task<StaffTaskDto> UpdateStaffTaskStatusAsync(long employeeUserId, long taskId, UpdateStaffTaskStatusRequest request, CancellationToken cancellationToken = default)
    {
        var task = await _dbContext.StaffTasks.Include(t => t.Facility).Include(t => t.AssignedEmployee).FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task == null) throw AppException.FromError(StaffErrors.TaskNotFound);

        var now = DateTimeOffset.UtcNow;
        if (task.AssignedEmployeeId != employeeUserId)
        {
            var isAssignedToFacility = await _dbContext.StaffFacilityAssignments
                .AnyAsync(sfa => sfa.EmployeeId == employeeUserId 
                              && sfa.FacilityId == task.FacilityId 
                              && (sfa.EndsAt == null || sfa.EndsAt > now), cancellationToken);

            if (!isAssignedToFacility)
            {
                throw AppException.FromError(StaffErrors.StaffNotAssignedToFacility);
            }
        }

        var normalizedStatus = request.Status?.Trim().ToLowerInvariant();
        if (normalizedStatus == "completed" || normalizedStatus == "resolved")
        {
            normalizedStatus = "done";
        }
        else if (normalizedStatus == "severe" || normalizedStatus == "severe_need_relocation")
        {
            normalizedStatus = "blocked";
        }

        var validStatuses = new HashSet<string> { "todo", "in_progress", "blocked", "done", "cancelled" };
        if (!string.IsNullOrEmpty(normalizedStatus) && validStatuses.Contains(normalizedStatus))
        {
            task.Status = normalizedStatus;
        }
        else
        {
            throw AppException.FromError(Error.Validation("Staff.InvalidTaskStatus", $"Trạng thái nhiệm vụ '{request.Status}' không hợp lệ. Cho phép: todo, in_progress, done, blocked, cancelled."));
        }

        // Tự động map tiến độ theo trạng thái:
        // todo -> 0%, in_progress -> 50%, blocked -> 50%, done -> 100%
        short autoProgress = normalizedStatus switch
        {
            "done" => 100,
            "in_progress" => 50,
            "blocked" => 50,
            _ => 0
        };

        if (task.Status == "done")
        {
            task.ProgressPercent = 100;
        }
        else
        {
            task.ProgressPercent = request.ProgressPercent ?? autoProgress;
        }

        task.UpdatedAt = now;

        // Nếu task liên kết với SupportTicket, đồng bộ báo cáo kết quả lên Ticket cho Manager quản lý
        if (task.TicketId.HasValue)
        {
            var ticket = await _dbContext.SupportTickets
                .Include(t => t.StorageUnit)
                .FirstOrDefaultAsync(t => t.Id == task.TicketId.Value, cancellationToken);

            if (ticket != null)
            {
                var employee = await _dbContext.EmployeeProfiles
                    .FirstOrDefaultAsync(ep => ep.UserId == employeeUserId, cancellationToken);
                var employeeName = employee?.FullName ?? "Nhân viên";

                if (task.Status == "done")
                {
                    ticket.Status = TicketStatusConstants.Resolved;
                    ticket.ResolvedAt = now;
                    if (!string.IsNullOrWhiteSpace(request.ReportNote))
                    {
                        ticket.Resolution = request.ReportNote.Trim();
                        _dbContext.TicketMessages.Add(new TicketMessage
                        {
                            TicketId = ticket.Id,
                            AuthorUserId = employeeUserId,
                            Body = $"[Báo cáo hoàn thành - {employeeName}]\n{request.ReportNote.Trim()}",
                            IsInternal = false,
                            CreatedAt = now
                        });
                    }
                    else if (string.IsNullOrWhiteSpace(ticket.Resolution))
                    {
                        ticket.Resolution = "Đã khắc phục xong sự cố.";
                    }
                }
                else if (task.Status == "blocked")
                {
                    ticket.Status = TicketStatusConstants.SevereNeedRelocation;
                    var noteText = !string.IsNullOrWhiteSpace(request.ReportNote)
                        ? request.ReportNote.Trim()
                        : "Sự cố hư hỏng nặng, đề nghị Manager đổi kho mới cho khách hàng.";
                    ticket.Resolution = $"[Hư hỏng nặng - Đề nghị đổi kho] {noteText}";

                    _dbContext.TicketMessages.Add(new TicketMessage
                    {
                        TicketId = ticket.Id,
                        AuthorUserId = employeeUserId,
                        Body = $"[Báo cáo hư hỏng nặng - {employeeName}]\n{noteText}",
                        IsInternal = false,
                        CreatedAt = now
                    });

                    if (ticket.StorageUnit != null && ticket.StorageUnit.PhysicalStatus != StorageUnitStatusConstants.UnderMaintenanceValue)
                    {
                        ticket.StorageUnit.PhysicalStatus = StorageUnitStatusConstants.UnderMaintenanceValue;
                        ticket.StorageUnit.Notes = $"[LOCKED_BY:{employeeUserId}:{employeeName}] Hư hỏng nặng: {noteText}";
                    }
                }
                else if (task.Status == "in_progress")
                {
                    if (ticket.Status == TicketStatusConstants.Assigned || ticket.Status == TicketStatusConstants.Open)
                    {
                        ticket.Status = TicketStatusConstants.InProgress;
                    }
                    if (!string.IsNullOrWhiteSpace(request.ReportNote))
                    {
                        _dbContext.TicketMessages.Add(new TicketMessage
                        {
                            TicketId = ticket.Id,
                            AuthorUserId = employeeUserId,
                            Body = $"[Cập nhật tiến độ - {employeeName}]\n{request.ReportNote.Trim()}",
                            IsInternal = false,
                            CreatedAt = now
                        });
                    }
                }
                ticket.UpdatedAt = now;
            }

            // Đồng bộ trạng thái cho các task trùng lặp (nếu có trong DB lịch sử)
            var siblingTasks = await _dbContext.StaffTasks
                .Where(st => st.TicketId == task.TicketId.Value && st.Id != task.Id)
                .ToListAsync(cancellationToken);
            foreach (var st in siblingTasks)
            {
                st.Status = task.Status;
                st.ProgressPercent = task.ProgressPercent;
                st.UpdatedAt = now;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_realtimeNotifier != null)
        {
            try
            {
                await _realtimeNotifier.SendTaskAssignmentAsync(employeeUserId, new
                {
                    taskId = task.Id,
                    status = task.Status,
                    progressPercent = task.ProgressPercent,
                    facilityId = task.FacilityId,
                    updatedAt = now
                });
                if (task.TicketId.HasValue)
                {
                    await _realtimeNotifier.SendTicketUpdateAsync(task.FacilityId, new
                    {
                        ticketId = task.TicketId.Value,
                        status = task.Status == "done" ? TicketStatusConstants.Resolved : (task.Status == "blocked" ? TicketStatusConstants.SevereNeedRelocation : TicketStatusConstants.InProgress),
                        facilityId = task.FacilityId,
                        updatedAt = now
                    });
                }
            }
            catch
            {
                // Non-blocking
            }
        }

        return new StaffTaskDto(task.Id, task.FacilityId, task.Facility.Code, task.AssignedEmployeeId, task.AssignedEmployee?.FullName, task.TaskType, task.Title, task.DueAt, task.Status, task.ProgressPercent, task.CreatedAt, task.UpdatedAt);
    }

    public async Task<IReadOnlyList<StaffReservationLookupDto>> LookupReservationsAsync(long employeeUserId, string? query, long? facilityId = null, CancellationToken cancellationToken = default)
    {
        var targetFacilityId = await ResolveFacilityIdAsync(employeeUserId, facilityId, cancellationToken);
        return await LookupReservationsCoreAsync(query, targetFacilityId, cancellationToken);
    }

    private async Task<IReadOnlyList<StaffReservationLookupDto>> LookupReservationsCoreAsync(string? query, long? facilityId, CancellationToken cancellationToken)
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

        await ResolveFacilityIdAsync(employeeUserId, reservation.FacilityId, cancellationToken);
        var unit = await _dbContext.StorageUnits.FirstOrDefaultAsync(u => u.Id == request.StorageUnitId, cancellationToken);
        if (unit == null) throw AppException.FromError(StaffErrors.UnitNotFound);

        if (unit.FacilityId != reservation.FacilityId)
            throw AppException.FromError(Error.Validation("Staff.FacilityMismatch", "Ô kho được chọn không thuộc cùng cơ sở với đơn đặt chỗ."));

        if (unit.UnitTypeId != reservation.UnitTypeId)
            throw AppException.FromError(StaffErrors.UnitTypeMismatch);
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

    public async Task<IReadOnlyList<StaffMoveOutSummaryDto>> GetMoveOutsAsync(long employeeUserId, long? facilityId = null, DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        var targetFacilityId = await EnsureStrictFacilityAccessAsync(employeeUserId, facilityId, cancellationToken);
        return await GetMoveOutsCoreAsync(targetFacilityId, date, cancellationToken);
    }

    private async Task<IReadOnlyList<StaffMoveOutSummaryDto>> GetMoveOutsCoreAsync(long? facilityId, DateOnly? date, CancellationToken cancellationToken)
    {
        var q = _dbContext.MoveOutRequests.AsNoTracking()
            .Include(m => m.Agreement)
                .ThenInclude(a => a.Customer)
            .Include(m => m.Agreement)
                .ThenInclude(a => a.Facility)
            .Include(m => m.Agreement)
                .ThenInclude(a => a.UnitAllocations)
                    .ThenInclude(ua => ua.StorageUnit)
            .Include(m => m.Agreement)
                .ThenInclude(a => a.Reservation)
                    .ThenInclude(r => r.UnitAllocation)
                        .ThenInclude(ua => ua!.StorageUnit)
            .AsQueryable();

        if (facilityId.HasValue) q = q.Where(m => m.Agreement.FacilityId == facilityId.Value);
        if (date.HasValue) q = q.Where(m => m.RequestedMoveOutDate == date.Value);
        var list = await q.OrderByDescending(m => m.RequestedMoveOutDate).ToListAsync(cancellationToken);

        var agreementIds = list.Select(m => m.AgreementId).Distinct().ToList();
        var refunds = await _dbContext.Refunds.AsNoTracking()
            .Where(r => r.AgreementId.HasValue && agreementIds.Contains(r.AgreementId.Value))
            .OrderByDescending(r => r.Id)
            .ToListAsync(cancellationToken);
        var refundByAgreement = refunds
            .GroupBy(r => r.AgreementId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var result = list.Select(m =>
        {
            var alloc = m.Agreement.UnitAllocations
                .OrderByDescending(ua => ua.Status == AllocationStatusConstants.Active ? 1 : 0)
                .ThenByDescending(ua => ua.Id)
                .FirstOrDefault();
            var unitCode = alloc?.StorageUnit?.UnitCode
                ?? m.Agreement.Reservation?.UnitAllocation?.StorageUnit?.UnitCode
                ?? "N/A";
            refundByAgreement.TryGetValue(m.AgreementId, out var refItem);

            var deposit = m.Agreement.DepositSnapshot > 0 ? m.Agreement.DepositSnapshot : m.Agreement.DepositBalance;

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
                m.CreatedAt,
                refItem?.Id,
                refItem?.Amount,
                refItem?.Provider,
                refItem?.Status,
                deposit
            );
        }).ToList();

        // Đồng bộ các biên bản nghiệm thu trả kho (check_out) mà Quản lý trực tiếp phân công (chưa có bản ghi move_out_requests)
        var existingAgreementIds = list.Select(m => (long?)m.AgreementId).ToHashSet();
        var inspQuery = _dbContext.Inspections.AsNoTracking()
            .Include(i => i.Agreement).ThenInclude(a => a!.Customer)
            .Include(i => i.Agreement).ThenInclude(a => a!.Facility)
            .Include(i => i.Agreement).ThenInclude(a => a!.UnitAllocations).ThenInclude(ua => ua.StorageUnit)
            .Include(i => i.StorageUnit)
            .Where(i => i.AgreementId.HasValue && !existingAgreementIds.Contains(i.AgreementId) &&
                        (i.InspectionType == InspectionTypeConstants.CheckOut || i.InspectionType == "move_out"));

        if (facilityId.HasValue) inspQuery = inspQuery.Where(i => i.FacilityId == facilityId.Value);

        var standaloneInspections = await inspQuery.OrderByDescending(i => i.CreatedAt).ToListAsync(cancellationToken);

        if (standaloneInspections.Count > 0)
        {
            var extraAgreementIds = standaloneInspections.Select(i => i.AgreementId!.Value).Distinct().ToList();
            var extraRefunds = await _dbContext.Refunds.AsNoTracking()
                .Where(r => r.AgreementId.HasValue && extraAgreementIds.Contains(r.AgreementId.Value))
                .OrderByDescending(r => r.Id)
                .ToListAsync(cancellationToken);
            var extraRefundByAgreement = extraRefunds
                .GroupBy(r => r.AgreementId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            var latestInspPerAgreement = standaloneInspections
                .GroupBy(i => i.AgreementId!.Value)
                .Select(g => g.First());

            foreach (var insp in latestInspPerAgreement)
            {
                var agr = insp.Agreement;
                if (agr == null) continue;

                var alloc = agr.UnitAllocations
                    .OrderByDescending(ua => ua.Status == AllocationStatusConstants.Active ? 1 : 0)
                    .ThenByDescending(ua => ua.Id)
                    .FirstOrDefault();
                var unitCode = insp.StorageUnit?.UnitCode ?? alloc?.StorageUnit?.UnitCode ?? "N/A";
                extraRefundByAgreement.TryGetValue(agr.Id, out var refItem);

                var deposit = agr.DepositSnapshot > 0 ? agr.DepositSnapshot : agr.DepositBalance;
                var itemStatus = (insp.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) || insp.Status.Equals("resolved", StringComparison.OrdinalIgnoreCase))
                    ? "completed"
                    : MoveOutStatusConstants.InspectionPending;

                var moveOutDate = DateOnly.FromDateTime(agr.EndDate.ToDateTime(TimeOnly.MinValue));
                if (date.HasValue && moveOutDate != date.Value) continue;

                result.Add(new StaffMoveOutSummaryDto(
                    insp.Id,
                    agr.Id,
                    agr.AgreementNo,
                    agr.CustomerId,
                    agr.Customer.FullName,
                    agr.FacilityId,
                    unitCode,
                    moveOutDate,
                    itemStatus,
                    insp.Summary ?? "Quản lý phân công nghiệm thu trả kho theo hạn hợp đồng",
                    insp.CreatedAt,
                    refItem?.Id,
                    refItem?.Amount,
                    refItem?.Provider,
                    refItem?.Status,
                    deposit
                ));
            }
        }

        return result.OrderByDescending(r => r.RequestedMoveOutDate).ThenByDescending(r => r.CreatedAt).ToList();
    }

    public Task<InspectMoveOutResultDto> InspectMoveOutAsync(long employeeUserId, long moveOutId, InspectMoveOutRequest request, CancellationToken cancellationToken = default)
        => InspectMoveOutV1InternalAsync(employeeUserId, moveOutId, request, cancellationToken);

    [Obsolete("Use GetLogisticsTicketsAsync instead.")]
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

        await ResolveFacilityIdAsync(employeeUserId, ticket.FacilityId, cancellationToken);

        var messages = await _dbContext.TicketMessages
            .AsNoTracking()
            .Include(m => m.AuthorUser)
                .ThenInclude(u => u!.EmployeeProfile)
            .Include(m => m.AuthorUser)
                .ThenInclude(u => u!.CustomerProfile)
            .Include(m => m.AuthorUser)
                .ThenInclude(u => u!.UserRoleUsers)
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

        await ResolveFacilityIdAsync(employeeUserId, ticket.FacilityId, cancellationToken);

        var msg = new TicketMessage { TicketId = ticketId, AuthorUserId = employeeUserId, Body = request.Body, IsInternal = request.IsInternal, CreatedAt = DateTimeOffset.UtcNow };
        _dbContext.TicketMessages.Add(msg);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new StaffTicketMessageDto(msg.Id, ticketId, employeeUserId, emp.FullName, "Staff", request.Body, request.IsInternal, msg.CreatedAt);
    }

    public Task<TicketChargeProposalDto> ProposeTicketChargeAsync(long employeeUserId, long ticketId, ProposeTicketChargeRequest request, CancellationToken cancellationToken = default)
        => ProposeTicketChargeAsync(employeeUserId, ticketId, new StaffProposeChargeRequest(request.Amount, request.Description), cancellationToken);

    public async Task ResolveTicketAsync(long employeeUserId, long ticketId, ResolveTicketRequest request, CancellationToken cancellationToken = default)
        => await CompleteTicketAsync(employeeUserId, ticketId, new StaffCompleteTicketRequest(request.Resolution), cancellationToken);

    public async Task<IReadOnlyList<StaffFacilityUnitDto>> GetFacilityUnitsAsync(long employeeUserId, long facilityId, CancellationToken cancellationToken = default)
    {
        var units = await GetUnitsAsync(employeeUserId, facilityId, cancellationToken: cancellationToken);
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

        await ResolveFacilityIdAsync(employeeUserId, unit.FacilityId, cancellationToken);
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

    public async Task<IReadOnlyList<OverdueAgreementDto>> GetOverdueAgreementsAsync(long employeeUserId, long? facilityId = null, CancellationToken cancellationToken = default)
    {
        var targetFacilityId = await EnsureStrictFacilityAccessAsync(employeeUserId, facilityId, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = _dbContext.RentalAgreements
            .AsNoTracking()
            .Include(a => a.Customer)
            .ThenInclude(c => c.User)
            .Include(a => a.UnitAllocations)
            .ThenInclude(u => u.StorageUnit)
            .Include(a => a.AccessCredentials)
            .Include(a => a.Invoices)
            .Where(a => a.FacilityId == targetFacilityId && (a.Status == RentalAgreementStatusConstants.Active || a.Status == RentalAgreementStatusConstants.Overdue));

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
        await LockAgreementAccessV1Async(employeeUserId, agreementId, request, cancellationToken);
    }
}
