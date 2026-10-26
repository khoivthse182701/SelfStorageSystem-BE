using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Manager;

namespace SelfStorageSystem.Application.Interfaces;

public interface IFacilityManagerService
{
    // ==========================================
    // 1. Quản Lý Hạ Tầng Ô Kho & Sơ Đồ
    // ==========================================

    Task<PagedResult<ManagerStorageUnitDto>> GetUnitsAsync(
        long facilityId,
        string? status,
        long? unitTypeId,
        string? search,
        int pageNumber,
        int pageSize,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<ManagerStorageUnitDto> CreateUnitAsync(
        long facilityId,
        ManagerCreateStorageUnitRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<ManagerStorageUnitDto> UpdateUnitAsync(
        long unitId,
        ManagerUpdateStorageUnitRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<ManagerMapLayoutDto> GetMapLayoutAsync(
        long facilityId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<int> UpdateMapLayoutAsync(
        long facilityId,
        ManagerUpdateMapLayoutRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ManagerTransferRequestDto>> GetTransferRequestsAsync(
        long facilityId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    // ==========================================
    // 2. Phân Bổ Nhân Sự & Giao Việc Nhân Viên
    // ==========================================

    Task ValidateFacilityAccessAsync(
        long facilityId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ManagerStaffShiftDto>> GetShiftsAsync(
        long facilityId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<ManagerStaffShiftDto> CreateShiftAsync(
        long facilityId,
        ManagerCreateShiftRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ManagerShiftRosterItemDto>> GetShiftRosterAsync(
        long facilityId,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        long? employeeId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<ManagerShiftAssignmentDto> AssignShiftAsync(
        long facilityId,
        ManagerCreateShiftAssignmentRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task DeleteShiftAssignmentAsync(
        long facilityId,
        long shiftId,
        long employeeId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<ManagerBatchSaveShiftAssignmentsResultDto> BatchSaveShiftAssignmentsAsync(
        long facilityId,
        ManagerBatchSaveShiftAssignmentsRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ManagerFacilityStaffDto>> GetFacilityStaffAsync(
        long facilityId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<ManagerStaffTaskDto> CreateStaffTaskAsync(
        long facilityId,
        ManagerCreateStaffTaskRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    // ==========================================
    // 3. Phê Duyệt Tài Chính
    // ==========================================

    Task<IReadOnlyList<ManagerChargeProposalDto>> GetChargeProposalsAsync(
        long facilityId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<ManagerChargeProposalDecisionDto> DecideChargeProposalAsync(
        long id,
        ManagerDecideChargeProposalRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ManagerRefundApprovalDto>> GetRefundApprovalsAsync(
        long facilityId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<ManagerRefundDecisionDto> DecideRefundApprovalAsync(
        long id,
        ManagerDecideRefundApprovalRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    // ==========================================
    // 4. Giám Sát Hợp Đồng & Xử Lý Quá Hạn Kéo Dài
    // ==========================================

    Task<PagedResult<ManagerAgreementSummaryDto>> GetAgreementsAsync(
        long facilityId,
        string? status,
        string? search,
        int pageNumber,
        int pageSize,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<ManagerAgreementDetailDto> GetAgreementDetailAsync(
        long agreementId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ManagerDelinquencyCaseDto>> GetDelinquentCasesAsync(
        long facilityId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<ManagerDelinquencyActionDto> AddDelinquencyActionAsync(
        long caseId,
        ManagerAddDelinquencyActionRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    // ==========================================
    // 5. Dashboard Cơ Sở
    // ==========================================

    Task<ManagerFacilityDashboardDto> GetFacilityDashboardAsync(
        long facilityId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<List<ManagerFacilityScopeDto>> GetMyFacilitiesAsync(
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);
}
