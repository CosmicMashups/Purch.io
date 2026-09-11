using Microsoft.Extensions.Configuration;
using Purch.Domain.Enums;

namespace Purch.Infrastructure.Deployment;

/// <summary>
/// Reads PURCH_DEPLOYMENT_MODE (and mode-specific connection/storage settings) once
/// at startup from configuration (appsettings.json / env vars) — not per-request.
/// DeploymentMode is set directly by the developer per installation; there is no
/// customer-facing activation flow (see docs/adr on licensing removal).
/// </summary>
public sealed class ConfigDeploymentContext : IDeploymentContext
{
    public ConfigDeploymentContext(IConfiguration configuration)
    {
        var modeValue = configuration["PURCH_DEPLOYMENT_MODE"] ?? nameof(DeploymentMode.Cloud);
        Mode = Enum.Parse<DeploymentMode>(modeValue, ignoreCase: true);

        (DatabaseConnectionString, StorageLocation) = Mode switch
        {
            DeploymentMode.Local => (
                configuration["LOCAL_DB_CONNECTION_STRING"]
                    ?? throw new InvalidOperationException("LOCAL_DB_CONNECTION_STRING is required when PURCH_DEPLOYMENT_MODE=Local."),
                configuration["LOCAL_STORAGE_PATH"]
                    ?? throw new InvalidOperationException("LOCAL_STORAGE_PATH is required when PURCH_DEPLOYMENT_MODE=Local.")),
            DeploymentMode.Cloud => (
                configuration["SUPABASE_DB_CONNECTION_STRING"]
                    ?? throw new InvalidOperationException("SUPABASE_DB_CONNECTION_STRING is required when PURCH_DEPLOYMENT_MODE=Cloud."),
                configuration["SUPABASE_STORAGE_URL"]
                    ?? throw new InvalidOperationException("SUPABASE_STORAGE_URL is required when PURCH_DEPLOYMENT_MODE=Cloud.")),
            _ => throw new InvalidOperationException($"Unhandled deployment mode: {Mode}"),
        };
    }

    public DeploymentMode Mode { get; }

    public string DatabaseConnectionString { get; }

    public string StorageLocation { get; }
}
