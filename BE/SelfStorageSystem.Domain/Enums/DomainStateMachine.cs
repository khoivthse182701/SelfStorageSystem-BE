namespace SelfStorageSystem.Domain.Enums;

/// <summary>
/// Centralized state machine transition guards for core domain aggregates.
/// Mirrors database transition constraints across Flows 1–7.
/// </summary>
public static class DomainStateMachine
{
    public static bool CanTransition(UnitStatus current, UnitStatus next) =>
        current == next || (current, next) switch
        {
            (UnitStatus.Available, UnitStatus.Reserved or UnitStatus.Occupied or UnitStatus.PendingInspection or UnitStatus.Cleaning or UnitStatus.Maintenance or UnitStatus.Damaged or UnitStatus.OutOfService) => true,
            (UnitStatus.Reserved, UnitStatus.Available or UnitStatus.Occupied or UnitStatus.Cleaning or UnitStatus.Maintenance or UnitStatus.Damaged or UnitStatus.OutOfService) => true,
            (UnitStatus.Occupied, UnitStatus.PendingInspection or UnitStatus.Cleaning or UnitStatus.Maintenance or UnitStatus.Damaged or UnitStatus.OutOfService or UnitStatus.Available) => true,
            (UnitStatus.PendingInspection, UnitStatus.Available or UnitStatus.Cleaning or UnitStatus.Maintenance or UnitStatus.Damaged or UnitStatus.OutOfService or UnitStatus.Occupied) => true,
            (UnitStatus.Cleaning, UnitStatus.Available or UnitStatus.PendingInspection or UnitStatus.Maintenance or UnitStatus.Damaged or UnitStatus.OutOfService or UnitStatus.Occupied) => true,
            (UnitStatus.Maintenance, UnitStatus.Available or UnitStatus.Cleaning or UnitStatus.PendingInspection or UnitStatus.Damaged or UnitStatus.OutOfService or UnitStatus.Occupied) => true,
            (UnitStatus.Damaged, UnitStatus.Maintenance or UnitStatus.Cleaning or UnitStatus.PendingInspection or UnitStatus.OutOfService or UnitStatus.Available) => true,
            (UnitStatus.OutOfService, UnitStatus.Maintenance or UnitStatus.Cleaning or UnitStatus.Damaged or UnitStatus.Available) => true,
            _ => false
        };

    public static bool CanTransition(ReservationStatus current, ReservationStatus next) =>
        current == next || (current, next) switch
        {
            (ReservationStatus.Pending, ReservationStatus.AwaitingDeposit or ReservationStatus.Confirmed or ReservationStatus.Cancelled or ReservationStatus.Expired) => true,
            (ReservationStatus.AwaitingDeposit, ReservationStatus.Confirmed or ReservationStatus.Cancelled or ReservationStatus.Expired) => true,
            (ReservationStatus.Confirmed, ReservationStatus.CheckedIn or ReservationStatus.Converted or ReservationStatus.Cancelled or ReservationStatus.Expired or ReservationStatus.NoShow) => true,
            (ReservationStatus.CheckedIn, ReservationStatus.Converted or ReservationStatus.Completed) => true,
            (ReservationStatus.Converted, ReservationStatus.Completed) => true,
            _ => false
        };

    public static bool CanTransition(ContractStatus current, ContractStatus next) =>
        current == next || (current, next) switch
        {
            (ContractStatus.Draft, ContractStatus.Scheduled or ContractStatus.Active or ContractStatus.Cancelled) => true,
            (ContractStatus.Scheduled, ContractStatus.Active or ContractStatus.Cancelled) => true,
            (ContractStatus.Active, ContractStatus.ExpiringSoon or ContractStatus.Extended or ContractStatus.Overdue or ContractStatus.MoveOutScheduled or ContractStatus.CheckoutPending or ContractStatus.Expired or ContractStatus.Terminated or ContractStatus.Cancelled) => true,
            (ContractStatus.ExpiringSoon, ContractStatus.Active or ContractStatus.Extended or ContractStatus.Overdue or ContractStatus.MoveOutScheduled or ContractStatus.CheckoutPending or ContractStatus.Expired or ContractStatus.Terminated) => true,
            (ContractStatus.Extended, ContractStatus.Active or ContractStatus.ExpiringSoon or ContractStatus.Overdue or ContractStatus.MoveOutScheduled or ContractStatus.CheckoutPending or ContractStatus.Expired or ContractStatus.Terminated) => true,
            (ContractStatus.Overdue, ContractStatus.Active or ContractStatus.Defaulted or ContractStatus.MoveOutScheduled or ContractStatus.Terminated) => true,
            (ContractStatus.Defaulted, ContractStatus.Terminated or ContractStatus.Completed or ContractStatus.Closed) => true,
            (ContractStatus.MoveOutScheduled, ContractStatus.CheckoutPending or ContractStatus.Active or ContractStatus.Completed) => true,
            (ContractStatus.CheckoutPending, ContractStatus.Active or ContractStatus.Completed or ContractStatus.Closed) => true,
            (ContractStatus.Expired, ContractStatus.Extended or ContractStatus.Completed or ContractStatus.Closed) => true,
            (ContractStatus.Terminated, ContractStatus.Completed or ContractStatus.Closed) => true,
            (ContractStatus.Completed, ContractStatus.Closed) => true,
            _ => false
        };

