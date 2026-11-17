using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Facilities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;

namespace SelfStorageSystem.Controllers;

[ApiController]
public sealed class FacilitiesV1Controller(
    ICustomerCatalogService catalog,
    IPricingEngine pricingEngine) : ControllerBase
{
    [HttpGet("api/v1/facilities")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<FacilitySummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFacilities(
        [FromQuery] string? city,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await catalog.GetFacilitiesPagedAsync(city, search, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<FacilitySummaryDto>>.Ok(result));
    }

    [HttpGet("api/v1/facilities/{facilityId:long}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<FacilityDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFacility(long facilityId, CancellationToken ct)
    {
        var result = await catalog.GetFacilityAsync(facilityId, ct)
            ?? throw AppException.FromError(CatalogErrors.FacilityNotFound);
        return Ok(ApiResponse<FacilityDetailDto>.Ok(result));
    }

    [HttpGet("api/v1/unit-types")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UnitTypeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnitTypes(CancellationToken ct)
    {
        var result = await catalog.GetUnitTypesAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<UnitTypeDto>>.Ok(result));
    }

    [HttpGet("api/v1/facilities/{facilityId:long}/units/available")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AvailableUnitDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetAvailableUnits(
        long facilityId,
        [FromQuery] long? unitTypeId,
        [FromQuery] long? areaId,
        [FromQuery] DateOnly? startDate,
        [FromQuery] int durationMonths = 1,
        CancellationToken ct = default)
    {
        var result = await catalog.GetAvailableUnitsAsync(facilityId, unitTypeId, areaId, startDate, durationMonths, ct);
        return Ok(ApiResponse<IReadOnlyList<AvailableUnitDto>>.Ok(result));
    }

    [HttpPost("api/v1/pricing/quote")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PricingQuoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Quote(
        [FromBody] PricingQuoteRequest request,
        CancellationToken ct)
    {
        long? customerId = long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId)
            ? parsedId
            : null;
        var result = await pricingEngine.CalculateQuoteAsync(request, customerId, lockPromotionRow: false, throwConflictOnPromotionLimit: false, ct);
        return Ok(ApiResponse<PricingQuoteDto>.Ok(result));
    }
}
