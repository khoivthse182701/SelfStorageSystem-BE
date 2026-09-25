using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class AuthorizedAccessMember
{
    public long Id { get; set; }

    public long AgreementId { get; set; }

    public string FullName { get; set; } = null!;

    public string? IdentityFingerprint { get; set; }

    public string? RelationshipToCustomer { get; set; }

    public DateTimeOffset ValidFrom { get; set; }

    public DateTimeOffset? ValidTo { get; set; }

    public string Status { get; set; } = null!;

    public long CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual ICollection<AccessCredential> AccessCredentials { get; set; } = new List<AccessCredential>();

    public virtual RentalAgreement Agreement { get; set; } = null!;

    public virtual User CreatedByNavigation { get; set; } = null!;
}
