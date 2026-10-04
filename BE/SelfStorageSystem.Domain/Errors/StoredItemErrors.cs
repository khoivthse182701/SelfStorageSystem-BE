using SelfStorageSystem.Domain.Common;

namespace SelfStorageSystem.Domain.Errors;

public static class StoredItemErrors
{
    public static readonly Error AgreementNotFound = Error.NotFound(
        "StoredItem.AgreementNotFound",
        "Rental agreement was not found or does not belong to the authenticated customer.");

    public static readonly Error EmptyItemsList = Error.Validation(
        "StoredItem.EmptyItemsList",
        "Items list cannot be empty.");

    public static readonly Error InvalidQuantity = Error.Validation(
        "StoredItem.InvalidQuantity",
        "Item quantity must be greater than 0.");

    public static readonly Error InvalidCategory = Error.Validation(
        "StoredItem.InvalidCategory",
        "Invalid item category specified.");

    public static readonly Error InvalidRiskClassification = Error.Validation(
        "StoredItem.InvalidRiskClassification",
        "Invalid risk classification specified.");

    public static readonly Error EmptyItemName = Error.Validation(
        "StoredItem.EmptyItemName",
        "Item name is required and cannot be empty.");

    public static readonly Error ProhibitedItem = Error.Validation(
        "StoredItem.ProhibitedItem",
        "Hazardous, flammable, explosive, or illegal items are strictly prohibited from storage.");

    public static readonly Error AgreementNotActive = Error.Validation(
        "StoredItem.AgreementNotActive",
        "Stored items can only be declared for active rental agreements.");

    public static readonly Error ItemNotFound = Error.NotFound(
        "StoredItem.NotFound",
        "Stored item was not found or does not belong to this agreement.");

    public static readonly Error InvalidEstimatedValue = Error.Validation(
        "StoredItem.InvalidEstimatedValue",
        "Estimated value cannot be negative.");
}
