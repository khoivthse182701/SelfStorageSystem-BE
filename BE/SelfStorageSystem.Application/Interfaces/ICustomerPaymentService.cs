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
    /// Processes bank transfer balance update webhook received from SePay.
    /// </summary>
    Task<bool> ProcessSePayWebhookAsync(
        SePayWebhookPayload payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves payment history and digital receipts for the authenticated customer.
    /// </summary>
    Task<List<PaymentHistoryDto>> GetMyPaymentHistoryAsync(
        long customerId,
        CancellationToken cancellationToken = default);
}
