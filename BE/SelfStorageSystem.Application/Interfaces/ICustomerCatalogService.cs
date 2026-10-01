using SelfStorageSystem.Contracts.Customer.Facilities;

namespace SelfStorageSystem.Application.Interfaces;

public interface ICustomerCatalogService
{
    Task<IReadOnlyList<FacilitySummaryDto>> GetFacilitiesAsync(CancellationToken cancellationToken = default);
    Task<FacilityDetailDto?> GetFacilityAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UnitTypeDto>> GetUnitTypesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AvailableUnitDto>> GetAvailableUnitsAsync(long? facilityId, long? unitTypeId, long? facilityAreaId, CancellationToken cancellationToken = default);
    Task<PricingCalculationDto> CalculatePricingAsync(CalculatePricingRequest request, CancellationToken cancellationToken = default);
}
