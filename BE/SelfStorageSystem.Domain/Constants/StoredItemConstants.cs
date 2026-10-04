namespace SelfStorageSystem.Domain.Constants;

public static class StoredItemCategoryConstants
{
    public const string Furniture = "furniture";
    public const string Electronics = "electronics";
    public const string Documents = "documents";
    public const string Clothing = "clothing";
    public const string Household = "household";
    public const string Personal = "personal";
    public const string Commercial = "commercial";
    public const string Other = "other";

    public static readonly string[] All = { Furniture, Electronics, Documents, Clothing, Household, Personal, Commercial, Other };
}

public static class ItemRiskClassificationConstants
{
    public const string Standard = "standard";
    public const string Fragile = "fragile";
    public const string HighValue = "high_value";
    public const string SpecialCare = "special_care";

    public static readonly string[] All = { Standard, Fragile, HighValue, SpecialCare };
}

public static class StoredItemRulesConstants
{
    public static readonly string[] ProhibitedKeywords =
    {
        "explosive", "flammable", "weapon", "gun", "drug", "illegal", "chemical", "poison",
        "chất nổ", "chất dễ cháy", "vũ khí", "ma túy", "hàng cấm", "hóa chất độc hại", "súng"
    };
}
