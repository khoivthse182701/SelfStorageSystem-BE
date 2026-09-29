using SelfStorageSystem.Domain.Common;

namespace SelfStorageSystem.Domain.Errors;

public static class CatalogErrors
{
    public static readonly Error InvalidPricingRequest = new("Catalog.InvalidPricingRequest", "Duration must be between 1 and 12 months and facility and unit type must be valid.", ErrorType.Validation);
    public static readonly Error InvalidVoucher = new("Catalog.InvalidVoucher", "The voucher is invalid, expired, or its conditions are not met.", ErrorType.Validation);
}
