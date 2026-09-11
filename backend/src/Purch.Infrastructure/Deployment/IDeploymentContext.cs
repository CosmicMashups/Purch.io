using Purch.Domain.Enums;

namespace Purch.Infrastructure.Deployment;

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

    /// <summary>Supabase Storage URL/key (Cloud) or a local filesystem root path (Local).</summary>
    string StorageLocation { get; }
}
