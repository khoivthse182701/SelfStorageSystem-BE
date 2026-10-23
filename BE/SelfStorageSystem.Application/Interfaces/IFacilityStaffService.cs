using SelfStorageSystem.Contracts.Staff;

namespace SelfStorageSystem.Application.Interfaces;

public interface IFacilityStaffService
{
    // ==========================================
    // 0. STAFF ASSIGNED FACILITY INFO
    // ==========================================
    Task<StaffFacilityInfoDto> GetMyFacilityAsync(long employeeUserId, CancellationToken cancellationToken = default);

    // ==========================================
    // 1. UNIT AVAILABILITY & DETAILS (Nhóm 1)
    // ==========================================
    Task<IReadOnlyList<StaffUnitGridDto>> GetUnitsAsync(
        long employeeUserId,
        long? facilityId = null,
        string? status = null,
        long? unitTypeId = null,
        string? sizeCategory = null,
        CancellationToken cancellationToken = default);

    Task<StaffUnitDetailDto> GetUnitDetailAsync(
        long employeeUserId,
        long unitId,
        CancellationToken cancellationToken = default);

    // ==========================================
    // 2. WALK-IN BOOKING AT COUNTER (Nhóm 2)
    // ==========================================
    Task<StaffWalkInBookingResponse> CreateWalkInBookingAsync(
        long employeeUserId,
        StaffWalkInBookingRequest request,
        CancellationToken cancellationToken = default);

    // ==========================================
    // 3. QR VERIFY & CONFIRM ACTION (Nhóm 3)
    // ==========================================
    Task<StaffQrVerifyResponse> VerifyQrAsync(
        long employeeUserId,
        StaffQrVerifyRequest request,
        CancellationToken cancellationToken = default);

    Task<StaffConfirmQrActionResultDto> ConfirmQrActionAsync(
        long employeeUserId,
        StaffConfirmQrActionRequest request,
        CancellationToken cancellationToken = default);

    // ==========================================
    // 4. LOGISTICS & MAINTENANCE TICKETS (Nhóm 4)
    // ==========================================
    Task<IReadOnlyList<StaffLogisticsTicketDto>> GetLogisticsTicketsAsync(
        long employeeUserId,
        long? facilityId = null,
        string? category = null,
        string? status = null,
        bool? onlyAssignedToMe = null,
        CancellationToken cancellationToken = default);

    Task AssignTicketToStaffAsync(
        long employeeUserId,
        long ticketId,
        CancellationToken cancellationToken = default);

    Task<TicketChargeProposalDto> ProposeTicketChargeAsync(
        long employeeUserId,
        long ticketId,
        StaffProposeChargeRequest request,
        CancellationToken cancellationToken = default);

    Task<StaffCompleteTicketResultDto> CompleteTicketAsync(
        long employeeUserId,
        long ticketId,
        StaffCompleteTicketRequest request,
        CancellationToken cancellationToken = default);

    // ==========================================
    // 5. SHIFTS & WORK SCHEDULE
    // ==========================================
    Task<IReadOnlyList<StaffShiftScheduleDto>> GetStaffShiftsAsync(
        long employeeUserId,
        long? facilityId = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default);

    Task<StaffShiftScheduleDto> CheckInShiftAsync(long employeeUserId, long shiftId, CancellationToken cancellationToken = default);
    Task<StaffShiftScheduleDto> CheckOutShiftAsync(long employeeUserId, long shiftId, CancellationToken cancellationToken = default);

    // ==========================================
    // LEGACY METHODS (Kept for compatibility)
    // ==========================================
    Task<IReadOnlyList<StaffTaskDto>> GetStaffTasksAsync(long employeeUserId, long? facilityId = null, CancellationToken cancellationToken = default);
    Task<StaffTaskDto> UpdateStaffTaskStatusAsync(long employeeUserId, long taskId, UpdateStaffTaskStatusRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StaffReservationLookupDto>> LookupReservationsAsync(string? query, long? facilityId = null, CancellationToken cancellationToken = default);
    Task AssignUnitToReservationAsync(long employeeUserId, long reservationId, AssignUnitToReservationRequest request, CancellationToken cancellationToken = default);
    Task<StaffHandoverResultDto> CreateHandoverAsync(long employeeUserId, CreateStaffHandoverRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StaffMoveOutSummaryDto>> GetMoveOutsAsync(long? facilityId = null, DateOnly? date = null, CancellationToken cancellationToken = default);
    Task<InspectMoveOutResultDto> InspectMoveOutAsync(long employeeUserId, long moveOutId, InspectMoveOutRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StaffSupportTicketDto>> GetFacilityTicketsAsync(long? facilityId = null, string? status = null, CancellationToken cancellationToken = default);
    Task AssignTicketToMeAsync(long employeeUserId, long ticketId, CancellationToken cancellationToken = default);
    Task<StaffTicketMessageDto> AddStaffTicketMessageAsync(long employeeUserId, long ticketId, AddStaffTicketMessageRequest request, CancellationToken cancellationToken = default);
    Task<TicketChargeProposalDto> ProposeTicketChargeAsync(long employeeUserId, long ticketId, ProposeTicketChargeRequest request, CancellationToken cancellationToken = default);
    Task ResolveTicketAsync(long employeeUserId, long ticketId, ResolveTicketRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StaffFacilityUnitDto>> GetFacilityUnitsAsync(long facilityId, CancellationToken cancellationToken = default);
    Task UpdateUnitStatusAsync(long employeeUserId, long unitId, UpdateUnitStatusRequest request, CancellationToken cancellationToken = default);
    Task<MaintenanceWorkOrderDto> CreateMaintenanceOrderAsync(long employeeUserId, long unitId, CreateMaintenanceWorkOrderRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OverdueAgreementDto>> GetOverdueAgreementsAsync(long? facilityId = null, CancellationToken cancellationToken = default);
    Task LockAgreementAccessAsync(long employeeUserId, long agreementId, LockAccessRequest request, CancellationToken cancellationToken = default);
}
