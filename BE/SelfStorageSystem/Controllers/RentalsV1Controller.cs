using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Auth;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer;
using SelfStorageSystem.Contracts.Customer.Rentals;

namespace SelfStorageSystem.Controllers;

[ApiController]
[Authorize(Roles = "Customer")]
public sealed class RentalsV1Controller(
    ICustomerRentalService rentalService,
    ICustomerStoredItemService storedItemService,
    IAuthService authService) : ControllerBase
{
    [HttpGet("api/v1/rentals/my-debt-alerts")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyDebtAlerts(CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var profile = await authService.GetProfileAsync(customerId, ct);
        return Ok(ApiResponse<object>.Ok(new
        {
            hasNegativeDepositDebt = profile.HasNegativeDepositDebt,
            totalNegativeDebtAmount = profile.TotalNegativeDebtAmount,
            items = profile.NegativeDebtInvoices
        }, "Retrieved negative deposit debt alerts successfully."));
    }

    [HttpGet("api/v1/rentals/my")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RentalSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyRentals([FromQuery] string? status, CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await rentalService.GetMyRentalsV1Async(customerId, status, ct);
        return Ok(ApiResponse<IReadOnlyList<RentalSummaryDto>>.Ok(result, "Retrieved rentals successfully."));
    }

    [HttpGet("api/v1/rentals/{agreementId:long}")]
    [ProducesResponseType(typeof(ApiResponse<RentalDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRentalDetail(long agreementId, CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await rentalService.GetRentalDetailAsync(customerId, agreementId, ct);
        return Ok(ApiResponse<RentalDetailDto>.Ok(result, "Retrieved rental detail successfully."));
    }

    [HttpGet("api/v1/rentals/{agreementId:long}/access-credentials")]
    [ProducesResponseType(typeof(ApiResponse<AccessCredentialDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetAccessCredentials(long agreementId, CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await rentalService.GetAccessCredentialsAsync(customerId, agreementId, ct);
        return Ok(ApiResponse<AccessCredentialDto>.Ok(result, "Access credentials retrieved successfully."));
    }

    [HttpPut("api/v1/rentals/{agreementId:long}/pin")]
    [ProducesResponseType(typeof(ApiResponse<ChangePinResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangePin(long agreementId, [FromBody] ChangePinRequest request, CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await rentalService.ChangePinAsync(customerId, agreementId, request, ct);
        return Ok(ApiResponse<ChangePinResponseDto>.Ok(result, result.Message));
    }

    [HttpPost("api/v1/rentals/{agreementId:long}/unlock")]
    [ProducesResponseType(typeof(ApiResponse<UnlockStorageUnitResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UnlockUnit(long agreementId, [FromBody] UnlockStorageUnitRequest request, CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await rentalService.UnlockUnitAsync(customerId, agreementId, request, ct);
        return Ok(ApiResponse<UnlockStorageUnitResponseDto>.Ok(result, result.Message));
    }

    [HttpGet("api/v1/rentals/{agreementId:long}/access-logs")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AccessEventDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccessLogs(
        long agreementId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await rentalService.GetAccessLogsAsync(customerId, agreementId, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<AccessEventDto>>.Ok(result, "Retrieved access logs successfully."));
    }

    [HttpGet("api/v1/rentals/{agreementId:long}/handover-record")]
    [ProducesResponseType(typeof(ApiResponse<HandoverRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHandoverRecord(long agreementId, CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await rentalService.GetHandoverRecordAsync(customerId, agreementId, ct);
        return Ok(ApiResponse<HandoverRecordDto>.Ok(result, "Retrieved handover record successfully."));
    }

    [HttpGet("api/v1/rentals/{agreementId:long}/authorized-members")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AuthorizedMemberDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuthorizedMembers(long agreementId, CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await rentalService.GetAuthorizedMembersAsync(customerId, agreementId, ct);
        return Ok(ApiResponse<IReadOnlyList<AuthorizedMemberDto>>.Ok(result, "Retrieved authorized members successfully."));
    }

    [HttpPost("api/v1/rentals/{agreementId:long}/authorized-members")]
    [ProducesResponseType(typeof(ApiResponse<AuthorizedMemberDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddAuthorizedMember(
        long agreementId,
        [FromBody] CreateAuthorizedMemberRequest request,
        CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await rentalService.AddAuthorizedMemberAsync(customerId, agreementId, request, ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<AuthorizedMemberDto>.Ok(result, "Authorized member added successfully."));
    }

    [HttpDelete("api/v1/rentals/{agreementId:long}/authorized-members/{memberId:long}")]
    [ProducesResponseType(typeof(ApiResponse<RevokeAuthorizedMemberResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RevokeAuthorizedMember(long agreementId, long memberId, CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await rentalService.RevokeAuthorizedMemberV1Async(customerId, agreementId, memberId, ct);
        return Ok(ApiResponse<RevokeAuthorizedMemberResponseDto>.Ok(result, "Authorized member revoked successfully."));
    }

    [HttpGet("api/v1/rentals/{agreementId:long}/items")]
    [ProducesResponseType(typeof(ApiResponse<RentalStoredItemsResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStoredItems(
        long agreementId,
        [FromQuery] string? search,
        [FromQuery] string? category,
        CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await storedItemService.GetDeclaredStoredItemsAsync(customerId, agreementId, search, category, ct);
        return Ok(ApiResponse<RentalStoredItemsResponseDto>.Ok(result, "Retrieved stored items successfully."));
    }

    [HttpPost("api/v1/rentals/{agreementId:long}/items")]
    [ProducesResponseType(typeof(ApiResponse<RentalStoredItemsResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeclareStoredItems(
        long agreementId,
        [FromBody] DeclareStoredItemsRequest request,
        CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await storedItemService.DeclareStoredItemsAsync(customerId, agreementId, request, ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<RentalStoredItemsResponseDto>.Ok(result, "Stored items declared successfully."));
    }

    [HttpPut("api/v1/rentals/{agreementId:long}/items/{itemId:long}")]
    [ProducesResponseType(typeof(ApiResponse<StoredItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStoredItem(
        long agreementId,
        long itemId,
        [FromBody] UpdateStoredItemRequest request,
        CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await storedItemService.UpdateStoredItemAsync(customerId, agreementId, itemId, request, ct);
        return Ok(ApiResponse<StoredItemDto>.Ok(result, "Stored item updated successfully."));
    }

    [HttpDelete("api/v1/rentals/{agreementId:long}/items/{itemId:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteStoredItem(long agreementId, long itemId, CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        await storedItemService.DeleteStoredItemAsync(customerId, agreementId, itemId, ct);
        return Ok(ApiResponse<object>.Ok(new { deleted = true }, "Stored item deleted successfully."));
    }

    [HttpGet("api/v1/rentals/{agreementId:long}/move-out/refund-preview")]
    [ProducesResponseType(typeof(ApiResponse<RefundPreviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRefundPreview(
        long agreementId,
        [FromQuery] DateOnly? requestedMoveOutDate,
        CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await rentalService.GetRefundPreviewAsync(customerId, agreementId, requestedMoveOutDate, ct);
        return Ok(ApiResponse<RefundPreviewDto>.Ok(result, "Refund preview calculated successfully."));
    }

    [HttpPost("api/v1/rentals/{agreementId:long}/move-out")]
    [ProducesResponseType(typeof(ApiResponse<MoveOutResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RequestMoveOut(
        long agreementId,
        [FromBody] RequestMoveOutRequest request,
        CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await rentalService.RequestMoveOutAsync(customerId, agreementId, request, ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<MoveOutResponseDto>.Ok(result, "Move-out request submitted successfully."));
    }

    [HttpGet("api/v1/rentals/refunds/pending-bank-info")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CustomerPendingRefundDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingBankInfoRefunds(CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await rentalService.GetPendingBankInfoRefundsAsync(customerId, ct);
        return Ok(ApiResponse<IReadOnlyList<CustomerPendingRefundDto>>.Ok(result, "Retrieved pending refunds successfully."));
    }

    [HttpPost("api/v1/rentals/refunds/{refundId:long}/bank-info")]
    [ProducesResponseType(typeof(ApiResponse<CustomerRefundBankInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitRefundBankInfo(
        long refundId,
        [FromBody] CustomerSubmitRefundBankInfoRequest request,
        CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await rentalService.SubmitRefundBankInfoAsync(customerId, refundId, request, ct);
        return Ok(ApiResponse<CustomerRefundBankInfoDto>.Ok(result, result.Message));
    }

    [HttpGet("api/v1/rentals/{agreementId:long}/transfer/preview")]
    [ProducesResponseType(typeof(ApiResponse<TransferPreviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransferPreview(
        long agreementId,
        [FromQuery] long? requestedFacilityId,
        [FromQuery] long? requestedUnitTypeId,
        [FromQuery] DateOnly? moveOutDate,
        CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await rentalService.GetTransferPreviewAsync(customerId, agreementId, requestedFacilityId, requestedUnitTypeId, moveOutDate, ct);
        return Ok(ApiResponse<TransferPreviewDto>.Ok(result, "Transfer preview calculated successfully."));
    }

    [HttpPost("api/v1/rentals/{agreementId:long}/transfer")]
    [ProducesResponseType(typeof(ApiResponse<UnitTransferRequestDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RequestUnitTransfer(
        long agreementId,
        [FromBody] CreateTransferRequest request,
        CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await rentalService.RequestUnitTransferAsync(customerId, agreementId, request, ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<UnitTransferRequestDto>.Ok(result, "Unit transfer request submitted successfully."));
    }

    [HttpGet("api/v1/rentals/{agreementId:long}/transfer")]
    [ProducesResponseType(typeof(ApiResponse<UnitTransferRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestTransferRequest(long agreementId, CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await rentalService.GetLatestTransferRequestAsync(customerId, agreementId, ct);
        if (result == null)
            return NotFound(ApiResponse.Fail("No active or recent transfer request found for this agreement.", "Rental.TransferRequestNotFound"));

        return Ok(ApiResponse<UnitTransferRequestDto>.Ok(result, "Retrieved transfer request successfully."));
    }

    [HttpPut("api/v1/rentals/transfers/{transferId:long}/reschedule")]
    [ProducesResponseType(typeof(ApiResponse<UnitTransferRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RescheduleTransfer(
        long transferId,
        [FromBody] RescheduleTransferRequest request,
        CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await rentalService.RescheduleTransferAsync(customerId, transferId, request, ct);
        return Ok(ApiResponse<UnitTransferRequestDto>.Ok(result, "Transfer dates rescheduled successfully."));
    }

    [HttpPost("api/v1/rentals/transfers/{transferId:long}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelTransferRequest(long transferId, CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        await rentalService.CancelTransferRequestAsync(customerId, transferId, ct);
        return Ok(ApiResponse<object>.Ok(new { cancelled = true }, "Transfer request cancelled successfully."));
    }

    // Flow 6: Renewal & Settlement
    [HttpPost("api/v1/rentals/{agreementId:long}/renew")]
    [ProducesResponseType(typeof(ApiResponse<RenewAgreementResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RenewAgreement(
        long agreementId,
        [FromBody] RenewAgreementRequest request,
        CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await rentalService.RenewAgreementAsync(customerId, agreementId, request, ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<RenewAgreementResponseDto>.Ok(result, "Rental renewal initialized successfully."));
    }

    [HttpPost("api/v1/renewals/{renewalId:long}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelRenewal(long renewalId, CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        await rentalService.CancelRenewalAsync(customerId, renewalId, ct);
        return Ok(ApiResponse<object>.Ok(new { renewalId, status = "cancelled" }, "Renewal request cancelled successfully."));
    }

    private bool TryGetCustomerId(out long customerId)
    {
        customerId = 0;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(claim) && long.TryParse(claim, out customerId);
    }
}
