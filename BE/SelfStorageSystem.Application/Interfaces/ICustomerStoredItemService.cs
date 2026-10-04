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

    Task<StoredItemDto> UpdateStoredItemAsync(
        long customerId,
        long agreementId,
        long itemId,
        UpdateStoredItemRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteStoredItemAsync(
        long customerId,
        long agreementId,
        long itemId,
        CancellationToken cancellationToken = default);
}
