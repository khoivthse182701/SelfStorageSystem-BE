using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
            if (targetStatus == "under_inspection") targetStatus = StorageUnitStatusConstants.PendingInspection;
            if (targetStatus == "under_maintenance" || targetStatus == "damaged") targetStatus = StorageUnitStatusConstants.Maintenance;
            if (targetStatus == "vacant") targetStatus = StorageUnitStatusConstants.Available;

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
            .Where(a => a.FacilityId == facilityId && a.AreaType == "zone")
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
            .Where(r => r.FromUnit.FacilityId == facilityId || r.RequestedFacilityId == facilityId)
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
                RequestedFacilityId = r.RequestedFacilityId,
                MoveOutDate = r.MoveOutDate,
                MoveInDate = r.MoveInDate,
                DiffRentAmount = r.DiffRentAmount,
                DiffDepositAmount = r.DiffDepositAmount,
                TotalPaidAmount = r.TotalPaidAmount,
                RescheduleCount = r.RescheduleCount,
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

    public async Task ValidateFacilityAccessAsync(
        long facilityId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);
    }

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

        // 1. Release old allocation and set old unit status
        activeAlloc.Status = AllocationStatusConstants.Released;
        activeAlloc.AllocationEndDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var oldUnitNewStatus = request.SetOldUnitUnderMaintenance
            ? StorageUnitStatusConstants.UnderMaintenanceValue
            : StorageUnitStatusConstants.Cleaning;

        var previousOldUnitStatus = oldUnit.PhysicalStatus;
        oldUnit.PhysicalStatus = oldUnitNewStatus;
        oldUnit.IsListed = false;
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

        // 2. Occupy new unit and allocate to agreement
        var previousNewUnitStatus = newUnit.PhysicalStatus;
        newUnit.PhysicalStatus = StorageUnitStatusConstants.Occupied;
        newUnit.IsListed = false;
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

        _dbContext.UnitAllocations.Add(new UnitAllocation
        {
            StorageUnitId = newUnit.Id,
            AgreementId = agreement.Id,
            AllocationKind = AllocationKindConstants.Rental,
            AllocationStartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            AllocationEndDate = agreement.EndDate,
            Status = AllocationStatusConstants.Active,
            CreatedAt = now
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

        agreement.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

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
            ? Math.Round((double)(occupied + reserved) / totalUnits * 100.0, 1)
            : 0.0;

        // Daily schedule
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayCheckIns = await _dbContext.Reservations
            .CountAsync(r => r.FacilityId == facilityId && 
                             (r.Status == ReservationStatusConstants.Confirmed || r.Status == ReservationStatusConstants.Pending), cancellationToken);

        var todayCheckOuts = await _dbContext.MoveOutRequests
            .CountAsync(m => m.Agreement.FacilityId == facilityId && 
                             (m.Status == MoveOutStatusConstants.Requested || m.Status == MoveOutStatusConstants.InspectionPending || m.Status == MoveOutStatusConstants.Scheduled || m.RequestedMoveOutDate == today), cancellationToken);

        var todayUtcDate = DateTime.UtcNow.Date;
        var todayStart = new DateTimeOffset(todayUtcDate, TimeSpan.Zero);
        var todayEnd = todayStart.AddDays(1);

        var todayInspections = await _dbContext.Inspections
            .CountAsync(i => i.FacilityId == facilityId && 
                             i.InspectedAt >= todayStart && 
                             i.InspectedAt < todayEnd, cancellationToken);

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
                        ((p.TargetInvoice.Agreement != null && p.TargetInvoice.Agreement.FacilityId == facilityId) || (p.TargetInvoice.Reservation != null && p.TargetInvoice.Reservation.FacilityId == facilityId)) &&
                        p.Status == PaymentConstants.StatusSucceeded &&
                        (p.PaidAt >= firstDayThisMonth || (p.PaidAt == null && p.CreatedAt >= firstDayThisMonth)))
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var totalOutstandingDebt = await _dbContext.Invoices
            .AsNoTracking()
            .Where(i => ((i.Agreement != null && i.Agreement.FacilityId == facilityId) || (i.Reservation != null && i.Reservation.FacilityId == facilityId)) &&
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
                             (sfa.AssignmentRole == StaffAssignmentRoles.FacilityManager ||
                              sfa.AssignmentRole == RoleConstants.FacilityManager ||
                              sfa.AssignmentRole.ToLower() == "manager" ||
                              sfa.AssignmentRole.ToLower() == "facility_manager") &&
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

    public async Task<List<ManagerFacilityScopeDto>> GetMyFacilitiesAsync(
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        if (isSystemAdmin)
        {
            return await _dbContext.Facilities
                .AsNoTracking()
                .OrderBy(f => f.Name)
                .Select(f => new ManagerFacilityScopeDto
                {
                    Id = f.Id,
                    Code = f.Code,
                    Name = f.Name,
                    AddressLine = f.AddressLine,
                    City = f.City,
                    Status = f.Status,
                    AssignmentRole = RoleConstants.SystemAdministrator
                })
                .ToListAsync(cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;
        var assigned = await _dbContext.StaffFacilityAssignments
            .AsNoTracking()
            .Include(sfa => sfa.Facility)
            .Where(sfa => sfa.EmployeeId == currentUserId &&
                          (sfa.AssignmentRole == StaffAssignmentRoles.FacilityManager ||
                           sfa.AssignmentRole == RoleConstants.FacilityManager ||
                           sfa.AssignmentRole.ToLower() == "manager" ||
                           sfa.AssignmentRole.ToLower() == "facility_manager") &&
                          (sfa.EndsAt == null || sfa.EndsAt > now))
            .OrderBy(sfa => sfa.Facility.Name)
            .Select(sfa => new ManagerFacilityScopeDto
            {
                Id = sfa.Facility.Id,
                Code = sfa.Facility.Code,
                Name = sfa.Facility.Name,
                AddressLine = sfa.Facility.AddressLine,
                City = sfa.Facility.City,
                Status = sfa.Facility.Status,
                AssignmentRole = StaffAssignmentRoles.FacilityManager
            })
            .ToListAsync(cancellationToken);

        return assigned;
    }

    // ==========================================
    // 6. Quản Lý Nhân Sự & Khách Hàng Cơ Sở (Facility Scope)
    // ==========================================

    public async Task<PagedResult<ManagerStaffItemDto>> GetFacilityStaffsAsync(
        long? facilityId,
        string? search,
        int pageNumber,
        int pageSize,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        var targetFacilityId = await ResolveAndValidateFacilityIdAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var query = _dbContext.StaffFacilityAssignments
            .AsNoTracking()
            .Include(sfa => sfa.Employee)
                .ThenInclude(e => e.User)
            .Include(sfa => sfa.Facility)
            .Where(sfa => sfa.FacilityId == targetFacilityId && (sfa.EndsAt == null || sfa.EndsAt > now));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(sfa => sfa.Employee.FullName.ToLower().Contains(s)
                || sfa.Employee.EmployeeCode.ToLower().Contains(s)
                || sfa.Employee.User.Email.ToLower().Contains(s)
                || (sfa.Employee.User.PhoneNumber != null && sfa.Employee.User.PhoneNumber.Contains(s)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(sfa => sfa.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(sfa => new ManagerStaffItemDto
            {
                UserId = sfa.Employee.UserId,
                EmployeeCode = sfa.Employee.EmployeeCode,
                FullName = sfa.Employee.FullName,
                Email = sfa.Employee.User.Email,
                PhoneNumber = sfa.Employee.User.PhoneNumber,
                HireDate = sfa.Employee.HireDate,
                EmploymentStatus = sfa.Employee.EmploymentStatus,
                UserStatus = sfa.Employee.User.Status,
                FacilityId = sfa.FacilityId,
                FacilityName = sfa.Facility.Name,
                AssignmentRole = sfa.AssignmentRole,
                CreatedAt = sfa.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ManagerStaffItemDto>(items, total, pageNumber, pageSize);
    }

    public async Task<ManagerStaffItemDto> CreateFacilityStaffAsync(
        ManagerCreateStaffRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        await ValidateFacilityAccessAsync(request.FacilityId, currentUserId, isSystemAdmin, cancellationToken);

        var email = request.Email.Trim().ToLowerInvariant();
        var emailExists = await _dbContext.Users.AnyAsync(u => u.Email.ToLower() == email, cancellationToken);
        if (emailExists)
        {
            throw AppException.FromError(ManagerErrors.DuplicateEmail);
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Email = email,
            PhoneNumber = request.PhoneNumber?.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Status = "active",
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var employeeCode = $"EMP-{now:yyMM}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
        var hireDate = request.HireDate ?? DateOnly.FromDateTime(now.DateTime);

        var employee = new EmployeeProfile
        {
            UserId = user.Id,
            EmployeeCode = employeeCode,
            FullName = request.FullName.Trim(),
            HireDate = hireDate,
            EmploymentStatus = "active",
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.EmployeeProfiles.Add(employee);

        var assignment = new StaffFacilityAssignment
        {
            EmployeeId = user.Id,
            FacilityId = request.FacilityId,
            AssignmentRole = string.IsNullOrWhiteSpace(request.AssignmentRole) ? RoleConstants.FacilityStaff : request.AssignmentRole.Trim(),
            StartsAt = now,
            AssignedBy = currentUserId,
            CreatedAt = now
        };
        _dbContext.StaffFacilityAssignments.Add(assignment);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var facility = await _dbContext.Facilities.FindAsync(new object[] { request.FacilityId }, cancellationToken);

        return new ManagerStaffItemDto
        {
            UserId = user.Id,
            EmployeeCode = employee.EmployeeCode,
            FullName = employee.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            HireDate = employee.HireDate,
            EmploymentStatus = employee.EmploymentStatus,
            UserStatus = user.Status,
            FacilityId = request.FacilityId,
            FacilityName = facility?.Name ?? string.Empty,
            AssignmentRole = assignment.AssignmentRole,
            CreatedAt = now
        };
    }

    public async Task<ManagerStaffItemDto> UpdateFacilityStaffAsync(
        long staffUserId,
        ManagerUpdateStaffRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var assignment = await _dbContext.StaffFacilityAssignments
            .Include(sfa => sfa.Facility)
            .Include(sfa => sfa.Employee)
                .ThenInclude(e => e.User)
            .FirstOrDefaultAsync(sfa => sfa.EmployeeId == staffUserId && (sfa.EndsAt == null || sfa.EndsAt > now), cancellationToken);

        if (assignment == null)
        {
            throw AppException.FromError(ManagerErrors.StaffNotFound);
        }

        await ValidateFacilityAccessAsync(assignment.FacilityId, currentUserId, isSystemAdmin, cancellationToken);

        var employee = assignment.Employee;
        employee.FullName = request.FullName.Trim();
        if (request.HireDate.HasValue) employee.HireDate = request.HireDate.Value;
        if (!string.IsNullOrWhiteSpace(request.EmploymentStatus)) employee.EmploymentStatus = request.EmploymentStatus.Trim();
        employee.UpdatedAt = now;

        if (request.PhoneNumber != null)
        {
            employee.User.PhoneNumber = request.PhoneNumber.Trim();
            employee.User.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ManagerStaffItemDto
        {
            UserId = employee.UserId,
            EmployeeCode = employee.EmployeeCode,
            FullName = employee.FullName,
            Email = employee.User.Email,
            PhoneNumber = employee.User.PhoneNumber,
            HireDate = employee.HireDate,
            EmploymentStatus = employee.EmploymentStatus,
            UserStatus = employee.User.Status,
            FacilityId = assignment.FacilityId,
            FacilityName = assignment.Facility?.Name ?? string.Empty,
            AssignmentRole = assignment.AssignmentRole,
            CreatedAt = assignment.CreatedAt
        };
    }

    public async Task<ManagerStaffItemDto> UpdateFacilityStaffStatusAsync(
        long staffUserId,
        ManagerUpdateStaffStatusRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var assignment = await _dbContext.StaffFacilityAssignments
            .Include(sfa => sfa.Facility)
            .Include(sfa => sfa.Employee)
                .ThenInclude(e => e.User)
            .FirstOrDefaultAsync(sfa => sfa.EmployeeId == staffUserId, cancellationToken);

        if (assignment == null)
        {
            throw AppException.FromError(ManagerErrors.StaffNotFound);
        }

        await ValidateFacilityAccessAsync(assignment.FacilityId, currentUserId, isSystemAdmin, cancellationToken);

        var isActive = request.Status.Equals("active", StringComparison.OrdinalIgnoreCase);
        assignment.Employee.User.Status = isActive ? "active" : "inactive";
        assignment.Employee.User.UpdatedAt = now;

        assignment.Employee.EmploymentStatus = isActive ? "active" : "terminated";
        assignment.Employee.UpdatedAt = now;

        if (!isActive)
        {
            assignment.EndsAt = now;
        }
        else if (assignment.EndsAt.HasValue && assignment.EndsAt <= now)
        {
            assignment.EndsAt = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ManagerStaffItemDto
        {
            UserId = assignment.Employee.UserId,
            EmployeeCode = assignment.Employee.EmployeeCode,
            FullName = assignment.Employee.FullName,
            Email = assignment.Employee.User.Email,
            PhoneNumber = assignment.Employee.User.PhoneNumber,
            HireDate = assignment.Employee.HireDate,
            EmploymentStatus = assignment.Employee.EmploymentStatus,
            UserStatus = assignment.Employee.User.Status,
            FacilityId = assignment.FacilityId,
            FacilityName = assignment.Facility?.Name ?? string.Empty,
            AssignmentRole = assignment.AssignmentRole,
            CreatedAt = assignment.CreatedAt
        };
    }

    public async Task<PagedResult<ManagerCustomerItemDto>> GetFacilityCustomersAsync(
        long? facilityId,
        string? search,
        int pageNumber,
        int pageSize,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        var targetFacilityId = await ResolveAndValidateFacilityIdAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);

        var agreementCustomers = _dbContext.RentalAgreements
            .Where(a => a.FacilityId == targetFacilityId)
            .Select(a => a.CustomerId);

        var reservationCustomers = _dbContext.Reservations
            .Where(r => r.FacilityId == targetFacilityId)
            .Select(r => r.CustomerId);

        var customerIdsQuery = agreementCustomers.Union(reservationCustomers);

        var customersQuery = _dbContext.CustomerProfiles
            .AsNoTracking()
            .Include(cp => cp.User)
            .Include(cp => cp.RentalAgreements)
            .Include(cp => cp.Reservations)
            .Where(cp => customerIdsQuery.Contains(cp.UserId));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            customersQuery = customersQuery.Where(cp => cp.FullName.ToLower().Contains(s)
                || cp.User.Email.ToLower().Contains(s)
                || (cp.User.PhoneNumber != null && cp.User.PhoneNumber.Contains(s)));
        }

        var total = await customersQuery.CountAsync(cancellationToken);
        var items = await customersQuery
            .OrderByDescending(cp => cp.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(cp => new ManagerCustomerItemDto
            {
                CustomerId = cp.UserId,
                FullName = cp.FullName,
                Email = cp.User.Email,
                PhoneNumber = cp.User.PhoneNumber,
                TotalAgreements = cp.RentalAgreements.Count(a => a.FacilityId == targetFacilityId),
                ActiveAgreements = cp.RentalAgreements.Count(a => a.FacilityId == targetFacilityId && (a.Status == "active" || a.Status == "expiring_soon")),
                TotalReservations = cp.Reservations.Count(r => r.FacilityId == targetFacilityId),
                FirstActivityAt = cp.CreatedAt,
                LastActivityAt = cp.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ManagerCustomerItemDto>(items, total, pageNumber, pageSize);
    }

    // ==========================================
    // 7. Quản Lý Sự Cố Ticket & Phân Công Kiểm Tra Hết Hạn
    // ==========================================

    public async Task<PagedResult<ManagerTicketDetailDto>> GetFacilityTicketsAsync(
        long? facilityId,
        string? status,
        string? category,
        int pageNumber,
        int pageSize,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        var targetFacilityId = await ResolveAndValidateFacilityIdAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);

        var query = _dbContext.SupportTickets
            .AsNoTracking()
            .Include(t => t.Facility)
            .Include(t => t.Customer)
            .Include(t => t.StorageUnit)
            .Include(t => t.Agreement)
            .Include(t => t.TicketAssignment)
                .ThenInclude(ta => ta!.Employee)
            .Where(t => t.FacilityId == targetFacilityId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim().ToLowerInvariant();
            query = query.Where(t => t.Status.ToLower() == s);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var c = category.Trim().ToLowerInvariant();
            query = query.Where(t => t.Category.ToLower() == c);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new ManagerTicketDetailDto
            {
                Id = t.Id,
                TicketNo = t.TicketNo,
                FacilityId = t.FacilityId,
                FacilityName = t.Facility.Name,
                CustomerId = t.CustomerId,
                CustomerName = t.Customer.FullName,
                AgreementId = t.AgreementId,
                AgreementNo = t.Agreement != null ? t.Agreement.AgreementNo : null,
                StorageUnitId = t.StorageUnitId,
                UnitCode = t.StorageUnit != null ? t.StorageUnit.UnitCode : null,
                Category = t.Category,
                Priority = t.Priority,
                Subject = t.Subject,
                Description = t.Description,
                Status = t.Status,
                AssignedEmployeeId = t.TicketAssignment != null ? (long?)t.TicketAssignment.EmployeeId : null,
                AssignedEmployeeName = t.TicketAssignment != null ? t.TicketAssignment.Employee.FullName : null,
                AssignedAt = t.TicketAssignment != null ? (DateTimeOffset?)t.TicketAssignment.AssignedAt : null,
                Resolution = t.Resolution,
                ResolvedAt = t.ResolvedAt,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ManagerTicketDetailDto>(items, total, pageNumber, pageSize);
    }

    public async Task<ManagerTicketDetailDto> AssignTicketStaffAsync(
        long ticketId,
        ManagerAssignTicketStaffRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _dbContext.SupportTickets
            .Include(t => t.Facility)
            .Include(t => t.Customer)
            .Include(t => t.StorageUnit)
            .Include(t => t.Agreement)
            .Include(t => t.TicketAssignment)
                .ThenInclude(ta => ta!.Employee)
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket == null)
            throw AppException.FromError(ManagerErrors.TicketNotFound);

        await ValidateFacilityAccessAsync(ticket.FacilityId, currentUserId, isSystemAdmin, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var staffAssigned = await _dbContext.StaffFacilityAssignments
            .AnyAsync(sfa => sfa.EmployeeId == request.EmployeeId && sfa.FacilityId == ticket.FacilityId && (sfa.EndsAt == null || sfa.EndsAt > now), cancellationToken);

        if (!staffAssigned)
            throw AppException.FromError(ManagerErrors.StaffNotAssignedToFacility);

        if (ticket.TicketAssignment == null)
        {
            ticket.TicketAssignment = new TicketAssignment
            {
                TicketId = ticket.Id,
                EmployeeId = request.EmployeeId,
                AssignedBy = currentUserId,
                AssignedAt = now
            };
            _dbContext.TicketAssignments.Add(ticket.TicketAssignment);
        }
        else
        {
            ticket.TicketAssignment.EmployeeId = request.EmployeeId;
            ticket.TicketAssignment.AssignedBy = currentUserId;
            ticket.TicketAssignment.AssignedAt = now;
            ticket.TicketAssignment.EndedAt = null;
        }

        ticket.Status = TicketStatusConstants.Assigned;
        ticket.UpdatedAt = now;

        var staffTask = new StaffTask
        {
            FacilityId = ticket.FacilityId,
            AssignedEmployeeId = request.EmployeeId,
            TicketId = ticket.Id,
            TaskType = "support",
            Title = $"[Sự cố #{ticket.TicketNo}] {ticket.Subject}",
            Status = "todo",
            CreatedBy = currentUserId,
            DueAt = request.DueAt ?? now.AddHours(24),
            ProgressPercent = 0,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.StaffTasks.Add(staffTask);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var employee = await _dbContext.EmployeeProfiles.FindAsync(new object[] { request.EmployeeId }, cancellationToken);

        return new ManagerTicketDetailDto
        {
            Id = ticket.Id,
            TicketNo = ticket.TicketNo,
            FacilityId = ticket.FacilityId,
            FacilityName = ticket.Facility.Name,
            CustomerId = ticket.CustomerId,
            CustomerName = ticket.Customer.FullName,
            AgreementId = ticket.AgreementId,
            AgreementNo = ticket.Agreement?.AgreementNo,
            StorageUnitId = ticket.StorageUnitId,
            UnitCode = ticket.StorageUnit?.UnitCode,
            Category = ticket.Category,
            Priority = ticket.Priority,
            Subject = ticket.Subject,
            Description = ticket.Description,
            Status = ticket.Status,
            AssignedEmployeeId = request.EmployeeId,
            AssignedEmployeeName = employee?.FullName,
            AssignedAt = now,
            Resolution = ticket.Resolution,
            ResolvedAt = ticket.ResolvedAt,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt
        };
    }

    public async Task<IReadOnlyList<ManagerAgreementDetailDto>> GetExpiringOrMoveOutAgreementsAsync(
        long facilityId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        await ValidateFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.DateTime);
        var upcomingThreshold = today.AddDays(14);

        var agreements = await _dbContext.RentalAgreements
            .AsNoTracking()
            .Include(a => a.Customer)
                .ThenInclude(c => c.User)
            .Include(a => a.MoveOutRequest)
            .Include(a => a.UnitAllocations)
                .ThenInclude(ua => ua.StorageUnit)
            .Include(a => a.Invoices)
            .Where(a => a.FacilityId == facilityId &&
                (a.Status == "active" || a.Status == "expiring_soon" || a.Status == "overdue") &&
                (a.EndDate <= upcomingThreshold || (a.MoveOutRequest != null && (a.MoveOutRequest.Status == "pending" || a.MoveOutRequest.Status == "approved"))))
            .OrderBy(a => a.EndDate)
            .ToListAsync(cancellationToken);

        return agreements.Select(a =>
        {
            var activeAllocation = a.UnitAllocations.FirstOrDefault(ua => ua.Status == "active");
            return new ManagerAgreementDetailDto
            {
                Id = a.Id,
                AgreementNo = a.AgreementNo,
                CustomerId = a.CustomerId,
                CustomerName = a.Customer.FullName,
                CustomerPhone = a.Customer.User.PhoneNumber,
                CustomerEmail = a.Customer.User.Email,
                StorageUnitId = activeAllocation?.StorageUnitId ?? 0,
                UnitCode = activeAllocation?.StorageUnit?.UnitCode ?? string.Empty,
                StartDate = a.StartDate,
                EndDate = a.EndDate,
                MonthlyRent = a.MonthlyRateSnapshot,
                DepositAmount = a.DepositSnapshot,
                Status = a.Status,
                TotalDebt = a.Invoices.Where(i => i.Status == InvoiceStatusConstants.Overdue).Sum(i => i.TotalAmount - i.PaidAmount)
            };
        }).ToList();
    }

    public async Task<ManagerInspectionDetailDto> AssignMoveOutInspectionAsync(
        ManagerAssignInspectionRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        var agreement = await _dbContext.RentalAgreements
            .Include(a => a.Facility)
            .Include(a => a.UnitAllocations)
                .ThenInclude(ua => ua.StorageUnit)
            .FirstOrDefaultAsync(a => a.Id == request.AgreementId, cancellationToken);

        if (agreement == null)
            throw AppException.FromError(ManagerErrors.AgreementNotFound);

        await ValidateFacilityAccessAsync(agreement.FacilityId, currentUserId, isSystemAdmin, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var staffAssigned = await _dbContext.StaffFacilityAssignments
            .AnyAsync(sfa => sfa.EmployeeId == request.EmployeeId && sfa.FacilityId == agreement.FacilityId && (sfa.EndsAt == null || sfa.EndsAt > now), cancellationToken);

        if (!staffAssigned)
            throw AppException.FromError(ManagerErrors.StaffNotAssignedToFacility);

        var activeUnit = agreement.UnitAllocations.FirstOrDefault(ua => ua.Status == "active")?.StorageUnit;

        var inspection = new Inspection
        {
            FacilityId = agreement.FacilityId,
            AgreementId = agreement.Id,
            StorageUnitId = activeUnit?.Id,
            InspectedBy = request.EmployeeId,
            InspectionType = "check_out",
            Status = InspectionStatusConstants.Draft,
            Summary = request.Notes,
            CreatedAt = now
        };
        _dbContext.Inspections.Add(inspection);

        agreement.Status = RentalAgreementStatusConstants.MoveOutScheduled;
        if (agreement.MoveOutRequest != null)
        {
            agreement.MoveOutRequest.Status = MoveOutStatusConstants.InspectionPending;
            agreement.MoveOutRequest.UpdatedAt = now;
        }
        agreement.UpdatedAt = now;

        var staffTask = new StaffTask
        {
            FacilityId = agreement.FacilityId,
            AssignedEmployeeId = request.EmployeeId,
            TaskType = "inspection",
            Title = $"[Nghiệm thu trả kho HĐ #{agreement.AgreementNo}] Ô kho {activeUnit?.UnitCode ?? ""}",
            Status = "todo",
            CreatedBy = currentUserId,
            DueAt = request.ScheduledAt ?? now.AddHours(24),
            ProgressPercent = 0,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.StaffTasks.Add(staffTask);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var employee = await _dbContext.EmployeeProfiles.FindAsync(new object[] { request.EmployeeId }, cancellationToken);

        return new ManagerInspectionDetailDto
        {
            Id = inspection.Id,
            FacilityId = inspection.FacilityId,
            FacilityName = agreement.Facility.Name,
            AgreementId = agreement.Id,
            AgreementNo = agreement.AgreementNo,
            StorageUnitId = activeUnit?.Id,
            UnitCode = activeUnit?.UnitCode,
            InspectedBy = request.EmployeeId,
            InspectedByName = employee?.FullName ?? string.Empty,
            InspectionType = inspection.InspectionType,
            Status = inspection.Status,
            Summary = inspection.Summary,
            CreatedAt = now
        };
    }

    private async Task<long> ResolveAndValidateFacilityIdAsync(
        long? facilityId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken)
    {
        if (facilityId.HasValue)
        {
            await ValidateFacilityAccessAsync(facilityId.Value, currentUserId, isSystemAdmin, cancellationToken);
            return facilityId.Value;
        }

        var facilities = await GetMyFacilitiesAsync(currentUserId, isSystemAdmin, cancellationToken);
        if (facilities.Count == 0)
        {
            throw AppException.FromError(ManagerErrors.NotAssignedToFacility);
        }
        return facilities[0].Id;
    }

    private static (string? BankName, string? AccountNumber, string? AccountHolder, string? Note) ExtractBankInfo(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (null, null, null, null);
        try
        {
            if (reason.TrimStart().StartsWith("{"))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(reason);
                var root = doc.RootElement;
                var bank = root.TryGetProperty("bank", out var b) ? b.GetString() : null;
                var acc = root.TryGetProperty("acc", out var a) ? a.GetString() : null;
                var holder = root.TryGetProperty("holder", out var h) ? h.GetString() : null;
                var note = root.TryGetProperty("note", out var n) ? n.GetString() : null;
                return (bank, acc, holder, note);
            }
        }
        catch { }

        var bankMatch = System.Text.RegularExpressions.Regex.Match(reason, @"(?:Ngân hàng|BANK)[:s]+([^|]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var accMatch = System.Text.RegularExpressions.Regex.Match(reason, @"(?:STK|ACC)[:s]+([^|]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var holderMatch = System.Text.RegularExpressions.Regex.Match(reason, @"(?:Chủ TK|NAME|HOLDER)[:s]+([^|]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var noteMatch = System.Text.RegularExpressions.Regex.Match(reason, @"(?:Ghi chú|NOTE)[:s]+([^|]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return (
            bankMatch.Success ? bankMatch.Groups[1].Value.Trim() : null,
            accMatch.Success ? accMatch.Groups[1].Value.Trim() : null,
            holderMatch.Success ? holderMatch.Groups[1].Value.Trim() : null,
            noteMatch.Success ? noteMatch.Groups[1].Value.Trim() : null
        );
    }

    public async Task<PagedResult<ManagerRefundItemDto>> GetFacilityRefundsAsync(
        long? facilityId,
        string? status,
        int pageNumber,
        int pageSize,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        var targetFacilityId = await ResolveAndValidateFacilityIdAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);

        var query = _dbContext.Refunds
            .AsNoTracking()
            .Include(r => r.Agreement)
                .ThenInclude(a => a!.Customer)
                    .ThenInclude(c => c.User)
            .Include(r => r.Agreement)
                .ThenInclude(a => a!.Facility)
            .Include(r => r.Agreement)
                .ThenInclude(a => a!.Inspections)
                    .ThenInclude(i => i.InspectionItems)
            .Include(r => r.RefundApproval)
                .ThenInclude(ra => ra!.DecidedByNavigation)
            .Where(r => r.Agreement != null && r.Agreement.FacilityId == targetFacilityId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToLowerInvariant();
            query = query.Where(r => r.Status == normalizedStatus);
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var refunds = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = refunds.Select(r =>
        {
            var agreement = r.Agreement;
            var deposit = agreement?.DepositSnapshot ?? 0m;
            var latestInspection = agreement?.Inspections.OrderByDescending(i => i.InspectedAt ?? i.CreatedAt).FirstOrDefault();
            var damageCost = latestInspection?.InspectionItems.Sum(ii => ii.ChargeAmount) ?? Math.Max(0m, deposit - r.Amount);

            var (bankName, accNumber, holder, note) = ExtractBankInfo(r.Reason);

            string? proofImageUrl = null;
            string? approvalNote = note;
            string? transactionRef = r.ProviderRefundId;

            if (!string.IsNullOrWhiteSpace(r.RefundApproval?.Reason))
            {
                try
                {
                    if (r.RefundApproval.Reason.TrimStart().StartsWith("{"))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(r.RefundApproval.Reason);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("proofImageUrl", out var p)) proofImageUrl = p.GetString();
                        if (root.TryGetProperty("note", out var n)) approvalNote = n.GetString();
                        if (root.TryGetProperty("transactionRef", out var t) && string.IsNullOrEmpty(transactionRef)) transactionRef = t.GetString();
                    }
                    else
                    {
                        approvalNote = r.RefundApproval.Reason;
                    }
                }
                catch { }
            }

            return new ManagerRefundItemDto
            {
                RefundId = r.Id,
                AgreementId = r.AgreementId,
                AgreementNo = agreement?.AgreementNo,
                FacilityId = agreement?.FacilityId ?? targetFacilityId,
                FacilityName = agreement?.Facility?.Name ?? string.Empty,
                CustomerId = agreement?.CustomerId ?? r.RequestedBy ?? 0,
                CustomerName = agreement?.Customer?.FullName ?? string.Empty,
                CustomerPhone = agreement?.Customer?.User?.PhoneNumber ?? string.Empty,
                CustomerEmail = agreement?.Customer?.User?.Email ?? string.Empty,
                DepositAmount = deposit,
                DamageCost = damageCost,
                RefundAmount = r.Amount,
                Currency = r.Currency,
                Status = r.Status,
                PayoutMethod = r.Provider,
                BankName = bankName,
                BankAccountNumber = accNumber,
                BankAccountHolder = holder,
                TransactionReference = transactionRef,
                ProofImageUrl = proofImageUrl,
                ProcessedByUserId = r.RefundApproval?.DecidedBy,
                ProcessedByUserName = r.RefundApproval?.DecidedByNavigation?.FullName,
                ProcessedAt = r.RefundApproval?.DecidedAt ?? r.RefundedAt,
                Note = approvalNote,
                CreatedAt = r.CreatedAt,
                RefundedAt = r.RefundedAt
            };
        }).ToList();

        return new PagedResult<ManagerRefundItemDto>(items, totalItems, pageNumber, pageSize);
    }

    public async Task<ManagerProcessRefundPayoutResultDto> ProcessRefundPayoutAsync(
        long refundId,
        ManagerProcessRefundPayoutRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        var refund = await _dbContext.Refunds
            .Include(r => r.Agreement)
                .ThenInclude(a => a!.Customer)
                    .ThenInclude(c => c.User)
            .Include(r => r.Agreement)
                .ThenInclude(a => a!.Facility)
            .Include(r => r.Agreement)
                .ThenInclude(a => a!.MoveOutRequest)
            .Include(r => r.RefundApproval)
            .FirstOrDefaultAsync(r => r.Id == refundId, cancellationToken);

        if (refund == null)
            throw AppException.FromError(ManagerErrors.RefundNotFound);

        if (refund.Agreement == null)
            throw AppException.FromError(ManagerErrors.AgreementNotFound);

        await ValidateFacilityAccessAsync(refund.Agreement.FacilityId, currentUserId, isSystemAdmin, cancellationToken);

        if (refund.Amount <= 0m)
            throw AppException.FromError(ManagerErrors.RefundAmountInvalid);

        if (refund.Status == RefundStatusConstants.Completed)
            throw AppException.FromError(ManagerErrors.RefundAlreadyCompleted);

        var now = DateTimeOffset.UtcNow;

        refund.Status = RefundStatusConstants.Completed;
        refund.Provider = RefundPayoutMethodConstants.BankTransfer;
        refund.ProviderRefundId = request.TransactionReference.Trim();
        refund.RefundedAt = now;
        refund.UpdatedAt = now;

        var approvalReason = System.Text.Json.JsonSerializer.Serialize(new
        {
            payoutMethod = RefundPayoutMethodConstants.BankTransfer,
            transactionRef = request.TransactionReference.Trim(),
            proofImageUrl = request.ProofImageUrl?.Trim(),
            note = request.Note?.Trim()
        });

        if (refund.RefundApproval == null)
        {
            refund.RefundApproval = new RefundApproval
            {
                RefundId = refund.Id,
                Decision = "completed",
                DecidedBy = currentUserId,
                Reason = approvalReason,
                DecidedAt = now
            };
            _dbContext.RefundApprovals.Add(refund.RefundApproval);
        }
        else
        {
            refund.RefundApproval.Decision = "completed";
            refund.RefundApproval.DecidedBy = currentUserId;
            refund.RefundApproval.Reason = approvalReason;
            refund.RefundApproval.DecidedAt = now;
        }

        // Complete Agreement & MoveOutRequest
        refund.Agreement.Status = RentalAgreementStatusConstants.Completed;
        refund.Agreement.DepositBalance = 0m;
        refund.Agreement.UpdatedAt = now;

        if (refund.Agreement.MoveOutRequest != null)
        {
            refund.Agreement.MoveOutRequest.Status = MoveOutStatusConstants.Completed;
            refund.Agreement.MoveOutRequest.UpdatedAt = now;
        }

        // Notify Customer
        var customerUserId = refund.Agreement.Customer?.UserId;
        if (customerUserId.HasValue)
        {
            var (bankName, accNumber, _, _) = ExtractBankInfo(refund.Reason);
            var bankTarget = !string.IsNullOrEmpty(accNumber) ? $"{bankName} ({accNumber})" : "tài khoản ngân hàng của bạn";
            _dbContext.Notifications.Add(new Notification
            {
                UserId = customerUserId.Value,
                Channel = "in_app",
                TemplateCode = "REFUND_PAYOUT_COMPLETED",
                Subject = $"[Hoàn cọc thành công #{refund.Id}] HĐ #{refund.Agreement.AgreementNo}",
                Payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    refundId = refund.Id,
                    agreementId = refund.AgreementId,
                    agreementNo = refund.Agreement.AgreementNo,
                    amount = refund.Amount,
                    transactionReference = request.TransactionReference,
                    payoutMethod = "bank_transfer",
                    message = $"Quản lý cơ sở {refund.Agreement.Facility?.Name} đã chuyển khoản hoàn tất số tiền cọc {refund.Amount:N0} VND vào {bankTarget}. Mã giao dịch: {request.TransactionReference}."
                }),
                Status = "pending",
                ScheduledAt = now,
                CreatedAt = now
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ManagerProcessRefundPayoutResultDto
        {
            RefundId = refund.Id,
            AgreementId = refund.AgreementId,
            Amount = refund.Amount,
            Status = refund.Status,
            PayoutMethod = refund.Provider,
            TransactionReference = request.TransactionReference,
            ProofImageUrl = request.ProofImageUrl,
            ProcessedByUserId = currentUserId,
            ProcessedAt = now,
            Message = "Đã xác nhận hoàn tất chuyển khoản tiền cọc cho khách hàng."
        };
    }
}
