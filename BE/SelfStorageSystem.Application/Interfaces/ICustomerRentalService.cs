using SelfStorageSystem.Contracts.Customer.Rentals;

namespace SelfStorageSystem.Application.Interfaces;

public interface ICustomerRentalService
{
    Task<IReadOnlyList<RentalSummaryDto>> GetMyRentalsAsync(long customerId, CancellationToken cancellationToken = default);
    Task<AccessCredentialDto> GetAccessCredentialsAsync(long customerId, long agreementId, CancellationToken cancellationToken = default);
    Task<ChangePinResponseDto> ChangePinAsync(long customerId, long agreementId, ChangePinRequest request, CancellationToken cancellationToken = default);
    Task<UnlockStorageUnitResponseDto> UnlockUnitAsync(long customerId, long agreementId, UnlockStorageUnitRequest request, CancellationToken cancellationToken = default);
    Task<HandoverRecordDto> GetHandoverRecordAsync(long customerId, long agreementId, CancellationToken cancellationToken = default);

    Task<MoveOutResponseDto> RequestMoveOutAsync(long customerId, long agreementId, RequestMoveOutRequest request, CancellationToken cancellationToken = default);
    Task<RefundPreviewDto> GetRefundPreviewAsync(long customerId, long agreementId, CancellationToken cancellationToken = default);
    Task<RenewAgreementResponseDto> RenewAgreementAsync(long customerId, long agreementId, RenewAgreementRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuthorizedMemberDto>> GetAuthorizedMembersAsync(long customerId, long agreementId, CancellationToken cancellationToken = default);
    Task<AuthorizedMemberDto> AddAuthorizedMemberAsync(long customerId, long agreementId, CreateAuthorizedMemberRequest request, CancellationToken cancellationToken = default);
    Task RevokeAuthorizedMemberAsync(long customerId, long agreementId, long memberId, CancellationToken cancellationToken = default);
    Task<UnitTransferRequestDto> RequestUnitTransferAsync(long customerId, long agreementId, CreateTransferRequest request, CancellationToken cancellationToken = default);
}
