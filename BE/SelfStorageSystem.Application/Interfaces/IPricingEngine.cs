using SelfStorageSystem.Contracts.Customer.Facilities;

namespace SelfStorageSystem.Application.Interfaces;

public interface IPricingEngine
{
    Task<PricingQuoteDto> CalculateQuoteAsync(
        PricingQuoteRequest request,
        long? customerId = null,
        bool lockPromotionRow = false,
        bool throwConflictOnPromotionLimit = false,
        CancellationToken cancellationToken = default);
}
