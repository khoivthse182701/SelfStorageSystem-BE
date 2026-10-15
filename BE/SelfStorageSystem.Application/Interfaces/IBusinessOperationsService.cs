using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Operations;

namespace SelfStorageSystem.Application.Interfaces;

public interface IBusinessOperationsService
{
    Task<PagedResult<OperationsRateDto>> GetRatesAsync(OperationsRateQueryParameters query, CancellationToken cancellationToken = default);
    Task<OperationsRateDto> UpsertRateAsync(OperationsUpsertRateRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<PagedResult<OperationsFeeDto>> GetFeesAsync(OperationsFeeQueryParameters query, CancellationToken cancellationToken = default);
    Task<OperationsFeeDto> UpsertFeeAsync(OperationsUpsertFeeRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<PagedResult<OperationsPromotionDto>> GetPromotionsAsync(OperationsPromotionQueryParameters query, CancellationToken cancellationToken = default);
    Task<OperationsPromotionDto> CreatePromotionAsync(OperationsCreatePromotionRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<OperationsPromotionDto> UpdatePromotionStatusAsync(long id, OperationsUpdatePromotionStatusRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<OperationsFacilityDto> CreateFacilityAsync(OperationsCreateFacilityRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<OperationsFacilityDto> UpdateFacilityAsync(OperationsUpdateFacilityRequest request, long actorUserId, CancellationToken cancellationToken = default);
}
