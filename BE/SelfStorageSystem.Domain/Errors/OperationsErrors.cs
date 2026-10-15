using SelfStorageSystem.Domain.Common;

namespace SelfStorageSystem.Domain.Errors;

public static class OperationsErrors
{
    public static readonly Error FacilityNotFound = Error.NotFound("Operations.FacilityNotFound", "The facility was not found.");
    public static readonly Error UnitTypeNotFound = Error.NotFound("Operations.UnitTypeNotFound", "The storage unit type was not found.");
    public static readonly Error RateNotFound = Error.NotFound("Operations.RateNotFound", "The facility rate was not found.");
    public static readonly Error FeeNotFound = Error.NotFound("Operations.FeeNotFound", "The fee rule was not found.");
    public static readonly Error PromotionNotFound = Error.NotFound("Operations.PromotionNotFound", "The promotion was not found.");
    public static readonly Error FacilityCodeAlreadyExists = Error.Conflict("Operations.FacilityCodeAlreadyExists", "A facility with this code already exists.");
    public static readonly Error PromotionCodeAlreadyExists = Error.Conflict("Operations.PromotionCodeAlreadyExists", "A promotion with this code already exists.");
    public static readonly Error RatePeriodOverlaps = Error.Conflict("Operations.RatePeriodOverlaps", "A rate already exists for this facility, unit type and overlapping effective period.");
    public static readonly Error FeePeriodOverlaps = Error.Conflict("Operations.FeePeriodOverlaps", "A fee rule already exists for this scope, code and overlapping effective period.");
    public static readonly Error RateOverlap = Error.Conflict("Operations.RateOverlap", "A rate already exists for this facility, unit type and overlapping effective period.");
    public static readonly Error FeeOverlap = Error.Conflict("Operations.FeeOverlap", "A fee rule already exists for this scope, code and overlapping effective period.");
    public static readonly Error RateInUse = Error.Conflict("Operations.RateInUse", "A rate referenced by a reservation cannot be changed; create a new rate period instead.");
    public static readonly Error PriceOutsideRange = Error.Validation("Operations.PriceOutsideRange", "The monthly rate must fall within an effective price range for this unit type.");
    public static readonly Error DuplicatePromotionCode = Error.Conflict("Operations.DuplicatePromotionCode", "A promotion with this code already exists.");
    public static readonly Error DuplicateFacilityCode = Error.Conflict("Operations.DuplicateFacilityCode", "A facility with this code already exists.");
    public static readonly Error InvalidTimezone = Error.Validation("Operations.InvalidTimezone", "Timezone must be a valid time zone identifier.");
    public static readonly Error Conflict = Error.Conflict("Operations.Conflict", "The requested update conflicts with existing operations data.");
    public static readonly Error InvalidRequest = Error.Validation("Operations.InvalidRequest", "The operations request is invalid.");
    public static readonly Error InvalidDates = Error.Validation("Operations.InvalidDates", "The end of the effective period must be later than its start.");
    public static readonly Error InvalidMoney = Error.Validation("Operations.InvalidMoney", "Monetary amounts must be non-negative, within numeric(14,2), and use at most two decimal places.");
    public static readonly Error InvalidFeeRule = Error.Validation("Operations.InvalidFeeRule", "The fee type, calculation method or applicable amount is invalid.");
    public static readonly Error InvalidConditions = Error.Validation("Operations.InvalidConditions", "Fee conditions must be a valid JSON object.");
    public static readonly Error InvalidLateFee = Error.Validation("Operations.InvalidLateFee", "Late fees must be 150 percent of daily rent, from the first overdue day.");
    public static readonly Error InvalidPromotion = Error.Validation("Operations.InvalidPromotion", "The promotion discount, limits or effective period is invalid.");
    public static readonly Error InvalidPromotionRule = Error.Validation("Operations.InvalidPromotionRule", "Promotion rules support one facility eq, unit_type eq and minimum_months gte rule each, with positive integer values.");
    public static readonly Error InvalidFacility = Error.Validation("Operations.InvalidFacility", "The facility details, coordinates, hours, status or time zone are invalid.");
    public static readonly Error InvalidQuery = Error.Validation("Operations.InvalidQuery", "The query parameters are invalid.");
}
