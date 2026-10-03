using SelfStorageSystem.Domain.Common;

namespace SelfStorageSystem.Domain.Errors;

public static class TicketErrors
{
    public static readonly Error TicketNotFound =
        Error.NotFound("Ticket.NotFound", "Support ticket was not found or does not belong to you.");

    public static readonly Error FacilityNotFound =
        Error.NotFound("Ticket.FacilityNotFound", "Facility was not found.");

    public static readonly Error AgreementNotFound =
        Error.NotFound("Ticket.AgreementNotFound", "Rental agreement was not found or does not belong to specified customer/facility.");

    public static readonly Error StorageUnitNotFound =
        Error.NotFound("Ticket.StorageUnitNotFound", "Storage unit was not found in this facility.");

    public static readonly Error StorageUnitNotBelongToAgreement =
        Error.Validation("Ticket.StorageUnitNotBelongToAgreement", "Storage unit does not belong to the specified rental agreement.");


    public static readonly Error TicketClosed =
        Error.Validation("Ticket.TicketClosed", "Cannot perform this action on a ticket that is closed or cancelled.");

    public static readonly Error InvalidRatingScore =
        Error.Validation("Ticket.InvalidRatingScore", "Rating score must be an integer between 1 and 5.");

    public static readonly Error TicketAlreadyRated =
        Error.Conflict("Ticket.AlreadyRated", "This support ticket has already been confirmed and rated.");

    public static readonly Error TicketNotResolved =
        Error.Validation("Ticket.NotResolved", "Ticket must be resolved by staff before customer can confirm and rate.");

    public static readonly Error EmptyMessage =
        Error.Validation("Ticket.EmptyMessage", "Message body cannot be empty.");
}
