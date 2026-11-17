using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Domain.Entities;

public partial class Refund
{
    public long Id { get; set; }

    public long PaymentId { get; set; }

    public long? AgreementId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public string Reason { get; set; } = null!;

    public string Provider { get; set; } = null!;

    public string? ProviderRefundId { get; set; }

    public string IdempotencyKey { get; set; } = null!;

    public string Status { get; set; } = null!;

    public long? RequestedBy { get; set; }

    public DateTimeOffset? RefundedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    [NotMapped, JsonIgnore]
    public RefundStatus RefundStatus
    {
        get => EnumDbMapper.ParseRefundStatus(Status);
        set => Status = value.ToDbValue();
    }

    public bool CanTransitionTo(RefundStatus nextStatus) =>
        EnumDbMapper.TryParseRefundStatus(Status, out var current) &&
        DomainStateMachine.CanTransition(current, nextStatus);

    public void TransitionStatus(RefundStatus nextStatus, DateTimeOffset? utcNow = null)
    {
        if (!string.IsNullOrWhiteSpace(Status) && !CanTransitionTo(nextStatus))
        {
            throw new InvalidOperationException($"Invalid refund status transition from '{Status}' to '{nextStatus.ToDbValue()}'.");
        }

        var ts = utcNow ?? DateTimeOffset.UtcNow;
        Status = nextStatus.ToDbValue();
        if (nextStatus is RefundStatus.Succeeded or RefundStatus.Completed)
        {
            RefundedAt ??= ts;
        }

        UpdatedAt = ts;
    }

    public virtual RentalAgreement? Agreement { get; set; }

    public virtual Payment Payment { get; set; } = null!;

    public virtual RefundApproval? RefundApproval { get; set; }

    public virtual User? RequestedByNavigation { get; set; }
}
