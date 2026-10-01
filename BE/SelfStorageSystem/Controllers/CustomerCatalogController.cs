using Microsoft.AspNetCore.Mvc;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Facilities;

namespace SelfStorageSystem.Controllers;

[ApiController]
[Route("api/customer")]
public sealed class CustomerCatalogController(ICustomerCatalogService catalog) : ControllerBase
{
    [HttpGet("facilities")]
    public async Task<IActionResult> GetFacilities(CancellationToken ct)
    {
        var result = await catalog.GetFacilitiesAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<FacilitySummaryDto>>.Ok(result));
    }

    [HttpGet("facilities/{id:long}")]
    public async Task<IActionResult> GetFacility(long id, CancellationToken ct)
    {
        var result = await catalog.GetFacilityAsync(id, ct);
        if (result is null)
        {
            return NotFound(ApiResponse.Fail("Facility not found."));
        }

        return Ok(ApiResponse<FacilityDetailDto>.Ok(result));
    }

    [HttpGet("unit-types")]
    public async Task<IActionResult> GetUnitTypes(CancellationToken ct)
    {
        var result = await catalog.GetUnitTypesAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<UnitTypeDto>>.Ok(result));
    }

    [HttpGet("storage-units/available")]
    public async Task<IActionResult> GetAvailableUnits(
        [FromQuery] long? facilityId,
        [FromQuery] long? unitTypeId,
        [FromQuery] long? facilityAreaId,
        CancellationToken ct)
    {
        var result = await catalog.GetAvailableUnitsAsync(facilityId, unitTypeId, facilityAreaId, ct);
        return Ok(ApiResponse<IReadOnlyList<AvailableUnitDto>>.Ok(result));
    }


    [HttpPost("pricing/calculate")]
    public async Task<IActionResult> CalculatePricing(
        [FromBody] CalculatePricingRequest request,
        CancellationToken ct)
    {
        var result = await catalog.CalculatePricingAsync(request, ct);
        return Ok(ApiResponse<PricingCalculationDto>.Ok(result));
    }
}
