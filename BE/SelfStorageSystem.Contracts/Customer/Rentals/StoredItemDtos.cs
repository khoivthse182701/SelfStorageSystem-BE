namespace SelfStorageSystem.Contracts.Customer.Rentals;

public record CreateStoredItemDto(
    string ItemName,
    string Category,
    string? Description,
    int Quantity = 1,
    decimal? EstimatedValue = null,
    string? RiskClassification = null,
    string? PhotoUrl = null
);

public record DeclareStoredItemsRequest(
    List<CreateStoredItemDto> Items
);

public record StoredItemDto(
    long Id,
    long AgreementId,
    string ItemName,
    string Category,
    string? Description,
    int Quantity,
    decimal? EstimatedValue,
    string RiskClassification,
    string? PhotoUrl,
    DateTimeOffset DeclaredAt,
    DateTimeOffset UpdatedAt
);

public record RentalStoredItemsResponseDto(
    long AgreementId,
    int TotalItemsCount,
    decimal TotalEstimatedValue,
    IReadOnlyList<StoredItemDto> Items
);
