namespace Purch.Application.Auth;

/// <summary>Key of a device's cached session state (see TenantResolutionMiddleware). Shared so whatever changes a
/// device row can evict exactly the entry the per-request check reads.</summary>
public static class DeviceSessionCacheKey
{
    public static string For(Guid tenantId, Guid deviceId) => $"device-session:{tenantId:N}:{deviceId:N}";
}
