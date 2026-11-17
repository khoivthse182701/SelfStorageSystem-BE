using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Reservations;

namespace SelfStorageSystem.Controllers;

[ApiController]
[Route("api/v1/reservations")]
[Authorize(Roles = "Customer")]
public sealed class ReservationsV1Controller(ICustomerReservationService reservationService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreateReservationResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateReservation(
        [FromBody] CreateReservationRequest request,
        CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var response = await reservationService.CreateReservationAsync(customerId, request, ct);
        var wrapped = ApiResponse<CreateReservationResponse>.Ok(response, "Unit reserved successfully. Please complete payment within 15 minutes.");
        return CreatedAtAction(nameof(GetReservationDetail), new { reservationId = response.ReservationId }, wrapped);
    }

    [HttpGet("my")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ReservationSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyReservations(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await reservationService.GetMyReservationsPagedAsync(customerId, status, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<ReservationSummaryDto>>.Ok(result, "Retrieved reservations list successfully."));
    }

    [HttpGet("{reservationId:long}")]
    [ProducesResponseType(typeof(ApiResponse<ReservationDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReservationDetail(long reservationId, CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var detail = await reservationService.GetReservationDetailAsync(customerId, reservationId, ct);
        return Ok(ApiResponse<ReservationDetailDto>.Ok(detail, "Retrieved reservation details successfully."));
    }

    [HttpPost("{reservationId:long}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<CancelReservationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelReservation(
        long reservationId,
        [FromBody] CancelReservationRequest? request,
        CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await reservationService.CancelReservationWithResultAsync(customerId, reservationId, request?.Reason, ct);
        return Ok(ApiResponse<CancelReservationResponse>.Ok(result, "Reservation cancelled successfully. Storage unit has been released."));
    }

    private bool TryGetCustomerId(out long customerId)
    {
        customerId = 0;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(claim) && long.TryParse(claim, out customerId);
    }
}
