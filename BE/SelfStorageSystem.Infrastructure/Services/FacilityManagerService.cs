using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Rentals;
using SelfStorageSystem.Contracts.Manager;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public class FacilityManagerService : IFacilityManagerService
{
    private readonly SelfStorageDbContext _dbContext;
    private readonly ILogger<FacilityManagerService> _logger;

    public FacilityManagerService(
        SelfStorageDbContext dbContext,
        ILogger<FacilityManagerService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    // ==========================================
    // 1. Quản Lý Hạ Tầng Ô Kho & Sơ Đồ
    // ==========================================

    public async Task<PagedResult<ManagerStorageUnitDto>> GetUnitsAsync(
        long facilityId,
        string? status,
        long? unitTypeId,
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

        var query = _dbContext.StorageUnits
            .AsNoTracking()
            .Include(u => u.UnitType)
            .Include(u => u.Area)
            .Where(u => u.FacilityId == facilityId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim().ToLowerInvariant();
            query = query.Where(u => u.PhysicalStatus.ToLower() == s);
        }

        if (unitTypeId.HasValue)
        {
            query = query.Where(u => u.UnitTypeId == unitTypeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(u => u.UnitCode.ToLower().Contains(term) ||
                                     (u.FloorLabel != null && u.FloorLabel.ToLower().Contains(term)) ||
                                     (u.ZoneLabel != null && u.ZoneLabel.ToLower().Contains(term)) ||
                                     (u.Notes != null && u.Notes.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(u => u.UnitCode)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new ManagerStorageUnitDto
            {
                Id = u.Id,
                FacilityId = u.FacilityId,
                UnitCode = u.UnitCode,
                UnitTypeId = u.UnitTypeId,
                UnitTypeCode = u.UnitType.Code,
                UnitTypeName = u.UnitType.Name,
                WidthM = u.UnitType.WidthM,
                LengthM = u.UnitType.LengthM,
                HeightM = u.UnitType.HeightM,
                AreaM2 = u.UnitType.AreaM2,
                VolumeM3 = u.UnitType.VolumeM3,
                AreaId = u.AreaId,
                AreaName = u.Area != null ? u.Area.Name : null,
                FloorLabel = u.FloorLabel,
                ZoneLabel = u.ZoneLabel,
                PhysicalStatus = u.PhysicalStatus,
                IsListed = u.IsListed,
                Notes = u.Notes,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ManagerStorageUnitDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<ManagerStorageUnitDto> CreateUnitAsync(
        long facilityId,
        ManagerCreateStorageUnitRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);

        var unitCode = request.UnitCode.Trim().ToUpperInvariant();
        var codeExists = await _dbContext.StorageUnits
            .AnyAsync(u => u.FacilityId == facilityId && u.UnitCode.ToUpper() == unitCode, cancellationToken);

        if (codeExists)
        {
            throw AppException.FromError(ManagerErrors.UnitCodeAlreadyExists);
        }

        var unitType = await _dbContext.UnitTypes
            .FirstOrDefaultAsync(ut => ut.Id == request.UnitTypeId, cancellationToken);
        if (unitType == null)
        {
            throw AppException.FromError(ManagerErrors.UnitTypeNotFound);
        }

        if (request.AreaId.HasValue)
        {
            var area = await _dbContext.FacilityAreas
                .FirstOrDefaultAsync(a => a.Id == request.AreaId.Value && a.FacilityId == facilityId, cancellationToken);
            if (area == null)
            {
                throw AppException.FromError(ManagerErrors.AreaNotFound);
            }
        }

        var status = !string.IsNullOrWhiteSpace(request.PhysicalStatus) 
            ? request.PhysicalStatus.Trim().ToLowerInvariant() 
            : StorageUnitStatusConstants.Available;

        var now = DateTimeOffset.UtcNow;
        var unit = new StorageUnit
        {
            FacilityId = facilityId,
            UnitCode = unitCode,
            UnitTypeId = request.UnitTypeId,
            AreaId = request.AreaId,
            FloorLabel = request.FloorLabel?.Trim(),
            ZoneLabel = request.ZoneLabel?.Trim(),
            PhysicalStatus = status,
            IsListed = request.IsListed,
            Notes = request.Notes?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.StorageUnits.Add(unit);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} created unit {UnitCode} at facility {FacilityId}.",
            currentUserId, unitCode, facilityId);

        return new ManagerStorageUnitDto
        {
            Id = unit.Id,
            FacilityId = unit.FacilityId,
            UnitCode = unit.UnitCode,
            UnitTypeId = unit.UnitTypeId,
            UnitTypeCode = unitType.Code,
            UnitTypeName = unitType.Name,
            WidthM = unitType.WidthM,
            LengthM = unitType.LengthM,
            HeightM = unitType.HeightM,
            AreaM2 = unitType.AreaM2,
            VolumeM3 = unitType.VolumeM3,
            AreaId = unit.AreaId,
            FloorLabel = unit.FloorLabel,
            ZoneLabel = unit.ZoneLabel,
            PhysicalStatus = unit.PhysicalStatus,
            IsListed = unit.IsListed,
            Notes = unit.Notes,
            CreatedAt = unit.CreatedAt,
            UpdatedAt = unit.UpdatedAt
        };
    }

    public async Task<ManagerStorageUnitDto> UpdateUnitAsync(
        long unitId,
        ManagerUpdateStorageUnitRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        var unit = await _dbContext.StorageUnits
            .Include(u => u.UnitType)
            .Include(u => u.Area)
            .FirstOrDefaultAsync(u => u.Id == unitId, cancellationToken);

        if (unit == null)
        {
            throw AppException.FromError(ManagerErrors.UnitNotFound);
        }

        await EnsureFacilityAccessAsync(unit.FacilityId, currentUserId, isSystemAdmin, cancellationToken);

        var newCode = request.UnitCode.Trim().ToUpperInvariant();
        if (!string.Equals(unit.UnitCode, newCode, StringComparison.OrdinalIgnoreCase))
        {
            var codeExists = await _dbContext.StorageUnits
                .AnyAsync(u => u.FacilityId == unit.FacilityId && u.UnitCode.ToUpper() == newCode && u.Id != unitId, cancellationToken);
            if (codeExists)
            {
                throw AppException.FromError(ManagerErrors.UnitCodeAlreadyExists);
            }
        }

        var unitType = await _dbContext.UnitTypes
            .FirstOrDefaultAsync(ut => ut.Id == request.UnitTypeId, cancellationToken);
        if (unitType == null)
        {
            throw AppException.FromError(ManagerErrors.UnitTypeNotFound);
        }

        if (request.AreaId.HasValue)
        {
            var area = await _dbContext.FacilityAreas
                .FirstOrDefaultAsync(a => a.Id == request.AreaId.Value && a.FacilityId == unit.FacilityId, cancellationToken);
            if (area == null)
            {
                throw AppException.FromError(ManagerErrors.AreaNotFound);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.PhysicalStatus))
        {
            var targetStatus = request.PhysicalStatus.Trim().ToLowerInvariant();
            if ((unit.PhysicalStatus == StorageUnitStatusConstants.Occupied || unit.PhysicalStatus == StorageUnitStatusConstants.Reserved)
                && targetStatus == StorageUnitStatusConstants.Available)
            {
                throw AppException.FromError(ManagerErrors.UnitOccupiedCannotModify);
            }
            unit.PhysicalStatus = targetStatus;
        }

        var now = DateTimeOffset.UtcNow;
        unit.UnitCode = newCode;
        unit.UnitTypeId = request.UnitTypeId;
        unit.AreaId = request.AreaId;
        unit.FloorLabel = request.FloorLabel?.Trim();
        unit.ZoneLabel = request.ZoneLabel?.Trim();
        unit.IsListed = request.IsListed;
        unit.Notes = request.Notes?.Trim();
        unit.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ManagerStorageUnitDto
        {
            Id = unit.Id,
            FacilityId = unit.FacilityId,
            UnitCode = unit.UnitCode,
            UnitTypeId = unit.UnitTypeId,
            UnitTypeCode = unitType.Code,
            UnitTypeName = unitType.Name,
            WidthM = unitType.WidthM,
            LengthM = unitType.LengthM,
            HeightM = unitType.HeightM,
            AreaM2 = unitType.AreaM2,
            VolumeM3 = unitType.VolumeM3,
            AreaId = unit.AreaId,
            AreaName = unit.Area?.Name,
            FloorLabel = unit.FloorLabel,
            ZoneLabel = unit.ZoneLabel,
            PhysicalStatus = unit.PhysicalStatus,
            IsListed = unit.IsListed,
            Notes = unit.Notes,
            CreatedAt = unit.CreatedAt,
            UpdatedAt = unit.UpdatedAt
        };
    }

    public async Task<ManagerMapLayoutDto> GetMapLayoutAsync(
        long facilityId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);

        var facility = await _dbContext.Facilities
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == facilityId, cancellationToken);
        if (facility == null)
        {
            throw AppException.FromError(ManagerErrors.FacilityNotFound);
        }

        var areas = await _dbContext.FacilityAreas
            .AsNoTracking()
            .Where(a => a.FacilityId == facilityId)
            .OrderBy(a => a.DisplayOrder)
            .Select(a => new ManagerAreaLayoutDto
            {
                AreaId = a.Id,
                Code = a.Code,
                Name = a.Name,
                AreaType = a.AreaType,
                DisplayOrder = a.DisplayOrder,
                MapMetadata = a.MapMetadata
            })
            .ToListAsync(cancellationToken);

        var unitPositions = await _dbContext.UnitMapPositions
            .AsNoTracking()
            .Include(p => p.Unit)
            .Where(p => p.Unit.FacilityId == facilityId)
            .Select(p => new ManagerUnitMapPositionDto
            {
                UnitId = p.UnitId,
                UnitCode = p.Unit.UnitCode,
                AreaId = p.AreaId,
                X = p.X,
                Y = p.Y,
                Width = p.Width,
                Height = p.Height,
                RotationDegrees = p.RotationDegrees,
                PhysicalStatus = p.Unit.PhysicalStatus,
                Metadata = p.Metadata
            })
            .ToListAsync(cancellationToken);

        return new ManagerMapLayoutDto
        {
            FacilityId = facilityId,
            FacilityName = facility.Name,
            Areas = areas,
            UnitPositions = unitPositions
        };
    }

    public async Task<int> UpdateMapLayoutAsync(
        long facilityId,
        ManagerUpdateMapLayoutRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);

        var count = 0;
        foreach (var pos in request.Positions)
        {
            var unit = await _dbContext.StorageUnits
                .FirstOrDefaultAsync(u => u.Id == pos.UnitId && u.FacilityId == facilityId, cancellationToken);
            if (unit == null) continue;

            var area = await _dbContext.FacilityAreas
                .FirstOrDefaultAsync(a => a.Id == pos.AreaId && a.FacilityId == facilityId, cancellationToken);
            if (area == null) continue;

            var existing = await _dbContext.UnitMapPositions
                .FirstOrDefaultAsync(p => p.UnitId == pos.UnitId, cancellationToken);

            if (existing != null)
            {
                existing.AreaId = pos.AreaId;
                existing.X = pos.X;
                existing.Y = pos.Y;
                existing.Width = pos.Width;
                existing.Height = pos.Height;
                existing.RotationDegrees = pos.RotationDegrees;
                existing.Metadata = string.IsNullOrWhiteSpace(pos.Metadata) ? ManagerConstants.DefaultMapMetadata : pos.Metadata;
            }
            else
            {
                _dbContext.UnitMapPositions.Add(new UnitMapPosition
                {
                    UnitId = pos.UnitId,
                    AreaId = pos.AreaId,
                    X = pos.X,
                    Y = pos.Y,
                    Width = pos.Width,
                    Height = pos.Height,
                    RotationDegrees = pos.RotationDegrees,
                    Metadata = string.IsNullOrWhiteSpace(pos.Metadata) ? ManagerConstants.DefaultMapMetadata : pos.Metadata
                });
            }
            count++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return count;
    }

    public async Task<IReadOnlyList<ManagerTransferRequestDto>> GetTransferRequestsAsync(
        long facilityId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);

        var requests = await _dbContext.UnitTransferRequests
            .AsNoTracking()
            .Include(r => r.Agreement)
                .ThenInclude(a => a.Customer)
                    .ThenInclude(c => c.User)
            .Include(r => r.FromUnit)
            .Include(r => r.ToUnit)
            .Include(r => r.RequestedUnitType)
            .Where(r => r.FromUnit.FacilityId == facilityId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ManagerTransferRequestDto
            {
                Id = r.Id,
                AgreementId = r.AgreementId,
                AgreementNo = r.Agreement.AgreementNo,
                CustomerId = r.Agreement.CustomerId,
                CustomerName = r.Agreement.Customer.FullName,
                CustomerPhone = r.Agreement.Customer.User.PhoneNumber ?? string.Empty,
                FromUnitId = r.FromUnitId,
                FromUnitCode = r.FromUnit.UnitCode,
                RequestedUnitTypeId = r.RequestedUnitTypeId,
                RequestedUnitTypeName = r.RequestedUnitType.Name,
                ToUnitId = r.ToUnitId,
                ToUnitCode = r.ToUnit != null ? r.ToUnit.UnitCode : null,
                RequestedEffectiveDate = r.RequestedEffectiveDate,
                Reason = r.Reason,
                Status = r.Status,
                RequestedBy = r.RequestedBy,
                ReviewedBy = r.ReviewedBy,
                ReviewedAt = r.ReviewedAt,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return requests;
    }

    public async Task<ManagerTransferRequestDto> DecideTransferRequestAsync(
        long id,
        ManagerDecideTransferRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _dbContext.UnitTransferRequests
            .Include(r => r.Agreement)
                .ThenInclude(a => a.Customer)
                    .ThenInclude(c => c.User)
            .Include(r => r.FromUnit)
            .Include(r => r.ToUnit)
            .Include(r => r.RequestedUnitType)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (transfer == null)
        {
            throw AppException.FromError(ManagerErrors.TransferRequestNotFound);
        }

        await EnsureFacilityAccessAsync(transfer.FromUnit.FacilityId, currentUserId, isSystemAdmin, cancellationToken);

        if (transfer.Status != TransferRequestStatusConstants.Pending)
        {
            throw AppException.FromError(ManagerErrors.TransferRequestNotPending);
        }

        var decision = request.Decision.Trim().ToLowerInvariant();
        if (!ApprovalDecisionConstants.All.Contains(decision))
        {
            throw AppException.FromError(ManagerErrors.InvalidDecision);
        }

        var now = DateTimeOffset.UtcNow;
        transfer.ReviewedBy = currentUserId;
        transfer.ReviewedAt = now;

        if (decision == ApprovalDecisionConstants.Approved)
        {
            transfer.Status = TransferRequestStatusConstants.Approved;
            if (request.TargetUnitId.HasValue)
            {
                var targetUnit = await _dbContext.StorageUnits
                    .FirstOrDefaultAsync(u => u.Id == request.TargetUnitId.Value && u.FacilityId == transfer.FromUnit.FacilityId, cancellationToken);
                if (targetUnit != null)
                {
                    transfer.ToUnitId = targetUnit.Id;
                }
            }
        }
        else
        {
            transfer.Status = TransferRequestStatusConstants.Rejected;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ManagerTransferRequestDto
        {
            Id = transfer.Id,
            AgreementId = transfer.AgreementId,
            AgreementNo = transfer.Agreement.AgreementNo,
            CustomerId = transfer.Agreement.CustomerId,
            CustomerName = transfer.Agreement.Customer.FullName,
            CustomerPhone = transfer.Agreement.Customer.User.PhoneNumber ?? string.Empty,
            FromUnitId = transfer.FromUnitId,
            FromUnitCode = transfer.FromUnit.UnitCode,
            RequestedUnitTypeId = transfer.RequestedUnitTypeId,
            RequestedUnitTypeName = transfer.RequestedUnitType.Name,
            ToUnitId = transfer.ToUnitId,
            ToUnitCode = transfer.ToUnit?.UnitCode,
            RequestedEffectiveDate = transfer.RequestedEffectiveDate,
            Reason = transfer.Reason,
            Status = transfer.Status,
            RequestedBy = transfer.RequestedBy,
            ReviewedBy = transfer.ReviewedBy,
            ReviewedAt = transfer.ReviewedAt,
            CreatedAt = transfer.CreatedAt
        };
    }

    // ==========================================
    // 2. Phân Ca Trực & Giao Việc Nhân Viên
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

        var exists = await _dbContext.ShiftAssignments
            .AnyAsync(sa => sa.ShiftId == request.ShiftId && sa.EmployeeId == request.EmployeeId, cancellationToken);
        if (exists)
        {
            throw AppException.FromError(ManagerErrors.ShiftAssignmentAlreadyExists);
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

    // ==========================================
    // 3. Phê Duyệt Tài Chính
    // ==========================================

    public async Task<IReadOnlyList<ManagerChargeProposalDto>> GetChargeProposalsAsync(
        long facilityId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);

        var proposals = await _dbContext.TicketChargeProposals
            .AsNoTracking()
            .Include(p => p.Ticket)
                .ThenInclude(t => t.Customer)
            .Include(p => p.Ticket)
                .ThenInclude(t => t.Agreement)
            .Include(p => p.ProposedByNavigation)
            .Include(p => p.TicketChargeApproval)
            .Where(p => p.Ticket.FacilityId == facilityId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ManagerChargeProposalDto
            {
                Id = p.Id,
                TicketId = p.TicketId,
                TicketTitle = p.Ticket.Subject,
                CustomerId = p.Ticket.CustomerId,
                CustomerName = p.Ticket.Customer.FullName,
                AgreementId = p.Ticket.AgreementId,
                AgreementNo = p.Ticket.Agreement != null ? p.Ticket.Agreement.AgreementNo : null,
                ProposedBy = p.ProposedBy,
                ProposedByStaffName = p.ProposedByNavigation.FullName,
                Description = p.Description,
                Amount = p.Amount,
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                Decision = p.TicketChargeApproval != null ? p.TicketChargeApproval.Decision : null,
                DecisionReason = p.TicketChargeApproval != null ? p.TicketChargeApproval.Reason : null,
                DecidedAt = p.TicketChargeApproval != null ? p.TicketChargeApproval.DecidedAt : null
            })
            .ToListAsync(cancellationToken);

        return proposals;
    }

    public async Task<ManagerChargeProposalDecisionDto> DecideChargeProposalAsync(
        long id,
        ManagerDecideChargeProposalRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        var proposal = await _dbContext.TicketChargeProposals
            .Include(p => p.Ticket)
            .Include(p => p.TicketChargeApproval)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (proposal == null)
        {
            throw AppException.FromError(ManagerErrors.ChargeProposalNotFound);
        }

        await EnsureFacilityAccessAsync(proposal.Ticket.FacilityId, currentUserId, isSystemAdmin, cancellationToken);

        if (proposal.Status != ProposalStatusConstants.Pending)
        {
            throw AppException.FromError(ManagerErrors.ChargeProposalAlreadyReviewed);
        }

        var decision = request.Decision.Trim().ToLowerInvariant();
        if (!ApprovalDecisionConstants.All.Contains(decision))
        {
            throw AppException.FromError(ManagerErrors.InvalidDecision);
        }

        var managerEmployeeId = await GetManagerEmployeeIdAsync(currentUserId, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var approval = new TicketChargeApproval
        {
            ProposalId = proposal.Id,
            Decision = decision,
            DecidedBy = managerEmployeeId,
            Reason = request.Reason?.Trim(),
            DecidedAt = now
        };
        _dbContext.TicketChargeApprovals.Add(approval);

        string? generatedInvoiceNo = null;
        long? generatedInvoiceId = null;

        if (decision == ApprovalDecisionConstants.Approved)
        {
            proposal.Status = ProposalStatusConstants.Approved;

            // Generate automatic invoice for damage charge
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            generatedInvoiceNo = $"{ManagerConstants.DefaultInvoicePrefix}{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

            var invoice = new Invoice
            {
                InvoiceNo = generatedInvoiceNo,
                CustomerId = proposal.Ticket.CustomerId,
                AgreementId = proposal.Ticket.AgreementId,
                TicketChargeProposalId = proposal.Id,
                IssueDate = today,
                DueDate = today.AddDays(ManagerConstants.DefaultInvoiceDueDays),
                Currency = PaymentConstants.CurrencyVnd,
                SubtotalAmount = proposal.Amount,
                TotalAmount = proposal.Amount,
                PaidAmount = 0m,
                Status = InvoiceStatusConstants.Open,
                CreatedAt = now,
                UpdatedAt = now
            };

            var invoiceLine = new InvoiceLine
            {
                Invoice = invoice,
                LineType = InvoiceLineTypeConstants.Rent == "damage" ? "damage" : "damage",
                Description = proposal.Description,
                Quantity = 1,
                UnitPrice = proposal.Amount,
                Metadata = ManagerConstants.DefaultActionDetails
            };

            invoice.InvoiceLines.Add(invoiceLine);
            _dbContext.Invoices.Add(invoice);

            await _dbContext.SaveChangesAsync(cancellationToken);
            generatedInvoiceId = invoice.Id;
        }
        else
        {
            proposal.Status = ProposalStatusConstants.Rejected;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new ManagerChargeProposalDecisionDto
        {
            ProposalId = proposal.Id,
            Decision = decision,
            GeneratedInvoiceNo = generatedInvoiceNo,
            GeneratedInvoiceId = generatedInvoiceId,
            Amount = proposal.Amount,
            DecidedAt = now
        };
    }

    public async Task<IReadOnlyList<ManagerRefundApprovalDto>> GetRefundApprovalsAsync(
        long facilityId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);

        var refunds = await _dbContext.Refunds
            .AsNoTracking()
            .Include(r => r.Agreement)
                .ThenInclude(a => a!.Customer)
            .Include(r => r.Agreement)
                .ThenInclude(a => a!.Invoices)
                    .ThenInclude(i => i.InvoiceLines)
            .Include(r => r.RefundApproval)
            .Where(r => r.Agreement != null && r.Agreement.FacilityId == facilityId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = refunds.Select(r =>
        {
            var deposit = r.Agreement?.DepositSnapshot ?? 0m;
            var unpaidDamages = r.Agreement?.Invoices
                .Where(i => i.Status != InvoiceStatusConstants.Paid)
                .SelectMany(i => i.InvoiceLines)
                .Where(l => l.LineType == "damage" || l.LineType == "penalty")
                .Sum(l => l.UnitPrice * l.Quantity) ?? 0m;

            var overdueDebt = r.Agreement?.Invoices
                .Where(i => i.Status == InvoiceStatusConstants.Overdue)
                .Sum(i => i.TotalAmount - i.PaidAmount) ?? 0m;

            var estimatedDeductions = unpaidDamages + overdueDebt;
            var recommendedRefund = Math.Max(0m, deposit - estimatedDeductions);

            return new ManagerRefundApprovalDto
            {
                RefundId = r.Id,
                PaymentId = r.PaymentId,
                AgreementId = r.AgreementId,
                AgreementNo = r.Agreement?.AgreementNo,
                CustomerId = r.Agreement?.CustomerId ?? 0,
                CustomerName = r.Agreement?.Customer.FullName ?? string.Empty,
                Amount = r.Amount,
                Reason = r.Reason,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                Decision = r.RefundApproval?.Decision,
                DecisionReason = r.RefundApproval?.Reason,
                DecidedAt = r.RefundApproval?.DecidedAt,
                OriginalDeposit = deposit,
                EstimatedDeductions = estimatedDeductions,
                RecommendedRefundAmount = recommendedRefund
            };
        }).ToList();

        return result;
    }

    public async Task<ManagerRefundDecisionDto> DecideRefundApprovalAsync(
        long id,
        ManagerDecideRefundApprovalRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        var refund = await _dbContext.Refunds
            .Include(r => r.Agreement)
            .Include(r => r.Agreement)
                .ThenInclude(a => a!.Invoices)
                    .ThenInclude(i => i.InvoiceLines)
            .Include(r => r.RefundApproval)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (refund == null)
        {
            throw AppException.FromError(ManagerErrors.RefundNotFound);
        }

        if (refund.Agreement != null)
        {
            await EnsureFacilityAccessAsync(refund.Agreement.FacilityId, currentUserId, isSystemAdmin, cancellationToken);
        }

        if (refund.RefundApproval != null)
        {
            throw AppException.FromError(ManagerErrors.RefundAlreadyReviewed);
        }

        var decision = request.Decision.Trim().ToLowerInvariant();
        if (!ApprovalDecisionConstants.All.Contains(decision))
        {
            throw AppException.FromError(ManagerErrors.InvalidDecision);
        }

        var managerEmployeeId = await GetManagerEmployeeIdAsync(currentUserId, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        decimal approvedAmount = 0m;
        if (decision == ApprovalDecisionConstants.Approved)
        {
            if (request.OverrideAmount.HasValue && request.OverrideAmount.Value >= 0)
            {
                approvedAmount = request.OverrideAmount.Value;
            }
            else
            {
                // Formula: Deposit - Damages - Overdue
                var deposit = refund.Agreement?.DepositSnapshot ?? refund.Amount;
                var unpaidDamages = refund.Agreement?.Invoices
                    .Where(i => i.Status != InvoiceStatusConstants.Paid)
                    .SelectMany(i => i.InvoiceLines)
                    .Where(l => l.LineType == "damage" || l.LineType == "penalty")
                    .Sum(l => l.UnitPrice * l.Quantity) ?? 0m;

                var overdueDebt = refund.Agreement?.Invoices
                    .Where(i => i.Status == InvoiceStatusConstants.Overdue)
                    .Sum(i => i.TotalAmount - i.PaidAmount) ?? 0m;

                approvedAmount = Math.Max(0m, deposit - (unpaidDamages + overdueDebt));
            }

            refund.Amount = approvedAmount;
            refund.Status = RefundStatusConstants.Approved;
        }
        else
        {
            refund.Status = RefundStatusConstants.Rejected;
        }

        var approval = new RefundApproval
        {
            RefundId = refund.Id,
            Decision = decision,
            DecidedBy = managerEmployeeId,
            Reason = request.Reason?.Trim(),
            DecidedAt = now
        };
        _dbContext.RefundApprovals.Add(approval);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ManagerRefundDecisionDto
        {
            RefundId = refund.Id,
            Decision = decision,
            ApprovedAmount = approvedAmount,
            DecidedAt = now
        };
    }

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

    public async Task<IReadOnlyList<ManagerDelinquencyCaseDto>> GetDelinquentCasesAsync(
        long facilityId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);

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

    // ==========================================
    // 5. Dashboard Cơ Sở
    // ==========================================

    public async Task<ManagerFacilityDashboardDto> GetFacilityDashboardAsync(
        long facilityId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);

        var facility = await _dbContext.Facilities
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == facilityId, cancellationToken);
        if (facility == null)
        {
            throw AppException.FromError(ManagerErrors.FacilityNotFound);
        }

        // Units and occupancy
        var units = await _dbContext.StorageUnits
            .AsNoTracking()
            .Where(u => u.FacilityId == facilityId)
            .ToListAsync(cancellationToken);

        var totalUnits = units.Count;
        var occupied = units.Count(u => u.PhysicalStatus == StorageUnitStatusConstants.Occupied);
        var reserved = units.Count(u => u.PhysicalStatus == StorageUnitStatusConstants.Reserved);
        var available = units.Count(u => u.PhysicalStatus == StorageUnitStatusConstants.Available);
        var maintenance = units.Count(u => u.PhysicalStatus == StorageUnitStatusConstants.Maintenance || 
                                           u.PhysicalStatus == StorageUnitStatusConstants.OutOfService || 
                                           u.PhysicalStatus == StorageUnitStatusConstants.PendingInspection);

        var occupancyRate = totalUnits > 0
            ? Math.Round((double)(occupied + reserved) / totalUnits * 100.0, 2)
            : 0.0;

        // Daily schedule
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayCheckIns = await _dbContext.Reservations
            .CountAsync(r => r.FacilityId == facilityId && r.StartDate == today, cancellationToken);

        var todayCheckOuts = await _dbContext.MoveOutRequests
            .CountAsync(m => m.Agreement.FacilityId == facilityId && 
                             m.RequestedMoveOutDate == today, cancellationToken);

        var todayInspections = await _dbContext.Inspections
            .CountAsync(i => i.FacilityId == facilityId && 
                             i.InspectedAt != null &&
                             DateOnly.FromDateTime(i.InspectedAt.Value.DateTime) == today, cancellationToken);

        // Support Tickets
        var openTickets = await _dbContext.SupportTickets
            .CountAsync(t => t.FacilityId == facilityId && t.Status != TicketStatusConstants.Resolved && t.Status != TicketStatusConstants.Closed, cancellationToken);

        var urgentTickets = await _dbContext.SupportTickets
            .CountAsync(t => t.FacilityId == facilityId && t.Priority == TicketPriorityConstants.Urgent && t.Status != TicketStatusConstants.Resolved && t.Status != TicketStatusConstants.Closed, cancellationToken);

        // Monthly revenue & Outstanding debt
        var now = DateTimeOffset.UtcNow;
        var firstDayThisMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var thisMonthRevenue = await _dbContext.Payments
            .AsNoTracking()
            .Where(p => p.TargetInvoice != null && 
                        (p.TargetInvoice.Agreement != null ? p.TargetInvoice.Agreement.FacilityId : (p.TargetInvoice.Reservation != null ? p.TargetInvoice.Reservation.FacilityId : 0)) == facilityId &&
                        p.Status == PaymentConstants.StatusSucceeded &&
                        (p.PaidAt >= firstDayThisMonth || (p.PaidAt == null && p.CreatedAt >= firstDayThisMonth)))
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var totalOutstandingDebt = await _dbContext.Invoices
            .AsNoTracking()
            .Where(i => (i.Agreement != null ? i.Agreement.FacilityId : (i.Reservation != null ? i.Reservation.FacilityId : 0)) == facilityId &&
                        i.Status != InvoiceStatusConstants.Paid &&
                        i.Status != InvoiceStatusConstants.Voided)
            .SumAsync(i => (decimal?)(i.TotalAmount - i.PaidAmount), cancellationToken) ?? 0m;

        return new ManagerFacilityDashboardDto
        {
            FacilityId = facility.Id,
            FacilityCode = facility.Code,
            FacilityName = facility.Name,
            TotalUnits = totalUnits,
            OccupiedUnits = occupied,
            ReservedUnits = reserved,
            AvailableUnits = available,
            MaintenanceUnits = maintenance,
            OccupancyRate = occupancyRate,
            DailySchedule = new ManagerDailyScheduleDto
            {
                TodayCheckInsCount = todayCheckIns,
                TodayCheckOutsCount = todayCheckOuts,
                TodayInspectionsCount = todayInspections
            },
            OpenTicketsCount = openTickets,
            UrgentTicketsCount = urgentTickets,
            ThisMonthRevenue = thisMonthRevenue,
            TotalOutstandingDebt = totalOutstandingDebt
        };
    }

    // ==========================================
    // HELPERS
    // ==========================================

    private async Task EnsureFacilityAccessAsync(
        long facilityId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken)
    {
        var facilityExists = await _dbContext.Facilities
            .AnyAsync(f => f.Id == facilityId, cancellationToken);

        if (!facilityExists)
        {
            throw AppException.FromError(ManagerErrors.FacilityNotFound);
        }

        if (isSystemAdmin)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var hasAccess = await _dbContext.StaffFacilityAssignments
            .AnyAsync(sfa => sfa.EmployeeId == currentUserId &&
                             sfa.FacilityId == facilityId &&
                             sfa.AssignmentRole == StaffAssignmentRoles.FacilityManager &&
                             (sfa.EndsAt == null || sfa.EndsAt > now),
                      cancellationToken);

        if (!hasAccess)
        {
            throw AppException.FromError(ManagerErrors.NotAssignedToFacility);
        }
    }

    private async Task<long> GetManagerEmployeeIdAsync(
        long currentUserId,
        CancellationToken cancellationToken)
    {
        var employee = await _dbContext.EmployeeProfiles
            .FirstOrDefaultAsync(ep => ep.UserId == currentUserId, cancellationToken);

        if (employee != null)
        {
            return employee.UserId;
        }

        // Check if user exists in Users table
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == currentUserId, cancellationToken);
        if (user == null)
        {
            throw AppException.FromError(ManagerErrors.ManagerEmployeeProfileNotFound);
        }

        // Auto-create employee profile for administrator performing manager duties if needed
        var now = DateTimeOffset.UtcNow;
        var newEmployee = new EmployeeProfile
        {
            UserId = user.Id,
            EmployeeCode = $"{AdminConstants.DefaultEmployeeCodePrefix}{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % AdminConstants.AutoEmployeeCodeModulo:D6}",
            FullName = user.Email,
            HireDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EmploymentStatus = EmploymentStatusConstants.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.EmployeeProfiles.Add(newEmployee);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return newEmployee.UserId;
    }
}
