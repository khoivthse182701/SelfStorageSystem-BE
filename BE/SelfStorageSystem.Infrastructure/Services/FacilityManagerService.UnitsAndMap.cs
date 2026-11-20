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
using SelfStorageSystem.Infrastructure.Common;
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



        int totalCount;
        List<StorageUnit> rawUnits;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(u => u.UnitCode.ToLower().Contains(term) ||
                                     (u.FloorLabel != null && u.FloorLabel.ToLower().Contains(term)) ||
                                     (u.ZoneLabel != null && u.ZoneLabel.ToLower().Contains(term)) ||
                                     (u.Notes != null && u.Notes.ToLower().Contains(term)));

            var candidates = await query
                .OrderBy(u => u.UnitCode)
                .ToListAsync(cancellationToken);

            var filteredUnits = candidates.Where(u =>
                u.UnitCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (u.FloorLabel != null && u.FloorLabel.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (u.ZoneLabel != null && u.ZoneLabel.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (ExtractNotesAndImageUrl(u.Notes).CleanNotes is { Length: > 0 } cn &&
                 cn.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            totalCount = filteredUnits.Count;
            rawUnits = filteredUnits
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }
        else
        {
            totalCount = await query.CountAsync(cancellationToken);
            rawUnits = await query
                .OrderBy(u => u.UnitCode)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        var items = rawUnits.Select(u =>
        {
            var (cleanNotes, imageUrl) = ExtractNotesAndImageUrl(u.Notes);
            var len = u.CustomLengthM ?? u.UnitType.LengthM;
            var wid = u.CustomWidthM ?? u.UnitType.WidthM;
            var hei = u.CustomHeightM ?? u.UnitType.HeightM;
            var area = u.CustomAreaM2 ?? (u.CustomLengthM.HasValue && u.CustomWidthM.HasValue ? u.CustomLengthM.Value * u.CustomWidthM.Value : u.UnitType.AreaM2);
            var category = !string.IsNullOrWhiteSpace(u.StorageCategory) ? u.StorageCategory : u.UnitType.Name;
            var finalImg = !string.IsNullOrWhiteSpace(u.ImageUrl) ? u.ImageUrl : imageUrl;

            return new ManagerStorageUnitDto
            {
                Id = u.Id,
                FacilityId = u.FacilityId,
                UnitCode = u.UnitCode,
                UnitTypeId = u.UnitTypeId,
                UnitTypeCode = u.UnitType.Code,
                UnitTypeName = category,
                WidthM = wid,
                LengthM = len,
                HeightM = hei,
                AreaM2 = area,
                VolumeM3 = u.UnitType.VolumeM3 ?? (area * hei),
                CustomWidthM = u.CustomWidthM,
                CustomLengthM = u.CustomLengthM,
                CustomHeightM = u.CustomHeightM,
                CustomAreaM2 = u.CustomAreaM2,
                StorageCategory = category,
                CategoryName = category,
                CustomMonthlyRate = u.CustomMonthlyRate,
                BasePricePerSqm = u.BasePricePerSqm,
                CategorySurcharge = u.CategorySurcharge,
                AreaId = u.AreaId,
                AreaName = u.Area != null ? u.Area.Name : null,
                FloorLabel = u.FloorLabel,
                ZoneLabel = u.ZoneLabel,
                PhysicalStatus = u.PhysicalStatus,
                IsListed = u.IsListed,
                Notes = cleanNotes,
                ImageUrl = finalImg,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            };
        }).ToList();



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



        var unitType = request.UnitTypeId > 0
            ? await _dbContext.UnitTypes.FirstOrDefaultAsync(ut => ut.Id == request.UnitTypeId, cancellationToken)
            : await _dbContext.UnitTypes.OrderBy(ut => ut.Id).FirstOrDefaultAsync(cancellationToken);

        if (unitType == null)
        {
            unitType = await _dbContext.UnitTypes.OrderBy(ut => ut.Id).FirstOrDefaultAsync(cancellationToken)
                ?? throw AppException.FromError(ManagerErrors.UnitTypeNotFound);
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

        var len = request.CustomLengthM ?? request.LengthM ?? unitType.LengthM;
        var wid = request.CustomWidthM ?? request.WidthM ?? unitType.WidthM;
        var hei = request.CustomHeightM ?? request.HeightM ?? unitType.HeightM;
        var areaM2 = request.CustomAreaM2 ?? (len * wid);
        var catName = !string.IsNullOrWhiteSpace(request.StorageCategory)
            ? request.StorageCategory.Trim()
            : (!string.IsNullOrWhiteSpace(request.CategoryName) ? request.CategoryName.Trim() : unitType.Name);

        var canonicalCat = StorageCategoryClassifier.ClassifyCanonicalGroup(catName);
        if (!StorageCategoryClassifier.IsUnitTypeMatchingCategory(unitType.Code, unitType.Name, canonicalCat))
        {
            var allTypes = await _dbContext.UnitTypes.Where(t => t.IsActive).ToListAsync(cancellationToken);
            var bestTypeId = StorageCategoryClassifier.FindBestMatchingUnitTypeId(allTypes, catName, areaM2);
            var resolvedType = allTypes.FirstOrDefault(t => t.Id == bestTypeId);
            if (resolvedType != null)
            {
                unitType = resolvedType;
            }
        }

        decimal basePricePerSqm = 100m;
        var pricingConfig = await _dbContext.PricingConfigs.OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        if (pricingConfig != null && pricingConfig.PricePerSquareMeter > 0)
        {
            basePricePerSqm = pricingConfig.PricePerSquareMeter;
        }

        decimal categorySurcharge = 0m;
        var matchedCat = await _dbContext.StorageCategories
            .FirstOrDefaultAsync(c => c.Name.ToLower() == catName.ToLower(), cancellationToken);
        if (matchedCat != null)
        {
            categorySurcharge = matchedCat.SurchargeAmount;
        }

        var monthlyRate = (request.CustomMonthlyRate.HasValue && request.CustomMonthlyRate.Value > 0)
            ? request.CustomMonthlyRate.Value
            : (areaM2 * basePricePerSqm + categorySurcharge);

        var (createdCleanNotes, extractedImg) = ExtractNotesAndImageUrl(request.Notes);
        var finalImg = !string.IsNullOrWhiteSpace(request.ImageUrl) ? request.ImageUrl.Trim() : extractedImg;

        var unit = new StorageUnit
        {
            FacilityId = facilityId,
            UnitCode = unitCode,
            UnitTypeId = unitType.Id,
            AreaId = request.AreaId,
            FloorLabel = request.FloorLabel?.Trim(),
            ZoneLabel = request.ZoneLabel?.Trim(),
            PhysicalStatus = status,
            IsListed = request.IsListed,
            Notes = PackNotesAndImageUrl(createdCleanNotes, finalImg),
            ImageUrl = finalImg,
            CustomLengthM = len,
            CustomWidthM = wid,
            CustomHeightM = hei,
            CustomAreaM2 = areaM2,
            StorageCategory = catName,
            CustomMonthlyRate = monthlyRate,
            BasePricePerSqm = basePricePerSqm,
            CategorySurcharge = categorySurcharge,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.StorageUnits.Add(unit);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} created unit {UnitCode} at facility {FacilityId} with custom size {Len}x{Wid} ({Area}m2), category {Cat}, monthly rate {Rate}.",
            currentUserId, unitCode, facilityId, len, wid, areaM2, catName, monthlyRate);

        return new ManagerStorageUnitDto
        {
            Id = unit.Id,
            FacilityId = unit.FacilityId,
            UnitCode = unit.UnitCode,
            UnitTypeId = unit.UnitTypeId,
            UnitTypeCode = unitType.Code,
            UnitTypeName = catName,
            WidthM = wid,
            LengthM = len,
            HeightM = hei,
            AreaM2 = areaM2,
            VolumeM3 = (areaM2 * hei),
            CustomWidthM = wid,
            CustomLengthM = len,
            CustomHeightM = hei,
            CustomAreaM2 = areaM2,
            StorageCategory = catName,
            CategoryName = catName,
            CustomMonthlyRate = monthlyRate,
            BasePricePerSqm = basePricePerSqm,
            CategorySurcharge = categorySurcharge,
            AreaId = unit.AreaId,
            FloorLabel = unit.FloorLabel,
            ZoneLabel = unit.ZoneLabel,
            PhysicalStatus = unit.PhysicalStatus,
            IsListed = unit.IsListed,
            Notes = createdCleanNotes,
            ImageUrl = finalImg,
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



        var newCode = string.IsNullOrWhiteSpace(request.UnitCode)
            ? unit.UnitCode
            : request.UnitCode.Trim().ToUpperInvariant();

        if (!string.Equals(unit.UnitCode, newCode, StringComparison.OrdinalIgnoreCase))
        {
            var codeExists = await _dbContext.StorageUnits
                .AnyAsync(u => u.FacilityId == unit.FacilityId && u.UnitCode.ToUpper() == newCode && u.Id != unitId, cancellationToken);
            if (codeExists)
            {
                throw AppException.FromError(ManagerErrors.UnitCodeAlreadyExists);
            }
        }

        var targetUnitTypeId = (request.UnitTypeId.HasValue && request.UnitTypeId.Value > 0)
            ? request.UnitTypeId.Value
            : unit.UnitTypeId;

        var unitType = await _dbContext.UnitTypes
            .FirstOrDefaultAsync(ut => ut.Id == targetUnitTypeId, cancellationToken);
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
        unit.UnitTypeId = targetUnitTypeId;
        if (request.AreaId.HasValue) unit.AreaId = request.AreaId;
        if (request.FloorLabel != null) unit.FloorLabel = request.FloorLabel.Trim();
        if (request.ZoneLabel != null) unit.ZoneLabel = request.ZoneLabel.Trim();
        if (request.IsListed.HasValue) unit.IsListed = request.IsListed.Value;

        if (request.CustomLengthM.HasValue || request.LengthM.HasValue)
        {
            unit.CustomLengthM = request.CustomLengthM ?? request.LengthM;
        }
        if (request.CustomWidthM.HasValue || request.WidthM.HasValue)
        {
            unit.CustomWidthM = request.CustomWidthM ?? request.WidthM;
        }
        if (request.CustomHeightM.HasValue || request.HeightM.HasValue)
        {
            unit.CustomHeightM = request.CustomHeightM ?? request.HeightM;
        }

        var effectiveLen = unit.CustomLengthM ?? unitType.LengthM;
        var effectiveWid = unit.CustomWidthM ?? unitType.WidthM;
        var effectiveHei = unit.CustomHeightM ?? unitType.HeightM;
        var effectiveArea = request.CustomAreaM2 ?? (unit.CustomLengthM.HasValue && unit.CustomWidthM.HasValue ? unit.CustomLengthM.Value * unit.CustomWidthM.Value : (effectiveLen * effectiveWid));
        unit.CustomAreaM2 = effectiveArea;

        if (!string.IsNullOrWhiteSpace(request.StorageCategory))
        {
            unit.StorageCategory = request.StorageCategory.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(request.CategoryName))
        {
            unit.StorageCategory = request.CategoryName.Trim();
        }

        var effectiveCat = !string.IsNullOrWhiteSpace(unit.StorageCategory) ? unit.StorageCategory : unitType.Name;

        var canonicalEffectiveCat = StorageCategoryClassifier.ClassifyCanonicalGroup(effectiveCat);
        if (!StorageCategoryClassifier.IsUnitTypeMatchingCategory(unitType.Code, unitType.Name, canonicalEffectiveCat))
        {
            var allTypes = await _dbContext.UnitTypes.Where(t => t.IsActive).ToListAsync(cancellationToken);
            var bestTypeId = StorageCategoryClassifier.FindBestMatchingUnitTypeId(allTypes, effectiveCat, effectiveArea);
            var resolvedType = allTypes.FirstOrDefault(t => t.Id == bestTypeId);
            if (resolvedType != null)
            {
                unitType = resolvedType;
                unit.UnitTypeId = resolvedType.Id;
            }
        }

        // Recalculate price if requested or not yet set
        if (request.CustomMonthlyRate.HasValue && request.CustomMonthlyRate.Value > 0)
        {
            unit.CustomMonthlyRate = request.CustomMonthlyRate.Value;
        }
        else
        {
            decimal basePricePerSqm = unit.BasePricePerSqm ?? 100m;
            var pricingConfig = await _dbContext.PricingConfigs.OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
            if (pricingConfig != null && pricingConfig.PricePerSquareMeter > 0)
            {
                basePricePerSqm = pricingConfig.PricePerSquareMeter;
            }
            unit.BasePricePerSqm = basePricePerSqm;

            decimal categorySurcharge = unit.CategorySurcharge ?? 0m;
            var matchedCat = await _dbContext.StorageCategories
                .FirstOrDefaultAsync(c => c.Name.ToLower() == effectiveCat.ToLower(), cancellationToken);
            if (matchedCat != null)
            {
                categorySurcharge = matchedCat.SurchargeAmount;
            }
            unit.CategorySurcharge = categorySurcharge;
            unit.CustomMonthlyRate = (effectiveArea * basePricePerSqm + categorySurcharge);
        }

        if (request.Notes != null || request.ImageUrl != null)
        {
            var (existingCleanNotes, existingImg) = ExtractNotesAndImageUrl(unit.Notes);
            var (reqCleanNotes, reqExtractedImg) = request.Notes != null
                ? ExtractNotesAndImageUrl(request.Notes)
                : (existingCleanNotes, null);

            var targetNotes = request.Notes != null ? reqCleanNotes : existingCleanNotes;
            var targetImg = request.ImageUrl != null
                ? (string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim())
                : (reqExtractedImg ?? existingImg);

            unit.Notes = PackNotesAndImageUrl(targetNotes, targetImg);
            unit.ImageUrl = targetImg;
        }
        unit.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var (updatedCleanNotes, updatedImageUrl) = ExtractNotesAndImageUrl(unit.Notes);
        var finalUpdatedImg = !string.IsNullOrWhiteSpace(unit.ImageUrl) ? unit.ImageUrl : updatedImageUrl;
        return new ManagerStorageUnitDto
        {
            Id = unit.Id,
            FacilityId = unit.FacilityId,
            UnitCode = unit.UnitCode,
            UnitTypeId = unit.UnitTypeId,
            UnitTypeCode = unitType.Code,
            UnitTypeName = effectiveCat,
            WidthM = effectiveWid,
            LengthM = effectiveLen,
            HeightM = effectiveHei,
            AreaM2 = effectiveArea,
            VolumeM3 = (effectiveArea * effectiveHei),
            CustomWidthM = unit.CustomWidthM,
            CustomLengthM = unit.CustomLengthM,
            CustomHeightM = unit.CustomHeightM,
            CustomAreaM2 = unit.CustomAreaM2,
            StorageCategory = effectiveCat,
            CategoryName = effectiveCat,
            CustomMonthlyRate = unit.CustomMonthlyRate,
            BasePricePerSqm = unit.BasePricePerSqm,
            CategorySurcharge = unit.CategorySurcharge,
            AreaId = unit.AreaId,
            AreaName = unit.Area?.Name,
            FloorLabel = unit.FloorLabel,
            ZoneLabel = unit.ZoneLabel,
            PhysicalStatus = unit.PhysicalStatus,
            IsListed = unit.IsListed,
            Notes = updatedCleanNotes,
            ImageUrl = finalUpdatedImg,
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

        var hasLinkedData = await _dbContext.SupportTickets.AnyAsync(t => t.StorageUnitId == unitId, cancellationToken)
            || await _dbContext.Inspections.AnyAsync(i => i.StorageUnitId == unitId, cancellationToken)
            || await _dbContext.MaintenanceWorkOrders.AnyAsync(m => m.StorageUnitId == unitId, cancellationToken)
            || await _dbContext.UnitTransferRequests.AnyAsync(ut => ut.FromUnitId == unitId || ut.ToUnitId == unitId, cancellationToken);

        if (hasLinkedData)
        {
            throw AppException.FromError(ManagerErrors.UnitCannotDeleteHasHistory);
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



        var currentStatus = (transferReq.Status ?? string.Empty).Trim().ToLowerInvariant();
        if (currentStatus is TransferRequestStatusConstants.Completed or TransferRequestStatusConstants.Rejected or TransferRequestStatusConstants.Cancelled)
            throw AppException.FromError(ManagerErrors.TransferRequestNotPending);

        var decision = (request.Decision ?? string.Empty).Trim().ToLowerInvariant();
        if (!ApprovalDecisionConstants.All.Contains(decision))
            throw AppException.FromError(ManagerErrors.InvalidDecision);

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

            if (targetUnit.FacilityId != transferReq.FromUnit.FacilityId)
            {
                await EnsureFacilityAccessAsync(targetUnit.FacilityId, currentUserId, isSystemAdmin, cancellationToken);
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
            var prevOldStatus = oldUnit?.PhysicalStatus;

            if (oldUnit != null)

            {

                if (oldUnit.PhysicalStatus != StorageUnitStatusConstants.Maintenance &&

                    oldUnit.PhysicalStatus != StorageUnitStatusConstants.UnderMaintenanceValue)

                {

                    oldUnit.PhysicalStatus = StorageUnitStatusConstants.Maintenance;

                }

                oldUnit.UpdatedAt = now;

            }



            if (oldUnit != null)
            {
                _dbContext.UnitStatusHistories.Add(new UnitStatusHistory
                {
                    StorageUnitId = oldUnit.Id,
                    OldStatus = prevOldStatus,
                    NewStatus = oldUnit.PhysicalStatus,
                    Reason = $"Manager approved unit transfer request #{transferReq.Id} for agreement #{transferReq.Agreement.AgreementNo}",
                    ChangedBy = currentUserId,
                    ChangedAt = now
                });
            }

            var agreement = transferReq.Agreement;
            List<SupportTicket>? linkedTickets = null;
            if (agreement.FacilityId != targetUnit.FacilityId)
            {
                linkedTickets = await _dbContext.SupportTickets
                    .Where(t => t.AgreementId == agreement.Id && t.FacilityId != targetUnit.FacilityId)
                    .ToListAsync(cancellationToken);
                foreach (var ticket in linkedTickets)
                {
                    ticket.AgreementId = null;
                }
            }

            // Step 1: Persist old allocation release and temporary ticket detach first inside the transaction
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Step 2: Update Agreement FacilityId & snapshots and persist before adding new UnitAllocation
            agreement.FacilityId = targetUnit.FacilityId;
            if (transferReq.DiffDepositAmount > 0)
            {
                agreement.DepositSnapshot += transferReq.DiffDepositAmount;
                agreement.DepositBalance += transferReq.DiffDepositAmount;
            }
            var targetRate = await _dbContext.FacilityRates
                .AsNoTracking()
                .Where(r => r.FacilityId == targetUnit.FacilityId && r.UnitTypeId == targetUnit.UnitTypeId && r.ValidFrom <= today && (r.ValidTo == null || r.ValidTo >= today))
                .OrderByDescending(r => r.ValidFrom)
                .FirstOrDefaultAsync(cancellationToken);
            if (targetRate != null)
            {
                agreement.MonthlyRateSnapshot = targetRate.MonthlyRate;
            }
            agreement.UpdatedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Step 3: Occupy target unit, insert new UnitAllocation, update credentials & transfer status
            var prevTargetStatus = targetUnit.PhysicalStatus;
            targetUnit.PhysicalStatus = StorageUnitStatusConstants.Occupied;
            _dbContext.UnitStatusHistories.Add(new UnitStatusHistory
            {
                StorageUnitId = targetUnit.Id,
                OldStatus = prevTargetStatus,
                NewStatus = StorageUnitStatusConstants.Occupied,
                Reason = $"Manager approved unit transfer request #{transferReq.Id} from unit {oldUnit?.UnitCode} for agreement #{transferReq.Agreement.AgreementNo}",
                ChangedBy = currentUserId,
                ChangedAt = now
            });
            targetUnit.UpdatedAt = now;

            var newAllocStartDate = today < agreement.StartDate ? agreement.StartDate : today;
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

            if (linkedTickets is { Count: > 0 })
            {
                foreach (var ticket in linkedTickets)
                {
                    ticket.AgreementId = agreement.Id;
                    ticket.FacilityId = targetUnit.FacilityId;
                    if (ticket.StorageUnitId != null)
                    {
                        ticket.StorageUnitId = targetUnit.Id;
                    }
                    ticket.UpdatedAt = now;
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

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



    private static (string? CleanNotes, string? ImageUrl) ExtractNotesAndImageUrl(string? rawNotes)
    {
        if (string.IsNullOrWhiteSpace(rawNotes))
        {
            return (rawNotes, null);
        }

        var match = System.Text.RegularExpressions.Regex.Match(rawNotes, @"\[IMG:([^\]]+)\]");
        if (match.Success)
        {
            var imageUrl = match.Groups[1].Value.Trim();
            var cleanNotes = System.Text.RegularExpressions.Regex.Replace(rawNotes, @"\[IMG:[^\]]+\]\s*", string.Empty).Trim();
            return (cleanNotes, imageUrl);
        }

        return (rawNotes, null);
    }

    private static string? PackNotesAndImageUrl(string? rawNotes, string? imageUrl)
    {
        var (cleanNotes, embeddedImg) = ExtractNotesAndImageUrl(rawNotes);
        var finalImg = !string.IsNullOrWhiteSpace(imageUrl) ? imageUrl.Trim() : embeddedImg;
        var trimmedNotes = cleanNotes?.Trim();

        if (!string.IsNullOrWhiteSpace(finalImg))
        {
            return string.IsNullOrWhiteSpace(trimmedNotes)
                ? $"[IMG:{finalImg}]"
                : $"[IMG:{finalImg}] {trimmedNotes}";
        }

        return string.IsNullOrWhiteSpace(trimmedNotes) ? null : trimmedNotes;
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
