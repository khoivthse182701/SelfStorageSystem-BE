namespace SelfStorageSystem.Domain.Constants;

public static class OperationsConstants
{
    public const int DefaultPageNumber = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxTextLength = 255;
    public const decimal MaxMoney = 999999999999.99m;
    public const string MaxMoneyText = "999999999999.99";
    public const decimal MaxRatePercent = 999.9999m;
    public const decimal DefaultLateFeeRatePercent = 150m;
    public const string FlatCalculationMethod = "flat";
    public const string PercentageCalculationMethod = "percentage";
    public const string PerDayCalculationMethod = "per_day";
    public const string LateFeeType = "late_fee";
    public const string MinMonthsRuleType = "minimum_months";
    public const string EqualOperator = "eq";
    public const string GreaterThanOrEqualOperator = "gte";
    public const string DefaultConditions = "{}";
    public const string AuditSchema = "core";
}
