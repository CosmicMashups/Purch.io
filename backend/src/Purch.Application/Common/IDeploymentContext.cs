using Purch.Domain.Enums;

namespace Purch.Application.Common;

/// <summary>
/// The single seam through which Cloud vs Local deployment mode is resolved.
/// Endpoints and application-layer code never branch on DeploymentMode directly —
/// only this abstraction does, keeping the config-only migration path (NFR17)
/// intact and extending it to cloud↔local, not just free-tier↔paid-tier.
/// </summary>
public interface IDeploymentContext
{
    DeploymentMode Mode { get; }

    string DatabaseConnectionString { get; }

    /// <summary>Supabase Storage URL (Cloud) or a local filesystem root path (Local).</summary>
    string StorageLocation { get; }

    /// <summary>Supabase Storage service key. Null in Local mode.</summary>
    string? StorageKey { get; }

    /// <summary>Supabase Storage bucket name. Null in Local mode.</summary>
    string? StorageBucket { get; }

    /// <summary>Supabase project URL for Auth (Cloud). Null when not configured, or in Local mode.</summary>
    string? IdentityUrl { get; }

    /// <summary>Supabase public (anon) key, used to check a password. Optional: the service key works too.</summary>
    string? IdentityAnonKey { get; }

    /// <summary>Supabase service-role key, used to create and update logins. Never leaves the API.</summary>
    string? IdentityServiceKey { get; }
}
