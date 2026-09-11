namespace Purch.Domain.Enums;

/// <summary>
/// Developer-configured per installation (appsettings/env var) at provisioning time.
/// Not a customer-facing or self-activated concept — see docs/adr for the reasoning.
/// </summary>
public enum DeploymentMode
{
    Cloud,
    Local,
}
