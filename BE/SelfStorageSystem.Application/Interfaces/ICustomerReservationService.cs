using SelfStorageSystem.Contracts.Customer.Reservations;

namespace SelfStorageSystem.Application.Interfaces;

public interface ICustomerReservationService
{
    /// <summary>
    /// Creates a 15-minute temporary reservation hold with the initial invoice (BR-RSV-01, BR-RSV-02, BR-FIN-01, BR-OPS-02).
    /// </summary>
    Task<CreateReservationResponse> CreateReservationAsync(
        long customerId,
        CreateReservationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a list of reservations for the current authenticated customer.
    /// </summary>
    Task<List<ReservationSummaryDto>> GetMyReservationsAsync(
        long customerId,
        string? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed reservation information, remaining hold time, and check-in QR code if confirmed.
    /// </summary>
    Task<ReservationDetailDto> GetReservationDetailAsync(
        long customerId,
        long reservationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Allows customer to cancel a pending reservation and immediately release the unit back to available status (BR-RSV-01).
    /// </summary>
    Task<bool> CancelReservationAsync(
        long customerId,
        long reservationId,
        string? reason = null,
        CancellationToken cancellationToken = default);
}
