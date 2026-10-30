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
[Authorize(Roles = RoleConstants.StaffOrManagerOrAdmin)]
public class StaffController : ControllerBase
{
    private readonly IFacilityStaffService _staffService;

    public StaffController(IFacilityStaffService staffService)
    {
        _staffService = staffService;
    }

    /// <summary>
    /// Lấy thông tin cơ sở kho mà nhân viên đang được phân công làm việc.
    /// </summary>
    [HttpGet("my-facility")]
    [ProducesResponseType(typeof(ApiResponse<StaffFacilityInfoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyFacility(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _staffService.GetMyFacilityAsync(userId, cancellationToken);
        return Ok(ApiResponse<StaffFacilityInfoDto>.Ok(result, "Retrieved assigned facility successfully."));
    }

    // ==========================================
    // 🏢 NHÓM 1: XEM HIỆN TRẠNG SƠ ĐỒ KHO (Unit Availability)
    // ==========================================

    /// <summary>
    /// Lấy danh sách & sơ đồ tất cả ô kho tại cơ sở của Staff (lọc theo trạng thái, loại kho, kích thước).
    /// </summary>
    [HttpGet("units")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StaffUnitGridDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnits(
        [FromQuery] long? facilityId,
        [FromQuery] string? status,
        [FromQuery] long? unitTypeId,
        [FromQuery] string? sizeCategory,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var list = await _staffService.GetUnitsAsync(userId, facilityId, status, unitTypeId, sizeCategory, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<StaffUnitGridDto>>.Ok(list, "Retrieved facility units successfully."));
    }

    /// <summary>
    /// Xem chi tiết 1 ô kho cụ thể (kích thước, giá thuê, tình trạng hiện tại, thông tin hợp đồng đang thuê).
    /// </summary>
    [HttpGet("units/{unitId:long}")]
    [ProducesResponseType(typeof(ApiResponse<StaffUnitDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnitDetail(
        [FromRoute] long unitId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _staffService.GetUnitDetailAsync(userId, unitId, cancellationToken);
        return Ok(ApiResponse<StaffUnitDetailDto>.Ok(result, "Retrieved unit detail successfully."));
    }

    /// <summary>
    /// Cập nhật trạng thái vật lý của ô kho (available, under_maintenance, cleaning, damaged...).
    /// </summary>
    [HttpPut("facilities/{facilityId:long}/units/{unitId:long}/physical-status")]
    [HttpPut("units/{unitId:long}/status")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUnitStatus(
        [FromRoute] long unitId,
        [FromRoute] long? facilityId,
        [FromBody] UpdateUnitStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        await _staffService.UpdateUnitStatusAsync(userId, unitId, request, cancellationToken);
        return Ok(ApiResponse.Ok("Unit physical status updated successfully."));
    }

    // ==========================================
    // 🛒 NHÓM 2: ĐẶT THUÊ KHO TẠI QUẦY (Walk-in Booking)
    // ==========================================

    /// <summary>
    /// Staff tạo đơn thuê tại quầy cho khách (Tìm/tạo tài khoản khách bằng SĐT -> Chọn ô kho Available -> Tính tiền cọc 1 tháng + Tiền thuê -> Trả về mã QR VietQR để khách quét ngay tại quầy).
    /// </summary>
    [HttpPost("walk-in-bookings")]
    [ProducesResponseType(typeof(ApiResponse<StaffWalkInBookingResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateWalkInBooking(
        [FromBody] StaffWalkInBookingRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _staffService.CreateWalkInBookingAsync(userId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<StaffWalkInBookingResponse>.Ok(result, "Walk-in booking created successfully. Present QR to customer for payment."));
    }

    // ==========================================
    // 📷 NHÓM 3: QUÉT QR (Check-in nhận kho / Check-out dọn kho / Ra vào)
    // ==========================================

    /// <summary>
    /// Staff quét mã QR của khách -> Hệ thống giải mã và trả về thông tin Hợp đồng/Yêu cầu chuyển kho kèm hành động tương ứng (VD: "Khách Nguyễn Văn A - Nhận kho HN", hoặc "Khách dọn kho HCM chuyển đi").
    /// </summary>
    [HttpPost("qr/verify")]
    [ProducesResponseType(typeof(ApiResponse<StaffQrVerifyResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyQr(
        [FromBody] StaffQrVerifyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _staffService.VerifyQrAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<StaffQrVerifyResponse>.Ok(result, "QR verified successfully."));
    }

    /// <summary>
    /// Sau khi kiểm tra xong, Staff bấm xác nhận trên màn hình quét QR để chốt trạng thái:
    /// • Nếu là Nhận kho: Kích hoạt hợp đồng, đổi kho sang Occupied.
    /// • Nếu là Dọn kho HCM (chuyển cơ sở): Đổi trạng thái chuyển kho sang MovedOut_InTransit, nhả kho HCM (hoặc tạo ticket dọn vệ sinh).
    /// </summary>
    [HttpPost("qr/confirm-action")]
    [ProducesResponseType(typeof(ApiResponse<StaffConfirmQrActionResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmQrAction(
        [FromBody] StaffConfirmQrActionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _staffService.ConfirmQrActionAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<StaffConfirmQrActionResultDto>.Ok(result, result.Message));
    }

    // ==========================================
    // 🛠️ NHÓM 4: HỖ TRỢ TICKET VỀ LOGISTICS (Dọn dẹp, Sửa chữa, Kiểm tra kho)
    // ==========================================

    /// <summary>
    /// Lấy danh sách các Ticket liên quan đến vận hành/logistics tại cơ sở (Dọn kho, sửa chữa, kiểm tra hư hại, hỗ trợ khách).
    /// </summary>
    [HttpGet("tickets")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StaffLogisticsTicketDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLogisticsTickets(
        [FromQuery] long? facilityId,
        [FromQuery] string? category,
        [FromQuery] string? status,
        [FromQuery] bool? onlyAssignedToMe,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var list = await _staffService.GetLogisticsTicketsAsync(userId, facilityId, category, status, onlyAssignedToMe, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<StaffLogisticsTicketDto>>.Ok(list, "Retrieved logistics tickets successfully."));
    }

    /// <summary>
    /// Staff nhận xử lý Ticket (hoặc xem Ticket đã được hệ thống/Manager gán cho mình).
    /// </summary>
    [HttpPut("tickets/{id:long}/assign")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignTicket(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        await _staffService.AssignTicketToStaffAsync(userId, id, cancellationToken);
        return Ok(ApiResponse.Ok("Ticket assigned to staff successfully."));
    }

    /// <summary>
    /// Staff gửi phản hồi hoặc ghi chú nội bộ cho Ticket.
    /// </summary>
    [HttpPost("tickets/{id:long}/messages")]
    [HttpPost("support-tickets/{id:long}/messages")]
    [ProducesResponseType(typeof(ApiResponse<StaffTicketMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddTicketMessage(
        [FromRoute] long id,
        [FromBody] AddStaffTicketMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _staffService.AddStaffTicketMessageAsync(userId, id, request, cancellationToken);
        return Ok(ApiResponse<StaffTicketMessageDto>.Ok(result, "Ticket message added successfully."));
    }

    /// <summary>
    /// (Tùy chọn) Nếu lúc đi dọn kho hoặc sửa chữa phát hiện khách làm hỏng đồ/bẩn kho -> Staff chụp ảnh và đề xuất phí phạt/phí vệ sinh gửi Manager duyệt trừ cọc.
    /// </summary>
    [HttpPost("tickets/{id:long}/propose-charge")]
    [ProducesResponseType(typeof(ApiResponse<TicketChargeProposalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProposeTicketCharge(
        [FromRoute] long id,
        [FromBody] StaffProposeChargeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _staffService.ProposeTicketChargeAsync(userId, id, request, cancellationToken);
        return Ok(ApiResponse<TicketChargeProposalDto>.Ok(result, "Ticket charge proposed successfully."));
    }

    /// <summary>
    /// Staff hoàn tất công việc (dọn xong / sửa xong).
    /// Lưu ý: Nếu đây là ticket "Dọn vệ sinh/Sửa chữa ô kho", khi Staff bấm complete, hệ thống tự động mở khóa ô kho đó từ Maintenance/Cleaning về lại Available để bán tiếp!
    /// </summary>
    [HttpPut("tickets/{id:long}/complete")]
    [ProducesResponseType(typeof(ApiResponse<StaffCompleteTicketResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteTicket(
        [FromRoute] long id,
        [FromBody] StaffCompleteTicketRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _staffService.CompleteTicketAsync(userId, id, request, cancellationToken);
        return Ok(ApiResponse<StaffCompleteTicketResultDto>.Ok(result, "Ticket completed successfully."));
    }

    // ==========================================
    // 🗓️ NHÓM 5: CA TRỰC & LỊCH LÀM VIỆC (Staff Shifts Schedule)
    // ==========================================

    /// <summary>
    /// Lấy danh sách ca trực / lịch làm việc của nhân viên đang đăng nhập.
    /// </summary>
    [HttpGet("shifts")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StaffShiftScheduleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetShifts(
        [FromQuery] long? facilityId,
        [FromQuery] DateTimeOffset? fromDate,
        [FromQuery] DateTimeOffset? toDate,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _staffService.GetStaffShiftsAsync(userId, facilityId, fromDate, toDate, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<StaffShiftScheduleDto>>.Ok(result, "Staff shifts retrieved successfully."));
    }

    /// <summary>
    /// Điểm danh vào ca trực.
    /// </summary>
    [HttpPost("shifts/{shiftId:long}/check-in")]
    [ProducesResponseType(typeof(ApiResponse<StaffShiftScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckInShift(
        [FromRoute] long shiftId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _staffService.CheckInShiftAsync(userId, shiftId, cancellationToken);
        return Ok(ApiResponse<StaffShiftScheduleDto>.Ok(result, "Shift checked in successfully."));
    }

    /// <summary>
    /// Điểm danh kết thúc ca trực.
    /// </summary>
    [HttpPost("shifts/{shiftId:long}/check-out")]
    [ProducesResponseType(typeof(ApiResponse<StaffShiftScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckOutShift(
        [FromRoute] long shiftId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _staffService.CheckOutShiftAsync(userId, shiftId, cancellationToken);
        return Ok(ApiResponse<StaffShiftScheduleDto>.Ok(result, "Shift checked out successfully."));
    }

    // ==========================================
    // 📦 NHÓM 6: MOVE-OUTS, TASKS, RESERVATIONS & OPERATIONS
    // ==========================================

    /// <summary>
    /// Danh sách yêu cầu trả kho (Move-out requests) theo ngày và cơ sở.
    /// </summary>
    [HttpGet("move-outs")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StaffMoveOutSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMoveOuts(
        [FromQuery] long? facilityId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var list = await _staffService.GetMoveOutsAsync(facilityId, date, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<StaffMoveOutSummaryDto>>.Ok(list, "Move-out requests retrieved successfully."));
    }

    /// <summary>
    /// Nhân viên nghiệm thu trả kho (Move-out inspection).
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
        return Ok(ApiResponse<InspectMoveOutResultDto>.Ok(result, "Move-out inspected successfully."));
    }

    /// <summary>
    /// Danh sách ô kho theo cơ sở cho Staff.
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
    /// Danh sách nhiệm vụ của nhân viên tại cơ sở.
    /// </summary>
    [HttpGet("tasks")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StaffTaskDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStaffTasks(
        [FromQuery] long? facilityId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var list = await _staffService.GetStaffTasksAsync(userId, facilityId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<StaffTaskDto>>.Ok(list, "Staff tasks retrieved successfully."));
    }

    /// <summary>
    /// Cập nhật tiến độ hoặc trạng thái nhiệm vụ.
    /// </summary>
    [HttpPut("tasks/{id:long}/status")]
    [ProducesResponseType(typeof(ApiResponse<StaffTaskDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStaffTaskStatus(
        [FromRoute] long id,
        [FromBody] UpdateStaffTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _staffService.UpdateStaffTaskStatusAsync(userId, id, request, cancellationToken);
        return Ok(ApiResponse<StaffTaskDto>.Ok(result, "Staff task status updated successfully."));
    }

    /// <summary>
    /// Tra cứu đặt chỗ (reservation lookup).
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
    /// Phân bổ ô kho cụ thể cho đơn đặt chỗ.
    /// </summary>
    [HttpPost("reservations/{id:long}/assign-unit")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignUnitToReservation(
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
    /// Tạo biên bản bàn giao kho (Handover).
    /// </summary>
    [HttpPost("handovers")]
    [ProducesResponseType(typeof(ApiResponse<StaffHandoverResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateHandover(
        [FromBody] CreateStaffHandoverRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _staffService.CreateHandoverAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<StaffHandoverResultDto>.Ok(result, "Handover created successfully."));
    }

    /// <summary>
    /// Danh sách hợp đồng quá hạn tại cơ sở.
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
    /// Khóa quyền truy cập cửa đối với hợp đồng quá hạn nợ.
    /// </summary>
    [HttpPost("agreements/{agreementId:long}/lock-access")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> LockAgreementAccess(
        [FromRoute] long agreementId,
        [FromBody] LockAccessRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        await _staffService.LockAgreementAccessAsync(userId, agreementId, request, cancellationToken);
        return Ok(ApiResponse.Ok("Agreement access locked successfully."));
    }

    /// <summary>
    /// Tạo yêu cầu bảo trì / sửa chữa ô kho.
    /// </summary>
    [HttpPost("units/{unitId:long}/maintenance-orders")]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceWorkOrderDto>), StatusCodes.Status200OK)]
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
    // HELPER
    // ==========================================

    private bool TryGetUserId(out long userId)
    {
        userId = 0;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(claim) && long.TryParse(claim, out userId);
    }
}
