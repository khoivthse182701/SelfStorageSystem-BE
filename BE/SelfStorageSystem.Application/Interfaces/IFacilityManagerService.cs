using SelfStorageSystem.Contracts.Staff;
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

    Task DeleteUnitAsync(
        long unitId,
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

    Task<ManagerTransferRequestDto> DecideTransferRequestAsync(
        long transferRequestId,
        ManagerDecideTransferRequest request,
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

    Task<ManagerReassignUnitResponseDto> ReassignAgreementUnitAsync(
        long agreementId,
        ManagerReassignUnitRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<ManagerEmergencyCancelRefundResponseDto> EmergencyCancelAndRefundAgreementAsync(
        long agreementId,
        ManagerEmergencyCancelRefundRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);



    // ==========================================
    // 6. Quản Lý Nhân Sự & Khách Hàng Cơ Sở (Facility Scope)
    // ==========================================

    Task<PagedResult<ManagerStaffItemDto>> GetFacilityStaffsAsync(
        long? facilityId,
        string? search,
        int pageNumber,
        int pageSize,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<ManagerStaffItemDto> CreateFacilityStaffAsync(
        ManagerCreateStaffRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<ManagerStaffItemDto> UpdateFacilityStaffAsync(
        long staffUserId,
        ManagerUpdateStaffRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<ManagerStaffItemDto> UpdateFacilityStaffStatusAsync(
        long staffUserId,
        ManagerUpdateStaffStatusRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ManagerCustomerItemDto>> GetFacilityCustomersAsync(
        long? facilityId,
        string? search,
        int pageNumber,
        int pageSize,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    // ==========================================
    // 7. Quản Lý Sự Cố Ticket & Phân Công Kiểm Tra Hết Hạn
    // ==========================================

    Task<PagedResult<ManagerTicketDetailDto>> GetFacilityTicketsAsync(
        long? facilityId,
        string? status,
        string? category,
        int pageNumber,
        int pageSize,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<ManagerTicketDetailDto> CreateTicketAsync(
        ManagerCreateTicketRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<ManagerTicketDetailDto> AssignTicketStaffAsync(
        long ticketId,
        ManagerAssignTicketStaffRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StaffTicketMessageDto>> GetTicketMessagesAsync(
        long ticketId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ManagerChargeProposalDto>> GetTicketChargeProposalsAsync(
        long ticketId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<ManagerTicketDetailDto> CloseTicketAsync(
        long ticketId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ManagerAgreementDetailDto>> GetExpiringOrMoveOutAgreementsAsync(
        long facilityId,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<ManagerInspectionDetailDto> AssignMoveOutInspectionAsync(
        ManagerAssignInspectionRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    // ==========================================
    // 8. Quản Lý & Chuyển Khoản Hoàn Cọc (Manager Bank Transfer Payout)
    // ==========================================

    Task<PagedResult<ManagerRefundItemDto>> GetFacilityRefundsAsync(
        long? facilityId,
        string? status,
        int pageNumber,
        int pageSize,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<ManagerProcessRefundPayoutResultDto> ProcessRefundPayoutAsync(
        long refundId,
        ManagerProcessRefundPayoutRequest request,
        long currentUserId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);
}
