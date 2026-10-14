using Purch.Application.Auth;

namespace Purch.Api.Auth;

public static class PermissionPolicyExtensions
{
    /// <summary>Requires a permission key; resolved to roles via <see cref="Permissions.RolesFor"/>.</summary>
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string key)
    {
        var roles = Permissions.RolesFor(key).ToArray();
        return builder.RequireAuthorization(policy => policy.RequireRole(roles));
    }
}
