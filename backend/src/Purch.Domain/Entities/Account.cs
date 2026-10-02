using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>A person's identity across businesses: one email, one Supabase Auth user. Deliberately not tenant-scoped,
/// because sign-in happens before a business is known. What the person may do lives on <see cref="Membership"/>.
/// Email is only a login name (it is never verified), and permissions are never read from Supabase.</summary>
public class Account : Entity
{
    /// <summary>The user id in Supabase Auth (auth.users.id).</summary>
    public Guid SupabaseUserId { get; set; }

    /// <summary>Lower-cased and trimmed; globally unique.</summary>
    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
