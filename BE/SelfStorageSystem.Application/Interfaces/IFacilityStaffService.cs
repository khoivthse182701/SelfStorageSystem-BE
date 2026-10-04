using SelfStorageSystem.Contracts.Staff;

namespace SelfStorageSystem.Application.Interfaces;

public interface IFacilityStaffService
{
    // 1. Daily Staff Tasks
    Task<IReadOnlyList<StaffTaskDto>> GetStaffTasksAsync(long employeeUserId, long? facilityId = null, CancellationToken cancellationToken = default);
    Task<StaffTaskDto> UpdateStaffTaskStatusAsync(long employeeUserId, long taskId, UpdateStaffTaskStatusRequest request, CancellationToken cancellationToken = default);

    // 2. Check-in & Handover
    Task<IReadOnlyList<StaffReservationLookupDto>> LookupReservationsAsync(string? query, long? facilityId = null, CancellationToken cancellationToken = default);
    Task AssignUnitToReservationAsync(long employeeUserId, long reservationId, AssignUnitToReservationRequest request, CancellationToken cancellationToken = default);
    Task<StaffHandoverResultDto> CreateHandoverAsync(long employeeUserId, CreateStaffHandoverRequest request, CancellationToken cancellationToken = default);

    // 3. Move-out & Inspection
    Task<IReadOnlyList<StaffMoveOutSummaryDto>> GetMoveOutsAsync(long? facilityId = null, DateOnly? date = null, CancellationToken cancellationToken = default);
    Task<InspectMoveOutResultDto> InspectMoveOutAsync(long employeeUserId, long moveOutId, InspectMoveOutRequest request, CancellationToken cancellationToken = default);

    // 4. Support Tickets
    Task<IReadOnlyList<StaffSupportTicketDto>> GetFacilityTicketsAsync(long? facilityId = null, string? status = null, CancellationToken cancellationToken = default);
    Task AssignTicketToMeAsync(long employeeUserId, long ticketId, CancellationToken cancellationToken = default);
    Task<StaffTicketMessageDto> AddStaffTicketMessageAsync(long employeeUserId, long ticketId, AddStaffTicketMessageRequest request, CancellationToken cancellationToken = default);
    Task<TicketChargeProposalDto> ProposeTicketChargeAsync(long employeeUserId, long ticketId, ProposeTicketChargeRequest request, CancellationToken cancellationToken = default);
    Task ResolveTicketAsync(long employeeUserId, long ticketId, ResolveTicketRequest request, CancellationToken cancellationToken = default);

    // 5. Unit Management & Maintenance
    Task<IReadOnlyList<StaffFacilityUnitDto>> GetFacilityUnitsAsync(long facilityId, CancellationToken cancellationToken = default);
    Task UpdateUnitStatusAsync(long employeeUserId, long unitId, UpdateUnitStatusRequest request, CancellationToken cancellationToken = default);
    Task<MaintenanceWorkOrderDto> CreateMaintenanceOrderAsync(long employeeUserId, long unitId, CreateMaintenanceWorkOrderRequest request, CancellationToken cancellationToken = default);

    // 6. Overdue Enforcement
    Task<IReadOnlyList<OverdueAgreementDto>> GetOverdueAgreementsAsync(long? facilityId = null, CancellationToken cancellationToken = default);
    Task LockAgreementAccessAsync(long employeeUserId, long agreementId, LockAccessRequest request, CancellationToken cancellationToken = default);
}
