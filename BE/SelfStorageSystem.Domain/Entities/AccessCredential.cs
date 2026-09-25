using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class AccessCredential
{
    public long Id { get; set; }

    public long AgreementId { get; set; }

    public long? AuthorizedMemberId { get; set; }

    public string CredentialType { get; set; } = null!;

    public string? ExternalSecretRef { get; set; }

    public string? SecretDigest { get; set; }

    public string? DisplayHint { get; set; }

    public DateTimeOffset IssuedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public string Status { get; set; } = null!;

    public long? IssuedBy { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual ICollection<AccessEvent> AccessEvents { get; set; } = new List<AccessEvent>();

    public virtual RentalAgreement Agreement { get; set; } = null!;

    public virtual AuthorizedAccessMember? AuthorizedMember { get; set; }

    public virtual EmployeeProfile? IssuedByNavigation { get; set; }
}
