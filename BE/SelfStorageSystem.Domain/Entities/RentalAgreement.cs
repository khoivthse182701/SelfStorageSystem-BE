using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class RentalAgreement
{
    public long Id { get; set; }

    public string AgreementNo { get; set; } = null!;

    public long ReservationId { get; set; }

    public long CustomerId { get; set; }

    public long FacilityId { get; set; }

    public long PolicyVersionId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public decimal MonthlyRateSnapshot { get; set; }

    public decimal DepositSnapshot { get; set; }

    public decimal DepositBalance { get; set; }

    public string Status { get; set; } = null!;

    public DateTimeOffset? SignedAt { get; set; }

    public DateTimeOffset? CheckedInAt { get; set; }

    public DateTimeOffset? CheckedOutAt { get; set; }

    public DateOnly? ActualEndDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public virtual ICollection<AccessCredential> AccessCredentials { get; set; } = new List<AccessCredential>();

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<AuthorizedAccessMember> AuthorizedAccessMembers { get; set; } = new List<AuthorizedAccessMember>();

    public virtual CustomerProfile Customer { get; set; } = null!;

    public virtual ICollection<DelinquencyCase> DelinquencyCases { get; set; } = new List<DelinquencyCase>();

    public virtual Facility Facility { get; set; } = null!;

    public virtual ICollection<HandoverRecord> HandoverRecords { get; set; } = new List<HandoverRecord>();

    public virtual ICollection<Inspection> Inspections { get; set; } = new List<Inspection>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual MoveOutRequest? MoveOutRequest { get; set; }

    public virtual PolicyVersion PolicyVersion { get; set; } = null!;

    public virtual ICollection<Refund> Refunds { get; set; } = new List<Refund>();

    public virtual RentalRenewal? RentalRenewal { get; set; }

    public virtual Reservation Reservation { get; set; } = null!;

    public virtual ICollection<SupportTicket> SupportTicketAgreements { get; set; } = new List<SupportTicket>();

    public virtual ICollection<SupportTicket> SupportTicketRentalAgreements { get; set; } = new List<SupportTicket>();

    public virtual ICollection<UnitAllocation> UnitAllocations { get; set; } = new List<UnitAllocation>();

    public virtual UnitTransferRequest? UnitTransferRequest { get; set; }
}
