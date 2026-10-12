using SelfStorageSystem.Contracts.Admin;
using SelfStorageSystem.Contracts.Common;

namespace SelfStorageSystem.Application.Interfaces;

public interface IAdminManagementService
{
    // ==========================================
    // 1. Quản lý Người Dùng & Phân Quyền (RBAC)
    // ==========================================

    /// <summary>
    /// Danh sách người dùng, tìm kiếm & bộ lọc nâng cao (role, status, facilityId).
    /// </summary>
    Task<PagedResult<AdminUserSummaryDto>> GetUsersAsync(
        AdminUserQueryParameters query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chi tiết hồ sơ nhân viên / khách hàng & lịch sử đăng nhập.
    /// </summary>
    Task<AdminUserDetailDto> GetUserDetailAsync(
        long id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo tài khoản nội bộ mới cho Staff / Manager / Admin.
    /// </summary>
    Task<AdminUserDetailDto> CreateInternalUserAsync(
        AdminCreateUserRequest request,
        long adminUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Khóa hoặc kích hoạt tài khoản người dùng.
    /// </summary>
    Task<AdminUserSummaryDto> UpdateUserStatusAsync(
        long id,
        AdminUpdateUserStatusRequest request,
        long adminUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gán & cập nhật vai trò người dùng ([core].[user_roles]).
    /// </summary>
    Task<IReadOnlyList<string>> UpdateUserRolesAsync(
        long id,
        AdminUpdateUserRolesRequest request,
        long adminUserId,
        CancellationToken cancellationToken = default);

    // ==========================================
    // 2. Phân Bổ Nhân Sự Điểm Kho (Data Scope)
    // ==========================================

    /// <summary>
    /// Gán nhân sự vào điểm kho phụ trách ([core].[staff_facility_assignments]).
    /// </summary>
    Task<AdminStaffFacilityAssignmentDto> AssignStaffToFacilityAsync(
        AdminCreateStaffAssignmentRequest request,
        long adminUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thu hồi điều chuyển nhân sự khỏi điểm kho.
    /// </summary>
    Task RevokeStaffAssignmentAsync(
        long assignmentId,
        long adminUserId,
        CancellationToken cancellationToken = default);

    // ==========================================
    // 3. Bảo Mật & Kiểm Toán & Dashboard
    // ==========================================

    /// <summary>
    /// Tra cứu nhật ký kiểm toán hệ thống ([core].[audit_logs]).
    /// </summary>
    Task<PagedResult<AdminAuditLogDto>> GetAuditLogsAsync(
        AdminAuditLogQueryParameters query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xem nhật ký đăng nhập & cảnh báo các hành vi đăng nhập bất thường.
    /// </summary>
    Task<AdminLoginHistoryReportDto> GetLoginHistoriesAsync(
        AdminLoginHistoryQueryParameters query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thống kê KPI chuỗi kho (doanh thu tổng, tỷ lệ lấp đầy toàn hệ thống).
    /// </summary>
    Task<AdminDashboardOverviewDto> GetDashboardOverviewAsync(
        CancellationToken cancellationToken = default);
}
