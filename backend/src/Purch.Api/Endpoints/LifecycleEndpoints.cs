using System.Security.Claims;
using Purch.Application.Lifecycle;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

/// <summary>Deactivate, reactivate, soft-delete and restore for every record kind. LifecycleService decides, per kind,
/// which roles may do which; the route only requires a back-office role.</summary>
public static class LifecycleEndpoints
{
    public static IEndpointRouteBuilder MapLifecycleEndpoints(this IEndpointRouteBuilder app)
    {
        var backOffice = new[] { nameof(Role.Admin), nameof(Role.Manager), nameof(Role.Warehouse) };
        _ = app.MapGet("/lifecycle/{kind}/deleted", async (LifecycleKind kind, ClaimsPrincipal user, ILifecycleService service, CancellationToken ct) =>
            Results.Ok(await service.ListDeletedAsync(kind, RolesOf(user), ct)))
            .RequireAuthorization(policy => policy.RequireRole(backOffice));

        var group = app.MapGroup("/lifecycle/{kind}/{id:guid}")
            .RequireAuthorization(policy => policy.RequireRole(backOffice));

        _ = group.MapGet("/impact", async (LifecycleKind kind, Guid id, ClaimsPrincipal user, ILifecycleService service, CancellationToken ct) =>
            Results.Ok(await service.GetImpactAsync(kind, id, RolesOf(user), ct)));
        _ = group.MapPost("/deactivate", async (LifecycleKind kind, Guid id, ClaimsPrincipal user, ILifecycleService service, CancellationToken ct) =>
            Results.Ok(await service.DeactivateAsync(kind, id, RolesOf(user), ct)));
        _ = group.MapPost("/reactivate", async (LifecycleKind kind, Guid id, ClaimsPrincipal user, ILifecycleService service, CancellationToken ct) =>
            Results.Ok(await service.ReactivateAsync(kind, id, RolesOf(user), ct)));
        _ = group.MapPost("/delete", async (LifecycleKind kind, Guid id, ClaimsPrincipal user, ILifecycleService service, CancellationToken ct) =>
            Results.Ok(await service.DeleteAsync(kind, id, RolesOf(user), ct)));
        _ = group.MapPost("/restore", async (LifecycleKind kind, Guid id, ClaimsPrincipal user, ILifecycleService service, CancellationToken ct) =>
            Results.Ok(await service.RestoreAsync(kind, id, RolesOf(user), ct)));

        return app;
    }

    private static HashSet<string> RolesOf(ClaimsPrincipal user)
    {
        return [.. Enum.GetNames<Role>().Where(user.IsInRole)];
    }
}
