using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Rentals;
using SelfStorageSystem.Contracts.Manager;
using SelfStorageSystem.Contracts.Operations;
using SelfStorageSystem.Domain.Constants;

namespace SelfStorageSystem.Controllers;

[ApiController]
[Route("api/manager")]
[Authorize(Roles = ManagerConstants.ManagerOrAdmin)]
public class ManagerController : ControllerBase
{
    private readonly IFacilityManagerService _managerService;
    private readonly IBusinessOperationsService _operationsService;

    public ManagerController(
        IFacilityManagerService managerService,
        IBusinessOperationsService operationsService)
    {
        _managerService = managerService;
        _operationsService = operationsService;
    }

    // ==========================================
    // 1. Quản Lý Hạ Tầng Ô Kho & Sơ Đồ
    // ==========================================

    /// <summary>
    /// Danh sách ô kho chi tiết kỹ thuật tại cơ sở.
    /// </summary>
    [HttpGet("facilities/{facilityId:long}/units")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ManagerStorageUnitDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnits(
        [FromRoute] long facilityId,
        [FromQuery] string? status,
        [FromQuery] long? unitTypeId,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.GetUnitsAsync(
            facilityId, status, unitTypeId, search, pageNumber, pageSize, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<PagedResult<ManagerStorageUnitDto>>.Ok(result, "Retrieved facility storage units successfully."));
    }

    /// <summary>
    /// Thêm mới ô kho vào hệ thống.
    /// </summary>
    [HttpPost("facilities/{facilityId:long}/units")]
    [ProducesResponseType(typeof(ApiResponse<ManagerStorageUnitDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateUnit(
        [FromRoute] long facilityId,
        [FromBody] ManagerCreateStorageUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.CreateUnitAsync(
            facilityId, request, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<ManagerStorageUnitDto>.Ok(result, "Storage unit created successfully."));
    }

    /// <summary>
    /// Sửa thông số kỹ thuật ô kho.
    /// </summary>
    [HttpPut("units/{unitId:long}")]
    [ProducesResponseType(typeof(ApiResponse<ManagerStorageUnitDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateUnit(
        [FromRoute] long unitId,
        [FromBody] ManagerUpdateStorageUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.UpdateUnitAsync(
            unitId, request, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<ManagerStorageUnitDto>.Ok(result, "Storage unit updated successfully."));
    }

    /// <summary>
    /// Lấy dữ liệu tọa độ sơ đồ bản đồ kho (unit_map_positions).
    /// </summary>
    [HttpGet("facilities/{facilityId:long}/map-layout")]
    [ProducesResponseType(typeof(ApiResponse<ManagerMapLayoutDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMapLayout(
        [FromRoute] long facilityId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.GetMapLayoutAsync(
            facilityId, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<ManagerMapLayoutDto>.Ok(result, "Retrieved facility map layout successfully."));
    }

    /// <summary>
    /// Cập nhật tọa độ sơ đồ bản đồ kho.
    /// </summary>
    [HttpPut("facilities/{facilityId:long}/map-layout")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMapLayout(
        [FromRoute] long facilityId,
        [FromBody] ManagerUpdateMapLayoutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var updatedCount = await _managerService.UpdateMapLayoutAsync(
            facilityId, request, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { updatedCount }, "Facility map layout updated successfully."));
    }

    /// <summary>
    /// Danh sách yêu cầu đổi kho của khách hàng.
    /// </summary>
    [HttpGet("facilities/{facilityId:long}/transfer-requests")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ManagerTransferRequestDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransferRequests(
        [FromRoute] long facilityId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.GetTransferRequestsAsync(
            facilityId, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<ManagerTransferRequestDto>>.Ok(result, "Retrieved transfer requests successfully."));
    }

    // ==========================================
    // 2. Phân Ca Trực & Giao Việc Nhân Viên
    // ==========================================

    /// <summary>
    /// Quản lý khung giờ ca trực nhân sự (staff_shifts) - Danh sách.
    /// </summary>
    [HttpGet("facilities/{facilityId:long}/shifts")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ManagerStaffShiftDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetShifts(
        [FromRoute] long facilityId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.GetShiftsAsync(
            facilityId, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<ManagerStaffShiftDto>>.Ok(result, "Retrieved shifts successfully."));
    }

    /// <summary>
    /// Quản lý khung giờ ca trực nhân sự (staff_shifts) - Tạo mới.
    /// </summary>
    [HttpPost("facilities/{facilityId:long}/shifts")]
    [ProducesResponseType(typeof(ApiResponse<ManagerStaffShiftDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateShift(
        [FromRoute] long facilityId,
        [FromBody] ManagerCreateShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.CreateShiftAsync(
            facilityId, request, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<ManagerStaffShiftDto>.Ok(result, "Staff shift created successfully."));
    }

    /// <summary>
    /// Xem lịch làm việc ma trận của nhân viên trong tuần/tháng.
    /// </summary>
    [HttpGet("facilities/{facilityId:long}/shift-roster")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ManagerShiftRosterItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetShiftRoster(
        [FromRoute] long facilityId,
        [FromQuery] DateTimeOffset? fromDate,
        [FromQuery] DateTimeOffset? toDate,
        [FromQuery] long? employeeId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.GetShiftRosterAsync(
            facilityId, fromDate, toDate, employeeId, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<ManagerShiftRosterItemDto>>.Ok(result, "Retrieved shift roster successfully."));
    }

    /// <summary>
    /// Phân ca trực cụ thể cho nhân sự.
    /// </summary>
    [HttpPost("facilities/{facilityId:long}/shift-assignments")]
    [ProducesResponseType(typeof(ApiResponse<ManagerShiftAssignmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignShift(
        [FromRoute] long facilityId,
        [FromBody] ManagerCreateShiftAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.AssignShiftAsync(
            facilityId, request, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<ManagerShiftAssignmentDto>.Ok(result, "Shift assignment completed successfully."));
    }

    /// <summary>
    /// Hủy phân công ca trực của nhân viên.
    /// </summary>
    [HttpDelete("facilities/{facilityId:long}/shift-assignments/{shiftId:long}/{employeeId:long}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteShiftAssignment(
        [FromRoute] long facilityId,
        [FromRoute] long shiftId,
        [FromRoute] long employeeId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        await _managerService.DeleteShiftAssignmentAsync(
            facilityId, shiftId, employeeId, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse.Ok("Shift assignment removed successfully."));
    }

    /// <summary>
    /// Lưu hàng loạt phân công ca trực (Thực thi khi nhấn nút Save trên giao diện).
    /// </summary>
    [HttpPost("facilities/{facilityId:long}/shift-assignments/batch-save")]
    [ProducesResponseType(typeof(ApiResponse<ManagerBatchSaveShiftAssignmentsResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BatchSaveShiftAssignments(
        [FromRoute] long facilityId,
        [FromBody] ManagerBatchSaveShiftAssignmentsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.BatchSaveShiftAssignmentsAsync(
            facilityId, request, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<ManagerBatchSaveShiftAssignmentsResultDto>.Ok(result, "Batch shift assignments saved successfully."));
    }

    /// <summary>
    /// Danh sách nhân sự phụ trách thuộc điểm kho (Facility Staff).
    /// </summary>
    [HttpGet("facilities/{facilityId:long}/staff")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ManagerFacilityStaffDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFacilityStaff(
        [FromRoute] long facilityId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.GetFacilityStaffAsync(
            facilityId, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<ManagerFacilityStaffDto>>.Ok(result, "Retrieved facility staff successfully."));
    }

    /// <summary>
    /// Manager chủ động giao task/nhiệm vụ cho Staff.
    /// </summary>
    [HttpPost("facilities/{facilityId:long}/staff-tasks")]
    [HttpPost("facilities/{facilityId:long}/tickets")]
    [ProducesResponseType(typeof(ApiResponse<ManagerStaffTaskDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateStaffTask(
        [FromRoute] long facilityId,
        [FromBody] ManagerCreateStaffTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.CreateStaffTaskAsync(
            facilityId, request, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<ManagerStaffTaskDto>.Ok(result, "Staff task created successfully."));
    }

    // ==========================================
    // 3. Phê Duyệt Tài Chính
    // ==========================================

    /// <summary>
    /// Danh sách phụ phí hư hỏng chờ ban quản lý duyệt.
    /// </summary>
    [HttpGet("facilities/{facilityId:long}/charge-proposals")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ManagerChargeProposalDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChargeProposals(
        [FromRoute] long facilityId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.GetChargeProposalsAsync(
            facilityId, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<ManagerChargeProposalDto>>.Ok(result, "Retrieved charge proposals successfully."));
    }

    /// <summary>
    /// Phê duyệt phụ phí hư hỏng & tự động xuất hóa đơn (invoices).
    /// </summary>
    [HttpPut("charge-proposals/{id:long}/decide")]
    [ProducesResponseType(typeof(ApiResponse<ManagerChargeProposalDecisionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DecideChargeProposal(
        [FromRoute] long id,
        [FromBody] ManagerDecideChargeProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.DecideChargeProposalAsync(
            id, request, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<ManagerChargeProposalDecisionDto>.Ok(result, "Charge proposal decided successfully."));
    }

    /// <summary>
    /// Danh sách yêu cầu hoàn cọc trả kho chờ duyệt.
    /// </summary>
    [HttpGet("facilities/{facilityId:long}/refund-approvals")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ManagerRefundApprovalDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRefundApprovals(
        [FromRoute] long facilityId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.GetRefundApprovalsAsync(
            facilityId, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<ManagerRefundApprovalDto>>.Ok(result, "Retrieved refund approvals successfully."));
    }

    /// <summary>
    /// Phê duyệt hoàn cọc theo công thức: Cọc - Hư hại - Phạt quá hạn.
    /// </summary>
    [HttpPut("refund-approvals/{id:long}/decide")]
    [ProducesResponseType(typeof(ApiResponse<ManagerRefundDecisionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DecideRefundApproval(
        [FromRoute] long id,
        [FromBody] ManagerDecideRefundApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.DecideRefundApprovalAsync(
            id, request, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<ManagerRefundDecisionDto>.Ok(result, "Refund approval decided successfully."));
    }

    // ==========================================
    // 4. Giám Sát Hợp Đồng & Xử Lý Quá Hạn Kéo Dài
    // ==========================================

    /// <summary>
    /// Giám sát toàn bộ hợp đồng tại cơ sở phụ trách.
    /// </summary>
    [HttpGet("facilities/{facilityId:long}/agreements")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ManagerAgreementSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAgreements(
        [FromRoute] long facilityId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.GetAgreementsAsync(
            facilityId, status, search, pageNumber, pageSize, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<PagedResult<ManagerAgreementSummaryDto>>.Ok(result, "Retrieved agreements successfully."));
    }

    /// <summary>
    /// Chi tiết hợp đồng, công nợ hiện tại và danh mục đồ lưu trữ.
    /// </summary>
    [HttpGet("agreements/{agreementId:long}")]
    [ProducesResponseType(typeof(ApiResponse<ManagerAgreementDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAgreementDetail(
        [FromRoute] long agreementId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.GetAgreementDetailAsync(
            agreementId, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<ManagerAgreementDetailDto>.Ok(result, "Retrieved agreement detail successfully."));
    }

    /// <summary>
    /// Danh sách kho quá hạn > 30 ngày (delinquency_cases).
    /// </summary>
    [HttpGet("facilities/{facilityId:long}/delinquent-cases")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ManagerDelinquencyCaseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDelinquentCases(
        [FromRoute] long facilityId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.GetDelinquentCasesAsync(
            facilityId, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<ManagerDelinquencyCaseDto>>.Ok(result, "Retrieved delinquent cases successfully."));
    }

    /// <summary>
    /// Ghi nhận niêm phong / xử lý tài sản theo quy tắc nghiệp vụ BR-REN-04.
    /// </summary>
    [HttpPost("delinquent-cases/{id:long}/actions")]
    [ProducesResponseType(typeof(ApiResponse<ManagerDelinquencyActionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordDelinquencyAction(
        [FromRoute] long id,
        [FromBody] ManagerAddDelinquencyActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.AddDelinquencyActionAsync(
            id, request, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<ManagerDelinquencyActionDto>.Ok(result, "Delinquency action recorded successfully."));
    }

    // ==========================================
    // 5. Dashboard Cơ Sở
    // ==========================================

    /// <summary>
    /// Tỷ lệ lấp đầy (Occupancy Rate), lịch check-in/out ngày, ticket sự cố và doanh thu tháng.
    /// </summary>
    [HttpGet("facilities/{facilityId:long}/dashboard")]
    [ProducesResponseType(typeof(ApiResponse<ManagerFacilityDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFacilityDashboard(
        [FromRoute] long facilityId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.GetFacilityDashboardAsync(
            facilityId, userId, isSystemAdmin, cancellationToken);

        return Ok(ApiResponse<ManagerFacilityDashboardDto>.Ok(result, "Retrieved facility dashboard successfully."));
    }

    /// <summary>
    /// Danh sách cơ sở mà Manager hiện tại được phân công quản lý (hoặc toàn bộ nếu là Admin).
    /// </summary>
    [HttpGet("my-facilities")]
    [ProducesResponseType(typeof(ApiResponse<List<ManagerFacilityScopeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyFacilities(CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _managerService.GetMyFacilitiesAsync(userId, isSystemAdmin, cancellationToken);
        return Ok(ApiResponse<List<ManagerFacilityScopeDto>>.Ok(result, "Retrieved manager facilities successfully."));
    }

    // ==========================================
    // 6. Quản Lý Giá Thuê, Phí & Khuyến Mãi (Sáp nhập từ Operations)
    // ==========================================

    /// <summary>
    /// Biểu giá thuê ô kho tại cơ sở (áp dụng theo loại kho và thời điểm hiệu lực).
    /// </summary>
    [HttpGet("facilities/{facilityId:long}/pricing/rates")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<OperationsRateDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetFacilityRates(
        [FromRoute] long facilityId,
        [FromQuery] OperationsRateQueryParameters query,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        await _managerService.ValidateFacilityAccessAsync(facilityId, userId, isSystemAdmin, cancellationToken);
        query.FacilityId = facilityId;

        var result = await _operationsService.GetRatesAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<OperationsRateDto>>.Ok(result, "Retrieved facility rates successfully."));
    }

    /// <summary>
    /// Cập nhật / thiết lập biểu giá thuê mới cho loại kho tại cơ sở.
    /// Giá mới có hiệu lực từ ValidFrom, không sửa đổi hồi tố hợp đồng đang active.
    /// </summary>
    [HttpPut("facilities/{facilityId:long}/pricing/rates")]
    [ProducesResponseType(typeof(ApiResponse<OperationsRateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpsertFacilityRate(
        [FromRoute] long facilityId,
        [FromBody] OperationsUpsertRateRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        await _managerService.ValidateFacilityAccessAsync(facilityId, userId, isSystemAdmin, cancellationToken);
        request.FacilityId = facilityId;

        var result = await _operationsService.UpsertRateAsync(request, userId, cancellationToken);
        return Ok(ApiResponse<OperationsRateDto>.Ok(result, "Facility rate saved successfully."));
    }

    /// <summary>
    /// Tra cứu danh mục quy tắc phí chuẩn hệ thống (phí vệ sinh, quá hạn, hư hỏng).
    /// </summary>
    [HttpGet("pricing/fees")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<OperationsFeeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFees(
        [FromQuery] OperationsFeeQueryParameters query,
        CancellationToken cancellationToken)
    {
        var result = await _operationsService.GetFeesAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<OperationsFeeDto>>.Ok(result, "Retrieved fee rules successfully."));
    }

    /// <summary>
    /// Danh sách chương trình khuyến mãi / voucher.
    /// </summary>
    [HttpGet("promotions")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<OperationsPromotionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPromotions(
        [FromQuery] OperationsPromotionQueryParameters query,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _operationsService.GetPromotionsAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<OperationsPromotionDto>>.Ok(result, "Retrieved promotions successfully."));
    }

    /// <summary>
    /// Tạo mới chiến dịch khuyến mãi / voucher cho cơ sở.
    /// </summary>
    [HttpPost("promotions")]
    [ProducesResponseType(typeof(ApiResponse<OperationsPromotionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePromotion(
        [FromBody] OperationsCreatePromotionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        if (!isSystemAdmin)
        {
            var facilityRules = request.Rules.Where(r => r.RuleType.Equals(PromotionRuleTypeConstants.Facility, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var rule in facilityRules)
            {
                if (long.TryParse(rule.RuleValue, out var targetFacilityId))
                {
                    await _managerService.ValidateFacilityAccessAsync(targetFacilityId, userId, isSystemAdmin, cancellationToken);
                }
            }
        }

        var result = await _operationsService.CreatePromotionAsync(request, userId, cancellationToken);
        return Ok(ApiResponse<OperationsPromotionDto>.Ok(result, "Promotion created successfully."));
    }

    /// <summary>
    /// Kích hoạt hoặc tạm ngưng chương trình khuyến mãi.
    /// </summary>
    [HttpPut("promotions/{id:long}/status")]
    [ProducesResponseType(typeof(ApiResponse<OperationsPromotionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePromotionStatus(
        [FromRoute] long id,
        [FromBody] OperationsUpdatePromotionStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var isSystemAdmin))
            return Unauthorized(ApiResponse.Fail("Cannot identify user identity from token."));

        var result = await _operationsService.UpdatePromotionStatusAsync(id, request, userId, cancellationToken);
        return Ok(ApiResponse<OperationsPromotionDto>.Ok(result, "Promotion status updated successfully."));
    }

    // ==========================================
    // HELPER
    // ==========================================

    private bool TryGetCurrentUser(out long userId, out bool isSystemAdmin)
    {
        isSystemAdmin = User.IsInRole(RoleConstants.SystemAdministrator);
        userId = 0;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(claim) && long.TryParse(claim, out userId);
    }
}
