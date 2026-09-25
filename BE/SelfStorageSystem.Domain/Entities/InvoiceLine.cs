using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class InvoiceLine
{
    public long Id { get; set; }

    public long InvoiceId { get; set; }

    public string LineType { get; set; } = null!;

    public string Description { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal? LineAmount { get; set; }

    public string Metadata { get; set; } = null!;

    public virtual Invoice Invoice { get; set; } = null!;
}
