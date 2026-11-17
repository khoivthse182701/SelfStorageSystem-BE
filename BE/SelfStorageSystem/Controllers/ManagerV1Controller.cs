using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Manager;

namespace SelfStorageSystem.Controllers;

[ApiController]
[Route("api/v1/manager")]
[Authorize(Roles = "Manager")]
public sealed class ManagerV1Controller(IFacilityManagerService managerService) : ControllerBase
{
    [HttpPost("agreements/{agreementId:long}/reassign-unit")]
    [ProducesResponseType(typeof(ApiResponse<ManagerReassignUnitResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReassignAgreementUnit(
        long agreementId,
        [FromBody] ManagerReassignUnitRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var managerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify manager identity from token.", "Auth.UserTokenInvalid"));

        var result = await managerService.ReassignAgreementUnitAsync(agreementId, request, managerId, isSystemAdmin: false, ct);
        return Ok(ApiResponse<ManagerReassignUnitResponseDto>.Ok(result, result.Message));
    }

    [HttpPost("agreements/{agreementId:long}/emergency-cancel-refund")]
    [ProducesResponseType(typeof(ApiResponse<ManagerEmergencyCancelRefundResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EmergencyCancelAndRefund(
        long agreementId,
        [FromBody] ManagerEmergencyCancelRefundRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var managerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify manager identity from token.", "Auth.UserTokenInvalid"));

        var result = await managerService.EmergencyCancelAndRefundAgreementAsync(agreementId, request, managerId, isSystemAdmin: false, ct);
        result.RefundStatus ??= result.RefundId.HasValue ? "pending" : "none";
        return Ok(ApiResponse<ManagerEmergencyCancelRefundResponseDto>.Ok(result, result.Message));
    }

    // Flow 6: Delinquency Cases, Liquidation & Expiring Cases Inspection Assignment
    [HttpGet("facilities/{facilityId:long}/delinquency-cases")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ManagerDelinquencyCaseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDelinquencyCases(
        long facilityId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var managerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify manager identity from token.", "Auth.UserTokenInvalid"));

        var cases = await managerService.GetDelinquentCasesAsync(facilityId, managerId, isSystemAdmin: false, ct);
        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim();
            cases = cases.Where(c => string.Equals(c.Status, s, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        return Ok(ApiResponse<IReadOnlyList<ManagerDelinquencyCaseDto>>.Ok(cases, "Retrieved delinquency cases successfully."));
    }

    [HttpPost("delinquency-cases/{caseId:long}/actions")]
    [ProducesResponseType(typeof(ApiResponse<ManagerDelinquencyActionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddDelinquencyAction(
        long caseId,
        [FromBody] ManagerAddDelinquencyActionRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var managerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify manager identity from token.", "Auth.UserTokenInvalid"));

        var action = await managerService.AddDelinquencyActionAsync(caseId, request, managerId, isSystemAdmin: false, ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<ManagerDelinquencyActionDto>.Ok(action, "Delinquency action recorded successfully."));
    }

    [HttpPost("agreements/{agreementId:long}/liquidate")]
    [ProducesResponseType(typeof(ApiResponse<ManagerLiquidateAgreementResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> LiquidateAgreement(
        long agreementId,
        [FromBody] ManagerLiquidateAgreementRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var managerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify manager identity from token.", "Auth.UserTokenInvalid"));

        var result = await managerService.LiquidateAgreementAsync(agreementId, request, managerId, isSystemAdmin: false, ct);
        return Ok(ApiResponse<ManagerLiquidateAgreementResultDto>.Ok(result, "Agreement liquidated successfully."));
    }

    [HttpGet("facilities/{facilityId:long}/expiring-cases")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ManagerAgreementDetailDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExpiringCases(long facilityId, CancellationToken ct)
    {
        if (!TryGetUserId(out var managerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify manager identity from token.", "Auth.UserTokenInvalid"));

        var result = await managerService.GetExpiringOrMoveOutAgreementsAsync(facilityId, managerId, isSystemAdmin: false, ct);
        return Ok(ApiResponse<IReadOnlyList<ManagerAgreementDetailDto>>.Ok(result, "Retrieved expiring and move-out cases successfully."));
    }

    [HttpPost("agreements/{agreementId:long}/assign-inspection")]
    [ProducesResponseType(typeof(ApiResponse<ManagerInspectionDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AssignInspection(
        long agreementId,
        [FromBody] ManagerAssignInspectionRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var managerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify manager identity from token.", "Auth.UserTokenInvalid"));

        request.AgreementId = agreementId;
        var result = await managerService.AssignMoveOutInspectionAsync(request, managerId, isSystemAdmin: false, ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<ManagerInspectionDetailDto>.Ok(result, "Move-out inspection assigned successfully."));
    }

    private bool TryGetUserId(out long userId)
    {
        userId = 0;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(claim) && long.TryParse(claim, out userId);
    }
}
