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

    public async Task DeleteUnitAsync(
        long unitId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        var unit = await _dbContext.StorageUnits
            .Include(u => u.UnitAllocations)
            .Include(u => u.UnitMapPosition)
            .Include(u => u.UnitStatusHistories)
            .FirstOrDefaultAsync(u => u.Id == unitId, cancellationToken);

        if (unit == null)
        {
            throw AppException.FromError(ManagerErrors.UnitNotFound);
        }

        await EnsureFacilityAccessAsync(unit.FacilityId, currentUserId, isSystemAdmin, cancellationToken);

        if (unit.PhysicalStatus == StorageUnitStatusConstants.Occupied ||
            unit.PhysicalStatus == StorageUnitStatusConstants.Reserved ||
            unit.UnitAllocations.Any())
        {
            throw AppException.FromError(ManagerErrors.UnitCannotDeleteHasRentals);
        }

        if (unit.UnitStatusHistories.Any())
        {
            _dbContext.UnitStatusHistories.RemoveRange(unit.UnitStatusHistories);
        }

        if (unit.UnitMapPosition != null)
        {
            _dbContext.UnitMapPositions.Remove(unit.UnitMapPosition);
        }

        _dbContext.StorageUnits.Remove(unit);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} deleted storage unit {UnitCode} (Id: {UnitId}) at facility {FacilityId}.",
            currentUserId, unit.UnitCode, unitId, unit.FacilityId);
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



    public async Task<ManagerTransferRequestDto> DecideTransferRequestAsync(

        long transferRequestId,

        ManagerDecideTransferRequest request,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        var transferReq = await _dbContext.UnitTransferRequests

            .Include(r => r.Agreement)

                .ThenInclude(a => a.Customer)

                    .ThenInclude(c => c.User)

            .Include(r => r.FromUnit)

            .Include(r => r.ToUnit)

            .Include(r => r.RequestedUnitType)

            .FirstOrDefaultAsync(r => r.Id == transferRequestId, cancellationToken);



        if (transferReq == null)

            throw new AppException(Error.NotFound("TransferRequest.NotFound", "Không tìm thấy yêu cầu chuyển kho."));



        await EnsureFacilityAccessAsync(transferReq.FromUnit.FacilityId, currentUserId, isSystemAdmin, cancellationToken);



        var decision = request.Decision.Trim().ToLowerInvariant();

        var now = DateTimeOffset.UtcNow;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);



        if (decision == "approved")

        {

            StorageUnit? targetUnit = null;

            if (request.TargetUnitId.HasValue && request.TargetUnitId.Value > 0)

            {

                targetUnit = await _dbContext.StorageUnits

                    .Include(u => u.UnitType)

                    .FirstOrDefaultAsync(u => u.Id == request.TargetUnitId.Value, cancellationToken);



                if (targetUnit == null)

                    throw new AppException(Error.NotFound("StorageUnit.NotFound", "Không tìm thấy ô kho chỉ định."));



                if (targetUnit.FacilityId != transferReq.RequestedFacilityId && targetUnit.FacilityId != transferReq.FromUnit.FacilityId)

                    throw new AppException(Error.Validation("StorageUnit.DifferentFacility", "Ô kho chỉ định không thuộc cơ sở chuyển đổi."));



                if (!string.Equals(targetUnit.PhysicalStatus, StorageUnitStatusConstants.Available, StringComparison.OrdinalIgnoreCase))

                    throw new AppException(Error.Validation("StorageUnit.NotAvailable", "Ô kho chỉ định hiện không sẵn sàng (Available)."));

            }

            else

            {

                targetUnit = await _dbContext.StorageUnits

                    .Include(u => u.UnitType)

                    .Where(u => (u.FacilityId == transferReq.RequestedFacilityId || u.FacilityId == transferReq.FromUnit.FacilityId)

                                && u.UnitTypeId == transferReq.RequestedUnitTypeId

                                && u.PhysicalStatus.ToLower() == StorageUnitStatusConstants.Available

                                && u.Id != transferReq.FromUnitId)

                    .FirstOrDefaultAsync(cancellationToken);



                if (targetUnit == null)

                {

                    targetUnit = await _dbContext.StorageUnits

                        .Include(u => u.UnitType)

                        .Where(u => (u.FacilityId == transferReq.RequestedFacilityId || u.FacilityId == transferReq.FromUnit.FacilityId)

                                    && u.PhysicalStatus.ToLower() == StorageUnitStatusConstants.Available

                                    && u.Id != transferReq.FromUnitId)

                        .FirstOrDefaultAsync(cancellationToken);

                }



                if (targetUnit == null)

                    throw new AppException(Error.Validation("StorageUnit.NoneAvailable", "Không còn ô kho trống phù hợp tại cơ sở để thực hiện chuyển đổi."));

            }



            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);



            var activeAlloc = await _dbContext.UnitAllocations

                .Where(ua => ua.AgreementId == transferReq.AgreementId && ua.Status == AllocationStatusConstants.Active)

                .OrderByDescending(ua => ua.Id)

                .FirstOrDefaultAsync(cancellationToken);



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



            var oldUnit = transferReq.FromUnit;

            if (oldUnit != null)

            {

                if (oldUnit.PhysicalStatus != StorageUnitStatusConstants.Maintenance &&

                    oldUnit.PhysicalStatus != StorageUnitStatusConstants.UnderMaintenanceValue)

                {

                    oldUnit.PhysicalStatus = StorageUnitStatusConstants.Maintenance;

                }

                oldUnit.UpdatedAt = now;

            }



            targetUnit.PhysicalStatus = StorageUnitStatusConstants.Occupied;

            targetUnit.UpdatedAt = now;



            var newAllocStartDate = today;

            var agreement = transferReq.Agreement;

            var newAllocEndDate = agreement.EndDate > newAllocStartDate ? agreement.EndDate : newAllocStartDate.AddDays(1);



            _dbContext.UnitAllocations.Add(new UnitAllocation

            {

                StorageUnitId = targetUnit.Id,

                AgreementId = agreement.Id,

                AllocationKind = AllocationKindConstants.Rental,

                AllocationStartDate = newAllocStartDate,

                AllocationEndDate = newAllocEndDate,

                Status = AllocationStatusConstants.Active,

                CreatedAt = now,

                EndedAt = null

            });



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



            transferReq.Status = TransferRequestStatusConstants.Completed;

            transferReq.ToUnitId = targetUnit.Id;

            transferReq.ReviewedBy = currentUserId;

            transferReq.ReviewedAt = now;

            transferReq.CompletedAt = now;

            transferReq.Reason = !string.IsNullOrWhiteSpace(request.ReviewNotes)

                ? $"{transferReq.Reason} | Phê duyệt: {request.ReviewNotes}"

                : transferReq.Reason;



            await _dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);



            transferReq.ToUnit = targetUnit;

        }

        else

        {

            transferReq.Status = "rejected";

            transferReq.ReviewedBy = currentUserId;

            transferReq.ReviewedAt = now;

            transferReq.Reason = !string.IsNullOrWhiteSpace(request.ReviewNotes)

                ? $"{transferReq.Reason} | Từ chối: {request.ReviewNotes}"

                : transferReq.Reason;



            await _dbContext.SaveChangesAsync(cancellationToken);

        }



        return new ManagerTransferRequestDto

        {

            Id = transferReq.Id,

            AgreementId = transferReq.AgreementId,

            AgreementNo = transferReq.Agreement.AgreementNo,

            CustomerId = transferReq.Agreement.CustomerId,

            CustomerName = transferReq.Agreement.Customer.FullName,

            CustomerPhone = transferReq.Agreement.Customer.User.PhoneNumber ?? string.Empty,

            FromUnitId = transferReq.FromUnitId,

            FromUnitCode = transferReq.FromUnit.UnitCode,

            RequestedUnitTypeId = transferReq.RequestedUnitTypeId,

            RequestedUnitTypeName = transferReq.RequestedUnitType.Name,

            RequestedFacilityId = transferReq.RequestedFacilityId,

            MoveOutDate = transferReq.MoveOutDate,

            MoveInDate = transferReq.MoveInDate,

            DiffRentAmount = transferReq.DiffRentAmount,

            DiffDepositAmount = transferReq.DiffDepositAmount,

            TotalPaidAmount = transferReq.TotalPaidAmount,

            RescheduleCount = transferReq.RescheduleCount,

            ToUnitId = transferReq.ToUnitId,

            ToUnitCode = transferReq.ToUnit != null ? transferReq.ToUnit.UnitCode : null,

            RequestedEffectiveDate = transferReq.RequestedEffectiveDate,

            Reason = transferReq.Reason,

            Status = transferReq.Status,

            RequestedBy = transferReq.RequestedBy,

            ReviewedBy = transferReq.ReviewedBy,

            ReviewedAt = transferReq.ReviewedAt,

            CreatedAt = transferReq.CreatedAt

        };

    }



    public async Task ValidateFacilityAccessAsync(

        long facilityId,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);

    }
}
