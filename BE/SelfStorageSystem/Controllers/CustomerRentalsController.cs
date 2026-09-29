using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Rentals;
using SelfStorageSystem.Domain.Constants;

namespace SelfStorageSystem.Controllers;

[ApiController]
[Route("api/customer/rentals")]
[Authorize(Roles = RoleConstants.Customer)]
public class CustomerRentalsController : ControllerBase
{
    private readonly ICustomerRentalService _rentalService;
    private readonly ILogger<CustomerRentalsController> _logger;

    public CustomerRentalsController(
        ICustomerRentalService rentalService,
        ILogger<CustomerRentalsController> logger)
    {
        _rentalService = rentalService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves active and expiring rental agreements belonging to the authenticated customer (My Rentals).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RentalSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyRentals(CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        var rentals = await _rentalService.GetMyRentalsAsync(customerId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RentalSummaryDto>>.Ok(rentals, "Retrieved rental agreements successfully."));
    }

    /// <summary>
    /// Retrieves unit keypad PIN and short-lived time-based Gate QR Token (BR-RSV-04, BR-REN-03).
    /// </summary>
    [HttpGet("{agreementId:long}/access-credentials")]
    [ProducesResponseType(typeof(ApiResponse<AccessCredentialDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccessCredentials(
        [FromRoute] long agreementId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        var credentials = await _rentalService.GetAccessCredentialsAsync(customerId, agreementId, cancellationToken);
        return Ok(ApiResponse<AccessCredentialDto>.Ok(credentials, "Retrieved access credentials successfully."));
    }

    /// <summary>
    /// Allows customer to change their storage unit keypad PIN (BR-REN-03).
    /// </summary>
    [HttpPut("{agreementId:long}/change-pin")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangePin(
        [FromRoute] long agreementId,
        [FromBody] ChangePinRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        await _rentalService.ChangePinAsync(customerId, agreementId, request, cancellationToken);
        return Ok(ApiResponse.Ok("Keypad PIN changed successfully."));
    }

    /// <summary>
    /// Retrieves check-in handover record, inspection condition, and photos (BR-FIN-02, BR-OPS-02).
    /// </summary>
    [HttpGet("{agreementId:long}/handover")]
    [ProducesResponseType(typeof(ApiResponse<HandoverRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHandoverRecord(
        [FromRoute] long agreementId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        var handover = await _rentalService.GetHandoverRecordAsync(customerId, agreementId, cancellationToken);
        return Ok(ApiResponse<HandoverRecordDto>.Ok(handover, "Retrieved handover record successfully."));
    }

    private bool TryGetCustomerId(out long customerId)
    {
        customerId = 0;
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(userIdClaim) && long.TryParse(userIdClaim, out customerId);
    }
}
