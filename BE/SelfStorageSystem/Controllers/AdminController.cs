using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Admin;
using SelfStorageSystem.Contracts.Operations;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Domain.Constants;

namespace SelfStorageSystem.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = ManagerConstants.ManagerOrAdmin)]
public class AdminController : ControllerBase
{
    private readonly IAdminManagementService _adminService;
    private readonly IBusinessOperationsService _operationsService;

    public AdminController(
        IAdminManagementService adminService,
        IBusinessOperationsService operationsService)
    {
        _adminService = adminService;
        _operationsService = operationsService;
    }

    // ==========================================
    // 1. Quản lý Người Dùng & Phân Quyền (RBAC)
    // ==========================================

    /// <summary>
    /// Danh sách người dùng, tìm kiếm & bộ lọc nâng cao (role, status, facilityId).
    /// </summary>
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    [HttpGet("users")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AdminUserSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] AdminUserQueryParameters query,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.GetUsersAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<AdminUserSummaryDto>>.Ok(result, "Retrieved user list successfully."));
    }

    /// <summary>
    /// Chi tiết hồ sơ nhân viên / khách hàng & lịch sử đăng nhập.
    /// </summary>
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    [HttpGet("users/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserDetail(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.GetUserDetailAsync(id, cancellationToken);
        return Ok(ApiResponse<AdminUserDetailDto>.Ok(result, "Retrieved user profile detail successfully."));
    }

    /// <summary>
    /// Tạo tài khoản nội bộ mới cho Staff / Manager / Admin.
    /// </summary>
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    [HttpPost("users")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUser(
        [FromBody] AdminCreateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var adminUserId))
            return Unauthorized(ApiResponse.Fail("Cannot identify administrator identity from token."));

        var result = await _adminService.CreateInternalUserAsync(request, adminUserId, cancellationToken);
        return Ok(ApiResponse<AdminUserDetailDto>.Ok(result, "Internal user created successfully."));
    }

    /// <summary>
    /// Khóa hoặc kích hoạt tài khoản người dùng.
    /// </summary>
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    [HttpPut("users/{id:long}/status")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserStatus(
        [FromRoute] long id,
        [FromBody] AdminUpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var adminUserId))
            return Unauthorized(ApiResponse.Fail("Cannot identify administrator identity from token."));

        var result = await _adminService.UpdateUserStatusAsync(id, request, adminUserId, cancellationToken);
        return Ok(ApiResponse<AdminUserSummaryDto>.Ok(result, "User status updated successfully."));
    }

    /// <summary>
    /// Gán & cập nhật vai trò người dùng ([core].[user_roles]).
    /// </summary>
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    [HttpPut("users/{id:long}/roles")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<string>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserRoles(
        [FromRoute] long id,
        [FromBody] AdminUpdateUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var adminUserId))
            return Unauthorized(ApiResponse.Fail("Cannot identify administrator identity from token."));

        var result = await _adminService.UpdateUserRolesAsync(id, request, adminUserId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<string>>.Ok(result, "User roles updated successfully."));
    }

    // ==========================================
    // 2. Phân Bổ Nhân Sự Điểm Kho (Data Scope)
    // ==========================================

    /// <summary>
    /// Gán nhân sự vào điểm kho phụ trách ([core].[staff_facility_assignments]).
    /// </summary>
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    [HttpPost("staff-assignments")]
    [ProducesResponseType(typeof(ApiResponse<AdminStaffFacilityAssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignStaffToFacility(
        [FromBody] AdminCreateStaffAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var adminUserId))
            return Unauthorized(ApiResponse.Fail("Cannot identify administrator identity from token."));

        var result = await _adminService.AssignStaffToFacilityAsync(request, adminUserId, cancellationToken);
        return Ok(ApiResponse<AdminStaffFacilityAssignmentDto>.Ok(result, "Staff assigned to facility successfully."));
    }

    /// <summary>
    /// Thu hồi điều chuyển nhân sự khỏi điểm kho.
    /// </summary>
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    [HttpDelete("staff-assignments/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeStaffAssignment(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var adminUserId))
            return Unauthorized(ApiResponse.Fail("Cannot identify administrator identity from token."));

        await _adminService.RevokeStaffAssignmentAsync(id, adminUserId, cancellationToken);
        return Ok(ApiResponse.Ok("Staff facility assignment revoked successfully."));
    }

    // ==========================================
    // 3. Bảo Mật & Kiểm Toán
    // ==========================================

    /// <summary>
    /// Tra cứu nhật ký kiểm toán hệ thống ([core].[audit_logs]).
    /// </summary>
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AdminAuditLogDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] AdminAuditLogQueryParameters query,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.GetAuditLogsAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<AdminAuditLogDto>>.Ok(result, "Retrieved audit logs successfully."));
    }

    /// <summary>
    /// Xem nhật ký đăng nhập & cảnh báo các hành vi đăng nhập bất thường.
    /// </summary>
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    [HttpGet("login-histories")]
    [ProducesResponseType(typeof(ApiResponse<AdminLoginHistoryReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLoginHistories(
        [FromQuery] AdminLoginHistoryQueryParameters query,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.GetLoginHistoriesAsync(query, cancellationToken);
        return Ok(ApiResponse<AdminLoginHistoryReportDto>.Ok(result, "Retrieved login histories and security alerts successfully."));
    }

    /// <summary>
    /// Thống kê KPI chuỗi kho (doanh thu tổng, tỷ lệ lấp đầy toàn hệ thống).
    /// </summary>
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    [HttpGet("dashboard/overview")]
    [ProducesResponseType(typeof(ApiResponse<AdminDashboardOverviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardOverview(CancellationToken cancellationToken)
    {
        var result = await _adminService.GetDashboardOverviewAsync(cancellationToken);
        return Ok(ApiResponse<AdminDashboardOverviewDto>.Ok(result, "Retrieved dashboard overview KPIs successfully."));
    }

    // ==========================================
    // 4. Quản lý Cơ Sở (Facilities)
    // ==========================================

    /// <summary>
    /// Danh sách cơ sở (kèm số ô kho và nhân sự đang hoạt động).
    /// </summary>
    [HttpGet("facilities")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AdminFacilitySummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFacilities(
        [FromQuery] AdminFacilityQueryParameters query,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.GetFacilitiesAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<AdminFacilitySummaryDto>>.Ok(result, "Retrieved facilities successfully."));
    }

    /// <summary>
    /// Chi tiết cơ sở (kèm nhân sự đang gán và thống kê trạng thái ô kho).
    /// </summary>
    [HttpGet("facilities/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<AdminFacilityDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFacilityDetail(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.GetFacilityDetailAsync(id, cancellationToken);
        return Ok(ApiResponse<AdminFacilityDetailDto>.Ok(result, "Retrieved facility detail successfully."));
    }

    /// <summary>
    /// Tạo cơ sở mới.
    /// </summary>
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    [HttpPost("facilities")]
    [ProducesResponseType(typeof(ApiResponse<AdminFacilityDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateFacility(
        [FromBody] AdminCreateFacilityRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var adminUserId))
            return Unauthorized(ApiResponse.Fail("Cannot identify administrator identity from token."));

        var result = await _adminService.CreateFacilityAsync(request, adminUserId, cancellationToken);
        return Ok(ApiResponse<AdminFacilityDetailDto>.Ok(result, "Facility created successfully."));
    }

    /// <summary>
    /// Cập nhật thông tin cơ sở.
    /// </summary>
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    [HttpPut("facilities/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<AdminFacilityDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateFacility(
        [FromRoute] long id,
        [FromBody] AdminUpdateFacilityRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var adminUserId))
            return Unauthorized(ApiResponse.Fail("Cannot identify administrator identity from token."));

        var result = await _adminService.UpdateFacilityAsync(id, request, adminUserId, cancellationToken);
        return Ok(ApiResponse<AdminFacilityDetailDto>.Ok(result, "Facility updated successfully."));
    }

    /// <summary>
    /// Cập nhật nhanh trạng thái cơ sở (active / inactive / maintenance).
    /// </summary>
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    [HttpPut("facilities/{id:long}/status")]
    [ProducesResponseType(typeof(ApiResponse<AdminFacilityDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateFacilityStatus(
        [FromRoute] long id,
        [FromBody] AdminUpdateFacilityStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var adminUserId))
            return Unauthorized(ApiResponse.Fail("Cannot identify administrator identity from token."));

        var result = await _adminService.UpdateFacilityStatusAsync(id, request, adminUserId, cancellationToken);
        return Ok(ApiResponse<AdminFacilityDetailDto>.Ok(result, "Facility status updated successfully."));
    }

    // ==========================================
    // 5. Quản Lý Quy Tắc Phí & Biểu Phí Hệ Thống
    // ==========================================

    /// <summary>
    /// Tra cứu danh mục quy tắc phí hệ thống (phí vệ sinh, phạt quá hạn, phí hư hỏng).
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
    /// Thiết lập / cập nhật quy tắc tính phí toàn hệ thống hoặc theo cơ sở.
    /// </summary>
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    [HttpPut("pricing/fees")]
    [ProducesResponseType(typeof(ApiResponse<OperationsFeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpsertFee(
        [FromBody] OperationsUpsertFeeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var adminUserId))
            return Unauthorized(ApiResponse.Fail("Cannot identify administrator identity from token."));

        var result = await _operationsService.UpsertFeeAsync(request, adminUserId, cancellationToken);
        return Ok(ApiResponse<OperationsFeeDto>.Ok(result, "Fee rule saved successfully."));
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
