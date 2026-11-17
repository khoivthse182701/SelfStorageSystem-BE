using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Facilities;

namespace SelfStorageSystem.Application.Interfaces;

public interface ICustomerCatalogService
{
    Task<IReadOnlyList<FacilitySummaryDto>> GetFacilitiesAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<FacilitySummaryDto>> GetFacilitiesPagedAsync(string? city = null, string? search = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<FacilityDetailDto?> GetFacilityAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UnitTypeDto>> GetUnitTypesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AvailableUnitDto>> GetAvailableUnitsAsync(long? facilityId, long? unitTypeId, long? facilityAreaId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AvailableUnitDto>> GetAvailableUnitsAsync(long facilityId, long? unitTypeId, long? areaId, DateOnly? startDate, int durationMonths = 1, CancellationToken cancellationToken = default);
    Task<PricingCalculationDto> CalculatePricingAsync(CalculatePricingRequest request, CancellationToken cancellationToken = default);
}
