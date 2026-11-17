using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Payments;

namespace SelfStorageSystem.Application.Interfaces;

public interface ICustomerPaymentService
{
    /// <summary>
    /// Creates checkout payment information and VietQR payload via SePay for a reservation.
    /// </summary>
    Task<CheckoutResponse> CreateCheckoutAsync(
        long customerId,
        CreateCheckoutRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns live status for a payment and its linked invoice/reservation (used for VietQR polling).
    /// </summary>
    Task<PaymentStatusDto> GetPaymentStatusAsync(
        long customerId,
        long paymentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes bank transfer balance update webhook received from SePay.
    /// </summary>
    Task<bool> ProcessSePayWebhookAsync(
        SePayWebhookPayload payload,
        CancellationToken cancellationToken = default);

    Task<SePayWebhookResultDto> ProcessSePayWebhookWithResultAsync(
        SePayWebhookPayload payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves payment history and digital receipts for the authenticated customer.
    /// </summary>
    Task<List<PaymentHistoryDto>> GetMyPaymentHistoryAsync(
        long customerId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<PaymentHistoryDto>> GetMyPaymentHistoryPagedAsync(
        long customerId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<List<UnpaidInvoiceDto>> GetMyUnpaidInvoicesAsync(
        long customerId,
        CancellationToken cancellationToken = default);
}
