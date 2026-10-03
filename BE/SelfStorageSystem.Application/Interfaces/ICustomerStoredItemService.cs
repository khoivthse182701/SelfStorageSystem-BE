using SelfStorageSystem.Contracts.Customer.Rentals;

namespace SelfStorageSystem.Application.Interfaces;

public interface ICustomerStoredItemService
{
    Task<RentalStoredItemsResponseDto> DeclareStoredItemsAsync(
        long customerId,
        long agreementId,
        DeclareStoredItemsRequest request,
        CancellationToken cancellationToken = default);

    Task<RentalStoredItemsResponseDto> GetDeclaredStoredItemsAsync(
        long customerId,
        long agreementId,
        CancellationToken cancellationToken = default);
}
