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
    private readonly ICustomerStoredItemService _itemService;
    private readonly ILogger<CustomerRentalsController> _logger;

    public CustomerRentalsController(
        ICustomerRentalService rentalService,
        ICustomerStoredItemService itemService,
        ILogger<CustomerRentalsController> logger)
    {
        _rentalService = rentalService;
        _itemService = itemService;
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
    /// Returns 202 Accepted with sync status while IoT lock synchronizes asynchronously.
    /// </summary>
    [HttpPut("{agreementId:long}/change-pin")]
    [ProducesResponseType(typeof(ApiResponse<ChangePinResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ChangePinResponseDto>), StatusCodes.Status202Accepted)]
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

        var result = await _rentalService.ChangePinAsync(customerId, agreementId, request, cancellationToken);
        if (result.SyncStatus == PinSyncStatusConstants.Pending)
        {
            return Accepted(ApiResponse<ChangePinResponseDto>.Ok(result, result.Message));
        }

        return Ok(ApiResponse<ChangePinResponseDto>.Ok(result, result.Message));
    }

    /// <summary>
    /// Verifies customer PIN and triggers remote unlock for the assigned storage unit door.
    /// </summary>
    [HttpPost("{agreementId:long}/unlock")]
    [ProducesResponseType(typeof(ApiResponse<UnlockStorageUnitResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UnlockUnit(
        [FromRoute] long agreementId,
        [FromBody] UnlockStorageUnitRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        var result = await _rentalService.UnlockUnitAsync(customerId, agreementId, request, cancellationToken);
        return Ok(ApiResponse<UnlockStorageUnitResponseDto>.Ok(result, result.Message));
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

    /// <summary>
    /// Declares stored items and item risk classifications in a rental storage unit.
    /// </summary>
    [HttpPost("{agreementId:long}/items")]
    [ProducesResponseType(typeof(ApiResponse<RentalStoredItemsResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeclareStoredItems(
        [FromRoute] long agreementId,
        [FromBody] DeclareStoredItemsRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        var result = await _itemService.DeclareStoredItemsAsync(customerId, agreementId, request, cancellationToken);
        return CreatedAtAction(
            nameof(GetDeclaredStoredItems), 
            new { agreementId = result.AgreementId }, 
            ApiResponse<RentalStoredItemsResponseDto>.Ok(result, "Declared stored items successfully."));
    }

    /// <summary>
    /// Retrieves the list of declared stored items for a specific rental agreement.
    /// </summary>
    [HttpGet("{agreementId:long}/items")]
    [ProducesResponseType(typeof(ApiResponse<RentalStoredItemsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeclaredStoredItems(
        [FromRoute] long agreementId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        var result = await _itemService.GetDeclaredStoredItemsAsync(customerId, agreementId, cancellationToken);
        return Ok(ApiResponse<RentalStoredItemsResponseDto>.Ok(result, "Retrieved declared stored items successfully."));
    }

    /// <summary>
    /// Updates an existing stored item in a rental storage unit.
    /// </summary>
    [HttpPut("{agreementId:long}/items/{itemId:long}")]
    [ProducesResponseType(typeof(ApiResponse<StoredItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStoredItem(
        [FromRoute] long agreementId,
        [FromRoute] long itemId,
        [FromBody] UpdateStoredItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        var result = await _itemService.UpdateStoredItemAsync(customerId, agreementId, itemId, request, cancellationToken);
        return Ok(ApiResponse<StoredItemDto>.Ok(result, "Stored item updated successfully."));
    }

    /// <summary>
    /// Deletes a stored item from a rental storage unit.
    /// </summary>
    [HttpDelete("{agreementId:long}/items/{itemId:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteStoredItem(
        [FromRoute] long agreementId,
        [FromRoute] long itemId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        await _itemService.DeleteStoredItemAsync(customerId, agreementId, itemId, cancellationToken);
        return Ok(ApiResponse.Ok("Stored item deleted successfully."));
    }

    /// <summary>
    /// Requests move-out and return inspection for a rental agreement (Flow 6 & BR-FIN-02).
    /// </summary>
    [HttpPost("{agreementId:long}/move-out")]
    [ProducesResponseType(typeof(ApiResponse<MoveOutResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestMoveOut(
        [FromRoute] long agreementId,
        [FromBody] RequestMoveOutRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        var result = await _rentalService.RequestMoveOutAsync(customerId, agreementId, request, cancellationToken);
        return Ok(ApiResponse<MoveOutResponseDto>.Ok(result, "Move-out request submitted successfully."));
    }

    /// <summary>
    /// Previews estimated deposit refund before move-out (BR-FIN-02).
    /// </summary>
    [HttpGet("{agreementId:long}/refund-preview")]
    [ProducesResponseType(typeof(ApiResponse<RefundPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRefundPreview(
        [FromRoute] long agreementId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        var result = await _rentalService.GetRefundPreviewAsync(customerId, agreementId, cancellationToken);
        return Ok(ApiResponse<RefundPreviewDto>.Ok(result, "Estimated refund preview retrieved successfully."));
    }

    /// <summary>
    /// Requests manual agreement renewal (Flow 6).
    /// </summary>
    [HttpPost("{agreementId:long}/renew")]
    [ProducesResponseType(typeof(ApiResponse<RenewAgreementResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RenewAgreement(
        [FromRoute] long agreementId,
        [FromBody] RenewAgreementRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        var result = await _rentalService.RenewAgreementAsync(customerId, agreementId, request, cancellationToken);
        return Ok(ApiResponse<RenewAgreementResponseDto>.Ok(result, "Agreement renewal requested successfully."));
    }

    /// <summary>
    /// Gets authorized access members for a rental agreement (Flow 3).
    /// </summary>
    [HttpGet("{agreementId:long}/authorized-members")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AuthorizedMemberDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuthorizedMembers(
        [FromRoute] long agreementId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        var result = await _rentalService.GetAuthorizedMembersAsync(customerId, agreementId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AuthorizedMemberDto>>.Ok(result, "Retrieved authorized access members successfully."));
    }

    /// <summary>
    /// Adds a new authorized access member to a rental agreement (Flow 3).
    /// </summary>
    [HttpPost("{agreementId:long}/authorized-members")]
    [ProducesResponseType(typeof(ApiResponse<AuthorizedMemberDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddAuthorizedMember(
        [FromRoute] long agreementId,
        [FromBody] CreateAuthorizedMemberRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        var result = await _rentalService.AddAuthorizedMemberAsync(customerId, agreementId, request, cancellationToken);
        return CreatedAtAction(
            nameof(GetAuthorizedMembers),
            new { agreementId },
            ApiResponse<AuthorizedMemberDto>.Ok(result, "Authorized member added successfully."));
    }

    /// <summary>
    /// Revokes an authorized access member (Flow 3).
    /// </summary>
    [HttpDelete("{agreementId:long}/authorized-members/{memberId:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeAuthorizedMember(
        [FromRoute] long agreementId,
        [FromRoute] long memberId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        await _rentalService.RevokeAuthorizedMemberAsync(customerId, agreementId, memberId, cancellationToken);
        return Ok(ApiResponse.Ok("Authorized member revoked successfully."));
    }

    /// <summary>
    /// Requests unit transfer or size upgrade (Flow 3 & BR-RSV-04).
    /// </summary>
    [HttpPost("{agreementId:long}/transfer-request")]
    [ProducesResponseType(typeof(ApiResponse<UnitTransferRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestUnitTransfer(
        [FromRoute] long agreementId,
        [FromBody] CreateTransferRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        var result = await _rentalService.RequestUnitTransferAsync(customerId, agreementId, request, cancellationToken);
        return Ok(ApiResponse<UnitTransferRequestDto>.Ok(result, "Unit transfer requested successfully."));
    }

    private bool TryGetCustomerId(out long customerId)
    {
        customerId = 0;
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(userIdClaim) && long.TryParse(userIdClaim, out customerId);
    }
}
