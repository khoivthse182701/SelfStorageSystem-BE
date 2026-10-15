using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Operations;
using SelfStorageSystem.Domain.Constants;

namespace SelfStorageSystem.Controllers;

[ApiController]
[Route("api/operations")]
[Authorize(Roles = RoleConstants.BusinessOperationsManager + "," + RoleConstants.SystemAdministrator)]
public class OperationsController : ControllerBase
{
    private readonly IBusinessOperationsService _operationsService;

    public OperationsController(IBusinessOperationsService operationsService)
    {
        _operationsService = operationsService;
    }

    [HttpGet("pricing/rates")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<OperationsRateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRates([FromQuery] OperationsRateQueryParameters query, CancellationToken cancellationToken)
    {
        var result = await _operationsService.GetRatesAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<OperationsRateDto>>.Ok(result, "Retrieved facility rates successfully."));
    }

    /// <summary>Upserts a rate by facility, unit type and ValidFrom when Id is omitted; otherwise updates the specified rate.</summary>
    [HttpPut("pricing/rates")]
    [ProducesResponseType(typeof(ApiResponse<OperationsRateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpsertRate([FromBody] OperationsUpsertRateRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var actorUserId)) return Unauthorized(ApiResponse.Fail("Cannot identify operations user identity from token."));
        var result = await _operationsService.UpsertRateAsync(request, actorUserId, cancellationToken);
        return Ok(ApiResponse<OperationsRateDto>.Ok(result, "Facility rate saved successfully."));
    }

    [HttpGet("pricing/fees")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<OperationsFeeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFees([FromQuery] OperationsFeeQueryParameters query, CancellationToken cancellationToken)
    {
        var result = await _operationsService.GetFeesAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<OperationsFeeDto>>.Ok(result, "Retrieved fee rules successfully."));
    }

    /// <summary>Upserts a fee rule by facility scope, code and ValidFrom when Id is omitted; otherwise updates the specified rule.</summary>
    [HttpPut("pricing/fees")]
    [ProducesResponseType(typeof(ApiResponse<OperationsFeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpsertFee([FromBody] OperationsUpsertFeeRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var actorUserId)) return Unauthorized(ApiResponse.Fail("Cannot identify operations user identity from token."));
        var result = await _operationsService.UpsertFeeAsync(request, actorUserId, cancellationToken);
        return Ok(ApiResponse<OperationsFeeDto>.Ok(result, "Fee rule saved successfully."));
    }

    [HttpGet("promotions")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<OperationsPromotionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPromotions([FromQuery] OperationsPromotionQueryParameters query, CancellationToken cancellationToken)
    {
        var result = await _operationsService.GetPromotionsAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<OperationsPromotionDto>>.Ok(result, "Retrieved promotions successfully."));
    }

    [HttpPost("promotions")]
    [ProducesResponseType(typeof(ApiResponse<OperationsPromotionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePromotion([FromBody] OperationsCreatePromotionRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var actorUserId)) return Unauthorized(ApiResponse.Fail("Cannot identify operations user identity from token."));
        var result = await _operationsService.CreatePromotionAsync(request, actorUserId, cancellationToken);
        return Ok(ApiResponse<OperationsPromotionDto>.Ok(result, "Promotion created successfully."));
    }

    [HttpPut("promotions/{id:long}/status")]
    [ProducesResponseType(typeof(ApiResponse<OperationsPromotionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePromotionStatus([FromRoute] long id, [FromBody] OperationsUpdatePromotionStatusRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var actorUserId)) return Unauthorized(ApiResponse.Fail("Cannot identify operations user identity from token."));
        var result = await _operationsService.UpdatePromotionStatusAsync(id, request, actorUserId, cancellationToken);
        return Ok(ApiResponse<OperationsPromotionDto>.Ok(result, "Promotion status updated successfully."));
    }

    [HttpPost("facilities")]
    [ProducesResponseType(typeof(ApiResponse<OperationsFacilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateFacility([FromBody] OperationsCreateFacilityRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var actorUserId)) return Unauthorized(ApiResponse.Fail("Cannot identify operations user identity from token."));
        var result = await _operationsService.CreateFacilityAsync(request, actorUserId, cancellationToken);
        return Ok(ApiResponse<OperationsFacilityDto>.Ok(result, "Facility created successfully."));
    }

    /// <summary>Updates the facility identified by Id in the route or request body.</summary>
    [HttpPut("facilities")]
    [HttpPut("facilities/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<OperationsFacilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateFacility(
        [FromBody] OperationsUpdateFacilityRequest request,
        [FromRoute] long? id,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var actorUserId)) return Unauthorized(ApiResponse.Fail("Cannot identify operations user identity from token."));
        if (id.HasValue && id.Value > 0)
        {
            if (request.Id > 0 && request.Id != id.Value)
            {
                return BadRequest(ApiResponse.Fail("Route facility ID does not match the request payload ID."));
            }
            request.Id = id.Value;
        }
        var result = await _operationsService.UpdateFacilityAsync(request, actorUserId, cancellationToken);
        return Ok(ApiResponse<OperationsFacilityDto>.Ok(result, "Facility updated successfully."));
    }

    private bool TryGetUserId(out long userId)
    {
        return long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId) && userId > 0;
    }
}
