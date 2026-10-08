using Purch.Domain.Common;

namespace Purch.Domain.Entities;

public class Supplier : TenantScopedEntity, ISoftDeletable
{
    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public Guid? DeletedByUserId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Legacy free-text contact line, from before contacts were structured. No longer written.</summary>
    public string? ContactInfo { get; set; }

    public string? Specialization { get; set; }

    public string? Address { get; set; }

    /// <summary>Tax identification number. Optional.</summary>
    public string? Tin { get; set; }

    public string? Remarks { get; set; }

    /// <summary>The people to reach at this supplier. Stored as one JSON column.</summary>
    public List<SupplierContact> Contacts { get; set; } = [];

    public bool IsActive { get; set; } = true;
}

public class SupplierContact
{
    public string ContactPerson { get; set; } = string.Empty;

    /// <summary>Any of Call, Viber, Email, Facebook, Messenger, Landline, Others.</summary>
    public List<string> Modes { get; set; } = [];

    public List<string> Numbers { get; set; } = [];

    public List<string> Emails { get; set; } = [];
}
