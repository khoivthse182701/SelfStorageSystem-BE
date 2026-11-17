namespace SelfStorageSystem.Domain.Constants;

public static class PromotionDiscountTypeConstants
{
    public const string Percentage = "percentage";
    public const string Fixed = "fixed";
    public const string FreeDays = "free_days";
}

public static class PromotionRedemptionStatusConstants
{
    public const string Reserved = "reserved";
    public const string Applied = "applied";
    public const string Released = "released";
}

public static class PromotionRuleTypeConstants
{
    public const string Month = "month";
    public const string MinimumMonths = "minimum_months";
    public const string MinimumAmount = "minimum_amount";
    public const string Facility = "facility";
    public const string UnitType = "unit_type";
    public const string NewCustomer = "new_customer";
}

public static class PromotionOperatorConstants
{
    public const string Eq = "eq";
    public const string Gte = "gte";
    public const string Lte = "lte";
    public const string In = "in";
}