    public static bool CanTransition(PaymentStatus current, PaymentStatus next) =>
        current == next || (current, next) switch
        {
            (PaymentStatus.Initiated, PaymentStatus.Pending or PaymentStatus.Succeeded or PaymentStatus.Failed or PaymentStatus.Cancelled) => true,
            (PaymentStatus.Pending, PaymentStatus.Succeeded or PaymentStatus.Failed or PaymentStatus.Cancelled) => true,
            (PaymentStatus.Succeeded, PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded) => true,
            (PaymentStatus.PartiallyRefunded, PaymentStatus.Refunded) => true,
            _ => false
        };

    public static bool CanTransition(TicketStatus current, TicketStatus next) =>
        current == next || (current, next) switch
        {
            (TicketStatus.Open, TicketStatus.PendingManagerReview or TicketStatus.Assigned or TicketStatus.InProgress or TicketStatus.Cancelled) => true,
            (TicketStatus.PendingManagerReview, TicketStatus.Assigned or TicketStatus.InProgress or TicketStatus.SevereNeedRelocation or TicketStatus.Resolved or TicketStatus.Cancelled) => true,
            (TicketStatus.Assigned, TicketStatus.InProgress or TicketStatus.WaitingForCustomer or TicketStatus.WaitingForMaintenance or TicketStatus.SevereNeedRelocation or TicketStatus.Assessed or TicketStatus.Resolved or TicketStatus.Cancelled) => true,
            (TicketStatus.InProgress, TicketStatus.WaitingForCustomer or TicketStatus.WaitingForMaintenance or TicketStatus.SevereNeedRelocation or TicketStatus.Assessed or TicketStatus.Resolved or TicketStatus.Cancelled) => true,
            (TicketStatus.WaitingForCustomer, TicketStatus.InProgress or TicketStatus.Resolved or TicketStatus.Closed or TicketStatus.Cancelled) => true,
            (TicketStatus.WaitingForMaintenance, TicketStatus.InProgress or TicketStatus.Assessed or TicketStatus.Resolved or TicketStatus.Cancelled) => true,
            (TicketStatus.SevereNeedRelocation, TicketStatus.Assigned or TicketStatus.InProgress or TicketStatus.Assessed or TicketStatus.Resolved or TicketStatus.Closed) => true,
            (TicketStatus.Assessed, TicketStatus.InProgress or TicketStatus.Resolved or TicketStatus.Closed) => true,
            (TicketStatus.Resolved, TicketStatus.Closed or TicketStatus.InProgress) => true,
            _ => false
        };

    public static bool CanTransition(InvoiceStatus current, InvoiceStatus next) =>
        current == next || (current, next) switch
        {
            (InvoiceStatus.Draft, InvoiceStatus.Open or InvoiceStatus.Voided) => true,
            (InvoiceStatus.Open, InvoiceStatus.PartiallyPaid or InvoiceStatus.Paid or InvoiceStatus.Overdue or InvoiceStatus.Voided) => true,
            (InvoiceStatus.PartiallyPaid, InvoiceStatus.Open or InvoiceStatus.Paid or InvoiceStatus.Overdue or InvoiceStatus.Voided) => true,
            (InvoiceStatus.Overdue, InvoiceStatus.PartiallyPaid or InvoiceStatus.Paid or InvoiceStatus.Voided) => true,
            _ => false
        };

    public static bool CanTransition(RefundStatus current, RefundStatus next) =>
        current == next || (current, next) switch
        {
            (RefundStatus.Requested, RefundStatus.Pending or RefundStatus.Approved or RefundStatus.Rejected or RefundStatus.Processing or RefundStatus.PendingManagerPayout or RefundStatus.PendingCounterPayout) => true,
            (RefundStatus.Pending, RefundStatus.Approved or RefundStatus.Rejected or RefundStatus.Processing or RefundStatus.PendingManagerPayout or RefundStatus.PendingCounterPayout) => true,
            (RefundStatus.Approved, RefundStatus.Processing or RefundStatus.Succeeded or RefundStatus.Completed or RefundStatus.Failed or RefundStatus.PendingManagerPayout or RefundStatus.PendingCounterPayout) => true,
            (RefundStatus.PendingManagerPayout, RefundStatus.Processing or RefundStatus.Succeeded or RefundStatus.Completed or RefundStatus.Failed or RefundStatus.Rejected) => true,
            (RefundStatus.PendingCounterPayout, RefundStatus.Processing or RefundStatus.Succeeded or RefundStatus.Completed or RefundStatus.Failed or RefundStatus.Rejected) => true,
            (RefundStatus.Processing, RefundStatus.Succeeded or RefundStatus.Completed or RefundStatus.Failed) => true,
            (RefundStatus.Failed, RefundStatus.Processing or RefundStatus.PendingManagerPayout or RefundStatus.PendingCounterPayout) => true,
            _ => false
        };
}
