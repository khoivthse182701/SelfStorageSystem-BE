using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Staff;
using SelfStorageSystem.Domain.Constants;

namespace SelfStorageSystem.Controllers;

[ApiController]
[Route("api/staff")]
[Authorize(Roles = RoleConstants.StaffOrManager)]
public class StaffController : ControllerBase
{
    private readonly IFacilityStaffService _staffService;

    public StaffController(IFacilityStaffService staffService)
    {
        _staffService = staffService;
    }

    // ==========================================
    // 1. DAILY STAFF TASKS (Flow 5 & StaffTask)
    // ==========================================

    /// <summary>
    /// Gets staff task list during shift at facility.
    /// </summary>
    [HttpGet("tasks")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StaffTaskDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStaffTasks(
        [FromQuery] long? facilityId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var tasks = await _staffService.GetStaffTasksAsync(userId, facilityId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<StaffTaskDto>>.Ok(tasks, "Retrieved staff tasks successfully."));
    }

    /// <summary>
    /// Updates staff task progress or status.
    /// </summary>
    [HttpPut("tasks/{id:long}/status")]
    [ProducesResponseType(typeof(ApiResponse<StaffTaskDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateTaskStatus(
        [FromRoute] long id,
        [FromBody] UpdateStaffTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _staffService.UpdateStaffTaskStatusAsync(userId, id, request, cancellationToken);
        return Ok(ApiResponse<StaffTaskDto>.Ok(result, "Staff task status updated successfully."));
    }

    // ==========================================
    // 2. CHECK-IN & HANDOVER (Flow 2 & BR-RSV-04)
    // ==========================================

    /// <summary>
    /// Looks up reservation by code, phone number, or customer name.
    /// </summary>
    [HttpGet("reservations/lookup")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StaffReservationLookupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> LookupReservations(
        [FromQuery] string? query,
        [FromQuery] long? facilityId,
        CancellationToken cancellationToken)
    {
        var list = await _staffService.LookupReservationsAsync(query, facilityId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<StaffReservationLookupDto>>.Ok(list, "Reservations retrieved successfully."));
    }

    /// <summary>
    /// Assigns specific storage unit to reservation if not assigned.
    /// </summary>
    [HttpPost("reservations/{id:long}/assign-unit")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignUnit(
        [FromRoute] long id,
        [FromBody] AssignUnitToReservationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        await _staffService.AssignUnitToReservationAsync(userId, id, request, cancellationToken);
        return Ok(ApiResponse.Ok("Unit assigned to reservation successfully."));
    }

    /// <summary>
    /// Creates handover record, inspection items, and activates contract (Reservation -> Converted, Agreement -> Active, Unit -> Occupied).
    /// </summary>
    [HttpPost("handovers")]
    [ProducesResponseType(typeof(ApiResponse<StaffHandoverResultDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateHandover(
        [FromBody] CreateStaffHandoverRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _staffService.CreateHandoverAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<StaffHandoverResultDto>.Ok(result, "Handover record created and agreement activated successfully."));
    }

    // ==========================================
    // 3. MOVE-OUT & INSPECTION (Flow 6 & BR-FIN-02)
    // ==========================================

    /// <summary>
    /// Gets list of scheduled customer move-outs at facility.
    /// </summary>
    [HttpGet("move-outs")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StaffMoveOutSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMoveOuts(
        [FromQuery] long? facilityId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var list = await _staffService.GetMoveOutsAsync(facilityId, date, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<StaffMoveOutSummaryDto>>.Ok(list, "Move-outs retrieved successfully."));
    }

    /// <summary>
    /// Performs return inspection for customer move-out, records damages and deducts from deposit.
    /// </summary>
    [HttpPost("move-outs/{id:long}/inspect")]
    [ProducesResponseType(typeof(ApiResponse<InspectMoveOutResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> InspectMoveOut(
        [FromRoute] long id,
        [FromBody] InspectMoveOutRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _staffService.InspectMoveOutAsync(userId, id, request, cancellationToken);
        return Ok(ApiResponse<InspectMoveOutResultDto>.Ok(result, "Move-out inspection completed successfully."));
    }

    // ==========================================
    // 4. SUPPORT TICKETS (Flow 7 & Incident)
    // ==========================================

    /// <summary>
    /// Gets open support tickets for facility staff.
    /// </summary>
    [HttpGet("support-tickets")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StaffSupportTicketDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFacilityTickets(
        [FromQuery] long? facilityId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var list = await _staffService.GetFacilityTicketsAsync(facilityId, status, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<StaffSupportTicketDto>>.Ok(list, "Support tickets retrieved successfully."));
    }

    /// <summary>
    /// Assigns a support ticket to the current staff.
    /// </summary>
    [HttpPut("support-tickets/{id:long}/assign")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignTicket(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        await _staffService.AssignTicketToMeAsync(userId, id, cancellationToken);
        return Ok(ApiResponse.Ok("Ticket assigned successfully."));
    }

    /// <summary>
    /// Sends a response message to customer or internal staff note on ticket.
    /// </summary>
    [HttpPost("support-tickets/{id:long}/messages")]
    [ProducesResponseType(typeof(ApiResponse<StaffTicketMessageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddTicketMessage(
        [FromRoute] long id,
        [FromBody] AddStaffTicketMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _staffService.AddStaffTicketMessageAsync(userId, id, request, cancellationToken);
        return Ok(ApiResponse<StaffTicketMessageDto>.Ok(result, "Message added successfully."));
    }

    /// <summary>
    /// Proposes extra incident charges for manager approval.
    /// </summary>
    [HttpPost("support-tickets/{id:long}/propose-charge")]
    [ProducesResponseType(typeof(ApiResponse<TicketChargeProposalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProposeCharge(
        [FromRoute] long id,
        [FromBody] ProposeTicketChargeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _staffService.ProposeTicketChargeAsync(userId, id, request, cancellationToken);
        return Ok(ApiResponse<TicketChargeProposalDto>.Ok(result, "Ticket charge proposed successfully."));
    }

    /// <summary>
    /// Resolves support ticket.
    /// </summary>
    [HttpPut("support-tickets/{id:long}/resolve")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResolveTicket(
        [FromRoute] long id,
        [FromBody] ResolveTicketRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        await _staffService.ResolveTicketAsync(userId, id, request, cancellationToken);
        return Ok(ApiResponse.Ok("Ticket marked as resolved successfully."));
    }

    // ==========================================
    // 5. UNIT STATUS & MAINTENANCE (Flow 3 & 5)
    // ==========================================

    /// <summary>
    /// Gets units diagram and status list at facility.
    /// </summary>
    [HttpGet("facilities/{facilityId:long}/units")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StaffFacilityUnitDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFacilityUnits(
        [FromRoute] long facilityId,
        CancellationToken cancellationToken)
    {
        var list = await _staffService.GetFacilityUnitsAsync(facilityId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<StaffFacilityUnitDto>>.Ok(list, "Facility units retrieved successfully."));
    }

    /// <summary>
    /// Updates unit physical status (e.g. available, under_maintenance).
    /// </summary>
    [HttpPut("units/{unitId:long}/status")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateUnitStatus(
        [FromRoute] long unitId,
        [FromBody] UpdateUnitStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        await _staffService.UpdateUnitStatusAsync(userId, unitId, request, cancellationToken);
        return Ok(ApiResponse.Ok("Unit status updated successfully."));
    }

    /// <summary>
    /// Creates maintenance work order for storage unit.
    /// </summary>
    [HttpPost("units/{unitId:long}/maintenance-orders")]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceWorkOrderDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateMaintenanceOrder(
        [FromRoute] long unitId,
        [FromBody] CreateMaintenanceWorkOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _staffService.CreateMaintenanceOrderAsync(userId, unitId, request, cancellationToken);
        return Ok(ApiResponse<MaintenanceWorkOrderDto>.Ok(result, "Maintenance work order created successfully."));
    }

    // ==========================================
    // 6. OVERDUE ENFORCEMENT (Flow 6 & BR-REN-03)
    // ==========================================

    /// <summary>
    /// Gets list of overdue rental agreements at facility.
    /// </summary>
    [HttpGet("overdue-agreements")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<OverdueAgreementDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverdueAgreements(
        [FromQuery] long? facilityId,
        CancellationToken cancellationToken)
    {
        var list = await _staffService.GetOverdueAgreementsAsync(facilityId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<OverdueAgreementDto>>.Ok(list, "Overdue agreements retrieved successfully."));
    }

    /// <summary>
    /// Manually locks/suspends door access credentials for an overdue agreement.
    /// </summary>
    [HttpPost("agreements/{agreementId:long}/lock-access")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> LockAgreementAccess(
        [FromRoute] long agreementId,
        [FromBody] LockAccessRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        await _staffService.LockAgreementAccessAsync(userId, agreementId, request, cancellationToken);
        return Ok(ApiResponse.Ok("Agreement access credentials locked successfully."));
    }

    // ==========================================
    // HELPER
    // ==========================================

    private bool TryGetUserId(out long userId)
    {
        userId = 0;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(claim) && long.TryParse(claim, out userId);
    }
}
