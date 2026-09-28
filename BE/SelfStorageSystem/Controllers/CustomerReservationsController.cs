using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Reservations;

namespace SelfStorageSystem.Controllers;

[ApiController]
[Route("api/customer/reservations")]
[Authorize(Roles = "storage_customer")]
public class CustomerReservationsController : ControllerBase
{
    private readonly ICustomerReservationService _reservationService;
    private readonly ILogger<CustomerReservationsController> _logger;

    public CustomerReservationsController(
        ICustomerReservationService reservationService,
        ILogger<CustomerReservationsController> logger)
    {
        _reservationService = reservationService;
        _logger = logger;
    }

    /// <summary>
    /// Creates an online storage unit reservation with a 15-minute temporary hold and initial invoice (BR-RSV-01, BR-RSV-02, BR-FIN-01).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreateReservationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateReservation(
        [FromBody] CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        try
        {
            var response = await _reservationService.CreateReservationAsync(customerId, request, cancellationToken);
            return Ok(ApiResponse<CreateReservationResponse>.Ok(response, "Unit reserved successfully. Please complete your payment within 15 minutes."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating reservation: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("A system error occurred while creating reservation. Please try again."));
        }
    }

    /// <summary>
    /// Retrieves all reservations belonging to the current authenticated customer.
    /// </summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(ApiResponse<List<ReservationSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyReservations(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        try
        {
            var list = await _reservationService.GetMyReservationsAsync(customerId, status, cancellationToken);
            return Ok(ApiResponse<List<ReservationSummaryDto>>.Ok(list, "Retrieved reservations list successfully."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reservations for customer {CustomerId}: {Message}", customerId, ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("A system error occurred while retrieving reservations list."));
        }
    }

    /// <summary>
    /// Retrieves detailed reservation information, remaining hold time, and check-in QR token if paid.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<ReservationDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReservationDetail(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        try
        {
            var detail = await _reservationService.GetReservationDetailAsync(customerId, id, cancellationToken);
            return Ok(ApiResponse<ReservationDetailDto>.Ok(detail, "Retrieved reservation details successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reservation details for ID {ReservationId}: {Message}", id, ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("A system error occurred while retrieving reservation details."));
        }
    }

    /// <summary>
    /// Cancels a pending unpaid reservation and releases the storage unit back to available status (BR-RSV-01).
    /// </summary>
    [HttpPost("{id:long}/cancel")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelReservation(
        [FromRoute] long id,
        [FromBody] CancelReservationRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        try
        {
            await _reservationService.CancelReservationAsync(customerId, id, request?.Reason, cancellationToken);
            return Ok(ApiResponse.Ok("Reservation cancelled successfully. Storage unit has been released."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling reservation {ReservationId}: {Message}", id, ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("A system error occurred while cancelling reservation."));
        }
    }

    private bool TryGetCustomerId(out long customerId)
    {
        customerId = 0;
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(userIdClaim) && long.TryParse(userIdClaim, out customerId);
    }
}
