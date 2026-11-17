using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using SelfStorageSystem.Domain.Enums;

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

    public DateTimeOffset? NegativeDebtNotifiedAt { get; set; }

    public DateTimeOffset? OverdueBankReminderSentAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    [NotMapped, JsonIgnore]
    public ContractStatus ContractStatus
    {
        get => EnumDbMapper.ParseContractStatus(Status);
        set => Status = value.ToDbValue();
    }

    public bool CanTransitionTo(ContractStatus nextStatus) =>
        EnumDbMapper.TryParseContractStatus(Status, out var current) &&
        DomainStateMachine.CanTransition(current, nextStatus);

    public void TransitionStatus(ContractStatus nextStatus, DateTimeOffset? utcNow = null)
    {
        if (!string.IsNullOrWhiteSpace(Status) && !CanTransitionTo(nextStatus))
        {
            throw new InvalidOperationException($"Invalid contract status transition from '{Status}' to '{nextStatus.ToDbValue()}'.");
        }

        Status = nextStatus.ToDbValue();
        UpdatedAt = utcNow ?? DateTimeOffset.UtcNow;
    }

    public bool IsExpiringSoon(DateOnly facilityToday, int thresholdDays = 7) =>
        EnumDbMapper.TryParseContractStatus(Status, out var parsed) &&
        parsed is ContractStatus.Active or ContractStatus.Extended or ContractStatus.ExpiringSoon &&
        EndDate >= facilityToday &&
        EndDate.DayNumber - facilityToday.DayNumber <= thresholdDays;

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

    public virtual ICollection<UnitAllocation> UnitAllocations { get; set; } = new List<UnitAllocation>();

    public virtual ICollection<StoredItem> StoredItems { get; set; } = new List<StoredItem>();

    public virtual ICollection<UnitTransferRequest> UnitTransferRequests { get; set; } = new List<UnitTransferRequest>();
}
