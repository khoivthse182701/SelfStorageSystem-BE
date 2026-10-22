namespace SelfStorageSystem.Domain.Constants;

public static class OperationsConstants
{
    public const int DefaultPageNumber = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxTextLength = 255;
    public const long MinEntityId = 1;

    public const decimal MinMoney = 0m;
    public const string MinMoneyText = "0";
    public const decimal MinPositiveMoney = 0.01m;
    public const string MinPositiveMoneyText = "0.01";
    public const decimal MaxMoney = 999999999999.99m;
    public const string MaxMoneyText = "999999999999.99";

    public const decimal MinRatePercent = 0.0001m;
    public const string MinRatePercentText = "0.0001";
    public const decimal MaxRatePercent = 999.9999m;
    public const string MaxRatePercentText = "999.9999";
    public const decimal DefaultLateFeeRatePercent = 150m;

    public const int MinGraceDays = 0;
    public const int MinUsageLimit = 1;
    public const int MinPerCustomerLimit = 1;
    public const int MoneyDecimalPlaces = 2;
    public const int RatePercentDecimalPlaces = 4;
    public const decimal MaxPercentageDiscount = 100m;
    public const int MaxPromotionRules = 3;
    public const int MaxRuleValueMonths = 12;

    public const string FlatCalculationMethod = "flat";
    public const string PercentageCalculationMethod = "percentage";
    public const string PerDayCalculationMethod = "per_day";
    public const string LateFeeType = "late_fee";
    public const string MinMonthsRuleType = "minimum_months";
    public const string EqualOperator = "eq";
    public const string GreaterThanOrEqualOperator = "gte";
    public const string DefaultConditions = "{}";
    public const string AuditSchema = "core";

    public const string FeeTypeRegex = "^(late_fee|damage_fee|lost_key_fee|maintenance_fee|other)$";
    public const string CalculationMethodRegex = "^(flat|percentage|per_day)$";
    public const string DiscountTypeRegex = "^(percentage|fixed)$";
    public const string PromotionRuleTypeRegex = "^(facility|unit_type|minimum_months)$";
    public const string PromotionOperatorRegex = "^(eq|gte)$";
}
