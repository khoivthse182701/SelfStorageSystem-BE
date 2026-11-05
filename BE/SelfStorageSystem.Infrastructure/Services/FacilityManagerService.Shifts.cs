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

    // 2. Phân Bổ Nhân Sự & Giao Việc Nhân Viên

    // ==========================================



    public async Task<IReadOnlyList<ManagerStaffShiftDto>> GetShiftsAsync(

        long facilityId,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);



        var shifts = await _dbContext.StaffShifts

            .AsNoTracking()

            .Include(s => s.ShiftAssignments)

            .Where(s => s.FacilityId == facilityId)

            .OrderByDescending(s => s.StartsAt)

            .Select(s => new ManagerStaffShiftDto

            {

                Id = s.Id,

                FacilityId = s.FacilityId,

                ShiftName = s.ShiftName,

                StartsAt = s.StartsAt,

                EndsAt = s.EndsAt,

                Status = s.Status,

                CreatedBy = s.CreatedBy,

                CreatedAt = s.CreatedAt,

                AssignmentCount = s.ShiftAssignments.Count

            })

            .ToListAsync(cancellationToken);



        return shifts;

    }



    public async Task<ManagerStaffShiftDto> CreateShiftAsync(

        long facilityId,

        ManagerCreateShiftRequest request,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);



        if (request.EndsAt <= request.StartsAt)

        {

            throw AppException.FromError(ManagerErrors.InvalidShiftTimes);

        }



        if (!request.AllowPast && request.StartsAt < DateTimeOffset.UtcNow)

        {

            throw AppException.FromError(ManagerErrors.PastShiftCreationNotAllowed);

        }



        var status = !string.IsNullOrWhiteSpace(request.Status) 

            ? request.Status.Trim().ToLowerInvariant() 

            : StaffShiftStatusConstants.Planned;



        var shift = new StaffShift

        {

            FacilityId = facilityId,

            ShiftName = request.ShiftName.Trim(),

            StartsAt = request.StartsAt,

            EndsAt = request.EndsAt,

            Status = status,

            CreatedBy = currentUserId,

            CreatedAt = DateTimeOffset.UtcNow

        };



        _dbContext.StaffShifts.Add(shift);

        await _dbContext.SaveChangesAsync(cancellationToken);



        return new ManagerStaffShiftDto

        {

            Id = shift.Id,

            FacilityId = shift.FacilityId,

            ShiftName = shift.ShiftName,

            StartsAt = shift.StartsAt,

            EndsAt = shift.EndsAt,

            Status = shift.Status,

            CreatedBy = shift.CreatedBy,

            CreatedAt = shift.CreatedAt,

            AssignmentCount = 0

        };

    }



    public async Task<IReadOnlyList<ManagerShiftRosterItemDto>> GetShiftRosterAsync(

        long facilityId,

        DateTimeOffset? fromDate,

        DateTimeOffset? toDate,

        long? employeeId,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);



        var query = _dbContext.ShiftAssignments

            .AsNoTracking()

            .Include(sa => sa.Shift)

            .Include(sa => sa.Employee)

            .Where(sa => sa.Shift.FacilityId == facilityId)

            .AsQueryable();



        if (fromDate.HasValue)

        {

            query = query.Where(sa => sa.Shift.StartsAt >= fromDate.Value);

        }



        if (toDate.HasValue)

        {

            query = query.Where(sa => sa.Shift.EndsAt <= toDate.Value);

        }



        if (employeeId.HasValue)

        {

            query = query.Where(sa => sa.EmployeeId == employeeId.Value);

        }



        var items = await query

            .OrderBy(sa => sa.Shift.StartsAt)

            .Select(sa => new ManagerShiftRosterItemDto

            {

                ShiftId = sa.ShiftId,

                ShiftName = sa.Shift.ShiftName,

                StartsAt = sa.Shift.StartsAt,

                EndsAt = sa.Shift.EndsAt,

                EmployeeId = sa.EmployeeId,

                EmployeeCode = sa.Employee.EmployeeCode,

                EmployeeName = sa.Employee.FullName,

                DutyRole = sa.DutyRole,

                Status = sa.Status,

                CheckInAt = sa.CheckInAt,

                CheckOutAt = sa.CheckOutAt

            })

            .ToListAsync(cancellationToken);



        return items;

    }



    public async Task<ManagerShiftAssignmentDto> AssignShiftAsync(

        long facilityId,

        ManagerCreateShiftAssignmentRequest request,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);



        var shift = await _dbContext.StaffShifts

            .FirstOrDefaultAsync(s => s.Id == request.ShiftId && s.FacilityId == facilityId, cancellationToken);

        if (shift == null)

        {

            throw AppException.FromError(ManagerErrors.ShiftNotFound);

        }



        if (!request.AllowPast && shift.StartsAt < DateTimeOffset.UtcNow)

        {

            throw AppException.FromError(ManagerErrors.PastShiftAssignmentNotAllowed);

        }



        var employee = await _dbContext.EmployeeProfiles

            .Include(ep => ep.StaffFacilityAssignments)

            .FirstOrDefaultAsync(ep => ep.UserId == request.EmployeeId, cancellationToken);

        if (employee == null)

        {

            throw AppException.FromError(AdminErrors.EmployeeProfileNotFound);

        }



        var now = DateTimeOffset.UtcNow;

        var isAssignedToFacility = employee.StaffFacilityAssignments

            .Any(sfa => sfa.FacilityId == facilityId && (sfa.EndsAt == null || sfa.EndsAt > now));

        if (!isAssignedToFacility)

        {

            throw AppException.FromError(ManagerErrors.EmployeeNotAssignedToFacility);

        }



        var existing = await _dbContext.ShiftAssignments

            .Include(sa => sa.Employee)

            .FirstOrDefaultAsync(sa => sa.ShiftId == request.ShiftId && sa.EmployeeId == request.EmployeeId, cancellationToken);

        if (existing != null)

        {

            if (!string.IsNullOrWhiteSpace(request.DutyRole))

            {

                existing.DutyRole = request.DutyRole.Trim().ToLowerInvariant();

            }

            if (!string.IsNullOrWhiteSpace(request.Status))

            {

                existing.Status = request.Status.Trim().ToLowerInvariant();

            }

            await _dbContext.SaveChangesAsync(cancellationToken);



            return new ManagerShiftAssignmentDto

            {

                ShiftId = existing.ShiftId,

                EmployeeId = existing.EmployeeId,

                EmployeeCode = existing.Employee.EmployeeCode,

                EmployeeName = existing.Employee.FullName,

                DutyRole = existing.DutyRole,

                Status = existing.Status,

                CheckInAt = existing.CheckInAt,

                CheckOutAt = existing.CheckOutAt

            };

        }



        var overlappingAssignments = await _dbContext.ShiftAssignments

            .Include(sa => sa.Shift)

            .Where(sa => sa.EmployeeId == request.EmployeeId

                      && sa.ShiftId != request.ShiftId

                      && sa.Shift.Status != StaffShiftStatusConstants.Cancelled

                      && sa.Status != ShiftAssignmentStatusConstants.Absent

                      && sa.Shift.StartsAt < shift.EndsAt

                      && sa.Shift.EndsAt > shift.StartsAt)

            .ToListAsync(cancellationToken);

        if (overlappingAssignments.Count > 0)

        {

            throw AppException.FromError(ManagerErrors.OverlappingShiftAssignment);

        }



        var status = !string.IsNullOrWhiteSpace(request.Status) 

            ? request.Status.Trim().ToLowerInvariant() 

            : ShiftAssignmentStatusConstants.Scheduled;



        var assignment = new ShiftAssignment

        {

            ShiftId = request.ShiftId,

            EmployeeId = request.EmployeeId,

            DutyRole = request.DutyRole.Trim().ToLowerInvariant(),

            Status = status

        };



        _dbContext.ShiftAssignments.Add(assignment);

        await _dbContext.SaveChangesAsync(cancellationToken);



        return new ManagerShiftAssignmentDto

        {

            ShiftId = assignment.ShiftId,

            EmployeeId = assignment.EmployeeId,

            EmployeeCode = employee.EmployeeCode,

            EmployeeName = employee.FullName,

            DutyRole = assignment.DutyRole,

            Status = assignment.Status,

            CheckInAt = assignment.CheckInAt,

            CheckOutAt = assignment.CheckOutAt

        };

    }



    public async Task DeleteShiftAssignmentAsync(

        long facilityId,

        long shiftId,

        long employeeId,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);



        var assignment = await _dbContext.ShiftAssignments

            .Include(sa => sa.Shift)

            .FirstOrDefaultAsync(sa => sa.ShiftId == shiftId && sa.EmployeeId == employeeId && sa.Shift.FacilityId == facilityId, cancellationToken);



        if (assignment != null)

        {

            _dbContext.ShiftAssignments.Remove(assignment);

            await _dbContext.SaveChangesAsync(cancellationToken);

        }

    }



    public async Task<ManagerBatchSaveShiftAssignmentsResultDto> BatchSaveShiftAssignmentsAsync(

        long facilityId,

        ManagerBatchSaveShiftAssignmentsRequest request,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);



        IDbContextTransaction? transaction = null;

        if (_dbContext.Database.IsSqlServer())

        {

            transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        }



        try

        {

            var result = new ManagerBatchSaveShiftAssignmentsResultDto();

            var toDelete = request.AssignmentsToDelete ?? new List<ManagerBatchDeleteShiftAssignmentItem>();

            var toAdd = request.AssignmentsToAdd ?? new List<ManagerBatchShiftAssignmentItem>();



            // 1. Process Deletions (Batch pre-fetch to eliminate N+1 queries)

            var deleteKeys = toDelete.Select(d => (d.ShiftId, d.EmployeeId)).ToHashSet();

            if (toDelete.Count > 0)

            {

                var delShiftIds = toDelete.Select(d => d.ShiftId).Distinct().ToList();

                var delEmpIds = toDelete.Select(d => d.EmployeeId).Distinct().ToList();



                var assignmentsToDelete = await _dbContext.ShiftAssignments

                    .Include(sa => sa.Shift)

                    .Where(sa => sa.Shift.FacilityId == facilityId

                              && delShiftIds.Contains(sa.ShiftId)

                              && delEmpIds.Contains(sa.EmployeeId))

                    .ToListAsync(cancellationToken);



                foreach (var assignment in assignmentsToDelete)

                {

                    if (deleteKeys.Contains((assignment.ShiftId, assignment.EmployeeId)))

                    {

                        _dbContext.ShiftAssignments.Remove(assignment);

                        result.DeletedCount++;

                    }

                }



                await _dbContext.SaveChangesAsync(cancellationToken);

            }



            // 2. Process Additions (Batch pre-fetch and in-memory overlap evaluation)

            if (toAdd.Count > 0)

            {

                // Check duplicate within the batch

                var duplicateInBatch = toAdd

                    .GroupBy(x => new { x.ShiftId, x.EmployeeId })

                    .Any(g => g.Count() > 1);

                if (duplicateInBatch)

                {

                    throw AppException.FromError(ManagerErrors.DuplicateAssignmentInBatch);

                }



                var shiftIds = toAdd.Select(x => x.ShiftId).Distinct().ToList();

                var shifts = await _dbContext.StaffShifts

                    .Where(s => shiftIds.Contains(s.Id) && s.FacilityId == facilityId)

                    .ToDictionaryAsync(s => s.Id, cancellationToken);



                var employeeIds = toAdd.Select(x => x.EmployeeId).Distinct().ToList();

                var employees = await _dbContext.EmployeeProfiles

                    .Include(ep => ep.StaffFacilityAssignments)

                    .Where(ep => employeeIds.Contains(ep.UserId))

                    .ToDictionaryAsync(ep => ep.UserId, cancellationToken);



                var now = DateTimeOffset.UtcNow;

                var minStart = shifts.Values.Count > 0 ? shifts.Values.Min(s => s.StartsAt) : now;

                var maxEnd = shifts.Values.Count > 0 ? shifts.Values.Max(s => s.EndsAt) : now;



                // Pre-fetch all active existing assignments for candidate employees in 1 query

                var existingEmployeeAssignments = await _dbContext.ShiftAssignments

                    .AsNoTracking()

                    .Include(sa => sa.Shift)

                    .Where(sa => employeeIds.Contains(sa.EmployeeId)

                              && sa.Shift.Status != StaffShiftStatusConstants.Cancelled

                              && sa.Status != ShiftAssignmentStatusConstants.Absent

                              && sa.Shift.StartsAt < maxEnd

                              && sa.Shift.EndsAt > minStart)

                    .ToListAsync(cancellationToken);



                // Ignore assignments that were deleted in step 1

                var activeExistingAssignments = existingEmployeeAssignments

                    .Where(sa => !deleteKeys.Contains((sa.ShiftId, sa.EmployeeId)))

                    .ToList();



                var newAssignments = new List<ShiftAssignment>();

                var plannedIntervals = new List<(long EmployeeId, DateTimeOffset StartsAt, DateTimeOffset EndsAt)>();



                foreach (var item in toAdd)

                {

                    if (!shifts.TryGetValue(item.ShiftId, out var shift))

                    {

                        throw AppException.FromError(ManagerErrors.ShiftNotFound);

                    }



                    if (!employees.TryGetValue(item.EmployeeId, out var employee))

                    {

                        throw AppException.FromError(AdminErrors.EmployeeProfileNotFound);

                    }



                    var isAssignedToFacility = employee.StaffFacilityAssignments

                        .Any(sfa => sfa.FacilityId == facilityId && (sfa.EndsAt == null || sfa.EndsAt > now));

                    if (!isAssignedToFacility)

                    {

                        throw AppException.FromError(ManagerErrors.EmployeeNotAssignedToFacility);

                    }



                    if (!item.AllowPast && shift.StartsAt < now)

                    {

                        throw AppException.FromError(ManagerErrors.PastShiftAssignmentNotAllowed);

                    }



                    // Check already exists in DB (evaluated in memory)

                    var exists = activeExistingAssignments.Any(sa => sa.ShiftId == item.ShiftId && sa.EmployeeId == item.EmployeeId);

                    if (exists)

                    {

                        throw AppException.FromError(ManagerErrors.ShiftAssignmentAlreadyExists);

                    }



                    // Check overlapping within the batch itself

                    var overlapInBatch = plannedIntervals.Any(pi => pi.EmployeeId == item.EmployeeId

                                                                  && pi.StartsAt < shift.EndsAt

                                                                  && pi.EndsAt > shift.StartsAt);

                    if (overlapInBatch)

                    {

                        throw AppException.FromError(ManagerErrors.OverlappingShiftAssignment);

                    }



                    // Check overlapping with other shifts in DB (evaluated in memory)

                    var overlapInDb = activeExistingAssignments.Any(sa => sa.EmployeeId == item.EmployeeId

                                                                       && sa.ShiftId != shift.Id

                                                                       && sa.Shift.StartsAt < shift.EndsAt

                                                                       && sa.Shift.EndsAt > shift.StartsAt);

                    if (overlapInDb)

                    {

                        throw AppException.FromError(ManagerErrors.OverlappingShiftAssignment);

                    }



                    plannedIntervals.Add((item.EmployeeId, shift.StartsAt, shift.EndsAt));



                    var status = !string.IsNullOrWhiteSpace(item.Status)

                        ? item.Status.Trim().ToLowerInvariant()

                        : ShiftAssignmentStatusConstants.Scheduled;



                    var assignment = new ShiftAssignment

                    {

                        ShiftId = item.ShiftId,

                        EmployeeId = item.EmployeeId,

                        DutyRole = item.DutyRole.Trim().ToLowerInvariant(),

                        Status = status

                    };



                    newAssignments.Add(assignment);

                    _dbContext.ShiftAssignments.Add(assignment);



                    result.Assignments.Add(new ManagerShiftAssignmentDto

                    {

                        ShiftId = assignment.ShiftId,

                        EmployeeId = assignment.EmployeeId,

                        EmployeeCode = employee.EmployeeCode,

                        EmployeeName = employee.FullName,

                        DutyRole = assignment.DutyRole,

                        Status = assignment.Status

                    });

                }



                await _dbContext.SaveChangesAsync(cancellationToken);

                result.AddedCount = newAssignments.Count;

            }



            if (transaction != null)

            {

                await transaction.CommitAsync(cancellationToken);

            }



            return result;

        }

        catch

        {

            if (transaction != null)

            {

                await transaction.RollbackAsync(cancellationToken);

            }

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



    public async Task<IReadOnlyList<ManagerFacilityStaffDto>> GetFacilityStaffAsync(

        long facilityId,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);



        var now = DateTimeOffset.UtcNow;

        var staff = await _dbContext.StaffFacilityAssignments

            .AsNoTracking()

            .Include(sfa => sfa.Employee)

                .ThenInclude(ep => ep.User)

            .Where(sfa => sfa.FacilityId == facilityId && (sfa.EndsAt == null || sfa.EndsAt > now))

            .OrderBy(sfa => sfa.Employee.FullName)

            .Select(sfa => new ManagerFacilityStaffDto

            {

                EmployeeId = sfa.EmployeeId,

                EmployeeCode = sfa.Employee.EmployeeCode,

                FullName = sfa.Employee.FullName,

                AssignmentRole = sfa.AssignmentRole,

                EmploymentStatus = sfa.Employee.EmploymentStatus,

                Email = sfa.Employee.User.Email,

                PhoneNumber = sfa.Employee.User.PhoneNumber

            })

            .ToListAsync(cancellationToken);



        return staff;

    }



    public async Task<ManagerStaffTaskDto> CreateStaffTaskAsync(

        long facilityId,

        ManagerCreateStaffTaskRequest request,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);



        var taskType = request.TaskType.Trim().ToLowerInvariant();

        if (!StaffTaskTypeConstants.All.Contains(taskType))

        {

            throw AppException.FromError(ManagerErrors.InvalidTaskType);

        }



        EmployeeProfile? employee = null;

        if (request.AssignedEmployeeId.HasValue)

        {

            employee = await _dbContext.EmployeeProfiles

                .FirstOrDefaultAsync(ep => ep.UserId == request.AssignedEmployeeId.Value, cancellationToken);

            if (employee == null)

            {

                throw AppException.FromError(AdminErrors.EmployeeProfileNotFound);

            }

        }



        var status = !string.IsNullOrWhiteSpace(request.Status) 

            ? request.Status.Trim().ToLowerInvariant() 

            : StaffTaskStatusConstants.Todo;



        var progress = request.ProgressPercent ?? 0;

        if (status == StaffTaskStatusConstants.Done)

        {

            progress = 100;

        }



        var now = DateTimeOffset.UtcNow;

        var task = new StaffTask

        {

            FacilityId = facilityId,

            ShiftId = request.ShiftId,

            AssignedEmployeeId = request.AssignedEmployeeId,

            TicketId = request.TicketId,

            MaintenanceWorkOrderId = request.MaintenanceWorkOrderId,

            TaskType = taskType,

            Title = request.Title.Trim(),

            DueAt = request.DueAt,

            Status = status,

            ProgressPercent = progress,

            CreatedBy = currentUserId,

            CreatedAt = now,

            UpdatedAt = now

        };



        // Đồng bộ tạo SupportTicket để hiển thị trên API GET /api/staff/tickets của Staff

        if (!request.TicketId.HasValue)

        {

            var syncTicket = new SupportTicket

            {

                TicketNo = TicketConstants.GenerateTicketNo(),

                FacilityId = facilityId,

                CustomerId = currentUserId,

                Category = taskType,

                Priority = TicketPriorityConstants.Normal,

                Subject = request.Title.Trim(),

                Description = $"Nhiệm vụ điều phối từ Quản lý cơ sở: {request.Title.Trim()}",

                Status = TicketStatusConstants.Open,

                CreatedAt = now,

                UpdatedAt = now

            };

            _dbContext.SupportTickets.Add(syncTicket);

            await _dbContext.SaveChangesAsync(cancellationToken);

            task.TicketId = syncTicket.Id;



            if (request.AssignedEmployeeId.HasValue)

            {

                _dbContext.TicketAssignments.Add(new TicketAssignment

                {

                    TicketId = syncTicket.Id,

                    EmployeeId = request.AssignedEmployeeId.Value,

                    AssignedBy = currentUserId,

                    AssignedAt = now

                });

            }

        }



        _dbContext.StaffTasks.Add(task);

        await _dbContext.SaveChangesAsync(cancellationToken);



        return new ManagerStaffTaskDto

        {

            Id = task.Id,

            FacilityId = task.FacilityId,

            ShiftId = task.ShiftId,

            AssignedEmployeeId = task.AssignedEmployeeId,

            AssignedEmployeeCode = employee?.EmployeeCode,

            AssignedEmployeeName = employee?.FullName,

            TicketId = task.TicketId,

            MaintenanceWorkOrderId = task.MaintenanceWorkOrderId,

            TaskType = task.TaskType,

            Title = task.Title,

            DueAt = task.DueAt,

            Status = task.Status,

            ProgressPercent = task.ProgressPercent,

            CreatedBy = task.CreatedBy,

            CreatedAt = task.CreatedAt,

            UpdatedAt = task.UpdatedAt

        };

    }
}
