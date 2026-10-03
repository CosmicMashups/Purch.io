using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>Stands in for Supabase Auth's user table when the API runs in Local deployment mode (and in tests), where
/// there is no Supabase project to check a password against. Id is the "provider user id" that an <see cref="Account"/>
/// points at. Never used in Cloud mode.</summary>
public class LocalCredential : Entity
{
    /// <summary>Lower-cased and trimmed; unique.</summary>
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;
}
