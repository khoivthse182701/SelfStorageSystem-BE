using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Staff;

namespace SelfStorageSystem.Controllers;

[ApiController]
[Route("api/v1/staff")]
[Authorize(Roles = "Staff,Manager")]
public sealed class StaffV1Controller(IFacilityStaffService staffService) : ControllerBase
{
    [HttpGet("my-facility")]
    [ProducesResponseType(typeof(ApiResponse<StaffFacilityInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyFacility(CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var empId))
            return Unauthorized(ApiResponse.Fail("Cannot identify staff identity from token.", "Auth.UserTokenInvalid"));

        var result = await staffService.GetMyFacilityAsync(empId, ct);
        return Ok(ApiResponse<StaffFacilityInfoDto>.Ok(result, "Facility information retrieved successfully."));
    }

    [HttpGet("reservations/lookup")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StaffReservationLookupDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> LookupReservations(
        [FromQuery] long? facilityId,
        [FromQuery] string? query,
        [FromQuery] string? status = "confirmed",
        CancellationToken ct = default)
    {
        if (!TryGetEmployeeId(out var empId))
            return Unauthorized(ApiResponse.Fail("Cannot identify staff identity from token.", "Auth.UserTokenInvalid"));

        var list = await staffService.LookupReservationsV1Async(empId, facilityId, query, status, ct);
        return Ok(ApiResponse<IReadOnlyList<StaffReservationLookupDto>>.Ok(list, "Reservations retrieved successfully."));
    }

    [HttpPost("check-ins/verify-qr")]
    [ProducesResponseType(typeof(ApiResponse<VerifyCheckInQrResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> VerifyCheckInQr([FromBody] VerifyCheckInQrRequest request, CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var empId))
            return Unauthorized(ApiResponse.Fail("Cannot identify staff identity from token.", "Auth.UserTokenInvalid"));

        var result = await staffService.VerifyCheckInQrAsync(empId, request, ct);
        return Ok(ApiResponse<VerifyCheckInQrResponse>.Ok(result, "Check-in QR token verified successfully."));
    }

    [HttpPost("reservations/{reservationId:long}/assign-unit")]
    [ProducesResponseType(typeof(ApiResponse<AssignReservationUnitResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AssignUnit(
        long reservationId,
        [FromBody] AssignReservationUnitRequest request,
        CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var empId))
            return Unauthorized(ApiResponse.Fail("Cannot identify staff identity from token.", "Auth.UserTokenInvalid"));

        var result = await staffService.AssignReservationUnitV1Async(empId, reservationId, request, ct);
        return Ok(ApiResponse<AssignReservationUnitResponse>.Ok(result, "Unit assigned to reservation successfully."));
    }

    [HttpPost("check-ins/confirm")]
    [ProducesResponseType(typeof(ApiResponse<ConfirmCheckInResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConfirmCheckIn([FromBody] ConfirmCheckInRequest request, CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var empId))
            return Unauthorized(ApiResponse.Fail("Cannot identify staff identity from token.", "Auth.UserTokenInvalid"));

        var result = await staffService.ConfirmCheckInAsync(empId, request, ct);
        return Ok(ApiResponse<ConfirmCheckInResponse>.Ok(result, "Check-in and unit handover completed successfully."));
    }

    [HttpGet("units")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StaffUnitGridDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUnits(
        [FromQuery] long? facilityId,
        [FromQuery] string? status,
        [FromQuery] long? unitTypeId,
        [FromQuery] string? sizeCategory,
        CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var empId))
            return Unauthorized(ApiResponse.Fail("Cannot identify staff identity from token.", "Auth.UserTokenInvalid"));

        var result = await staffService.GetUnitsAsync(empId, facilityId, status, unitTypeId, sizeCategory, ct);
        return Ok(ApiResponse<IReadOnlyList<StaffUnitGridDto>>.Ok(result, "Facility units retrieved successfully."));
    }

    [HttpGet("units/{unitId:long}")]
    [ProducesResponseType(typeof(ApiResponse<StaffUnitDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUnitDetail(long unitId, CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var empId))
            return Unauthorized(ApiResponse.Fail("Cannot identify staff identity from token.", "Auth.UserTokenInvalid"));

        var result = await staffService.GetUnitDetailAsync(empId, unitId, ct);
        return Ok(ApiResponse<StaffUnitDetailDto>.Ok(result, "Unit detail retrieved successfully."));
    }

    [HttpGet("tickets")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StaffLogisticsTicketDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTickets(
        [FromQuery] long? facilityId,
        [FromQuery] string? category,
        [FromQuery] string? status,
        [FromQuery] bool? onlyAssignedToMe,
        CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var empId))
            return Unauthorized(ApiResponse.Fail("Cannot identify staff identity from token.", "Auth.UserTokenInvalid"));

        var result = await staffService.GetLogisticsTicketsAsync(empId, facilityId, category, status, onlyAssignedToMe, ct);
        return Ok(ApiResponse<IReadOnlyList<StaffLogisticsTicketDto>>.Ok(result, "Tickets retrieved successfully."));
    }

    [HttpPost("tickets")]
    [ProducesResponseType(typeof(ApiResponse<CreateCheckInTicketResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateTicket([FromBody] StaffCreateCheckInIncidentTicketRequest request, CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var empId))
            return Unauthorized(ApiResponse.Fail("Cannot identify staff identity from token.", "Auth.UserTokenInvalid"));

        var ticket = await staffService.CreateCheckInIncidentTicketAsync(empId, request, ct);
        var dto = new CreateCheckInTicketResponse(
            ticket.Id,
            ticket.TicketNo,
            ticket.Status,
            ticket.CreatedAt,
            ticket.FacilityId,
            ticket.StorageUnitId,
            ticket.UnitCode);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<CreateCheckInTicketResponse>.Ok(dto, "Incident ticket created successfully."));
    }

    // Flow 3: Access Events Monitoring, Move-Outs, Inspections & Counter Cash Payouts
    [HttpGet("access-events")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<StaffAccessEventDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAccessEvents(
        [FromQuery] long? facilityId,
        [FromQuery] long? agreementId,
        [FromQuery] long? unitId,
        [FromQuery] string? result,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!TryGetEmployeeId(out var empId))
            return Unauthorized(ApiResponse.Fail("Cannot identify staff identity from token.", "Auth.UserTokenInvalid"));

        var data = await staffService.GetAccessEventsAsync(empId, facilityId, agreementId, unitId, result, from, to, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<StaffAccessEventDto>>.Ok(data, "Access events retrieved successfully."));
    }

    [HttpPost("access-events")]
    [ProducesResponseType(typeof(ApiResponse<StaffAccessEventDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordAccessEvent([FromBody] CreateStaffAccessEventRequest request, CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var empId))
            return Unauthorized(ApiResponse.Fail("Cannot identify staff identity from token.", "Auth.UserTokenInvalid"));

        var data = await staffService.RecordAccessEventAsync(empId, request, ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<StaffAccessEventDto>.Ok(data, "Access event recorded successfully."));
    }

    [HttpGet("move-outs")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StaffMoveOutSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMoveOuts(
        [FromQuery] long? facilityId,
        [FromQuery] DateOnly? date,
        CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var empId))
            return Unauthorized(ApiResponse.Fail("Cannot identify staff identity from token.", "Auth.UserTokenInvalid"));

        var data = await staffService.GetMoveOutsAsync(empId, facilityId, date, ct);
        return Ok(ApiResponse<IReadOnlyList<StaffMoveOutSummaryDto>>.Ok(data, "Move-outs retrieved successfully."));
    }

    [HttpPost("move-outs/{moveOutId:long}/inspect")]
    [ProducesResponseType(typeof(ApiResponse<InspectMoveOutResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> InspectMoveOut(
        long moveOutId,
        [FromBody] InspectMoveOutRequest request,
        CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var empId))
            return Unauthorized(ApiResponse.Fail("Cannot identify staff identity from token.", "Auth.UserTokenInvalid"));

        var data = await staffService.InspectMoveOutAsync(empId, moveOutId, request, ct);
        return Ok(ApiResponse<InspectMoveOutResultDto>.Ok(data, "Move-out inspection completed successfully."));
    }

    [HttpGet("inspections")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StaffAssignedInspectionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAssignedInspections(
        [FromQuery] long? facilityId,
        [FromQuery] string? status,
        [FromQuery] DateOnly? date,
        CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var empId))
            return Unauthorized(ApiResponse.Fail("Cannot identify staff identity from token.", "Auth.UserTokenInvalid"));

        var data = await staffService.GetAssignedInspectionsAsync(empId, facilityId, status, date, ct);
        return Ok(ApiResponse<IReadOnlyList<StaffAssignedInspectionDto>>.Ok(data, "Assigned inspections retrieved successfully."));
    }

    [HttpPost("inspections/{inspectionId:long}/submit")]
    [ProducesResponseType(typeof(ApiResponse<StaffInspectionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitInspectionReport(
        long inspectionId,
        [FromBody] StaffSubmitInspectionReportRequest request,
        CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var empId))
            return Unauthorized(ApiResponse.Fail("Cannot identify staff identity from token.", "Auth.UserTokenInvalid"));

        var data = await staffService.SubmitInspectionReportAsync(empId, inspectionId, request, ct);
        return Ok(ApiResponse<StaffInspectionResultDto>.Ok(data, data.SettlementSummary));
    }

    [HttpGet("refunds/queue")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StaffRefundQueueItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRefundsQueue([FromQuery] long? facilityId, CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var empId))
            return Unauthorized(ApiResponse.Fail("Cannot identify staff identity from token.", "Auth.UserTokenInvalid"));

        var data = await staffService.GetPendingRefundsQueueAsync(empId, facilityId, ct);
        return Ok(ApiResponse<IReadOnlyList<StaffRefundQueueItemDto>>.Ok(data, "Pending refund queue retrieved successfully."));
    }

    [HttpPost("refunds/{refundId:long}/counter-cash-payout")]
    [ProducesResponseType(typeof(ApiResponse<StaffCounterCashPayoutResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CounterCashPayout(
        long refundId,
        [FromBody] StaffCounterCashPayoutRequest request,
        CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var empId))
            return Unauthorized(ApiResponse.Fail("Cannot identify staff identity from token.", "Auth.UserTokenInvalid"));

        var data = await staffService.ProcessCounterCashPayoutAsync(empId, refundId, request, ct);
        return Ok(ApiResponse<StaffCounterCashPayoutResultDto>.Ok(data, data.Message));
    }

    // Flow 6: Overdue Monitoring & Access Lock
    [HttpGet("overdue-agreements")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<OverdueAgreementDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOverdueAgreements([FromQuery] long? facilityId, CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var empId))
            return Unauthorized(ApiResponse.Fail("Cannot identify staff identity from token.", "Auth.UserTokenInvalid"));

        var data = await staffService.GetOverdueAgreementsAsync(empId, facilityId, ct);
        return Ok(ApiResponse<IReadOnlyList<OverdueAgreementDto>>.Ok(data, "Overdue agreements retrieved successfully."));
    }

    [HttpPost("agreements/{agreementId:long}/lock-access")]
    [ProducesResponseType(typeof(ApiResponse<StaffLockAccessResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> LockAgreementAccess(
        long agreementId,
        [FromBody] LockAccessRequest request,
        CancellationToken ct)
    {
        if (!TryGetEmployeeId(out var empId))
            return Unauthorized(ApiResponse.Fail("Cannot identify staff identity from token.", "Auth.UserTokenInvalid"));

        var data = await staffService.LockAgreementAccessV1Async(empId, agreementId, request, ct);
        return Ok(ApiResponse<StaffLockAccessResponseDto>.Ok(data, "Agreement access suspended successfully."));
    }

    private bool TryGetEmployeeId(out long employeeId)
    {
        employeeId = 0;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(claim) && long.TryParse(claim, out employeeId);
    }
}
