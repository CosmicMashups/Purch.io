using Purch.Application.EquipmentInventory;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

public static class EquipmentEndpoints
{
    public static IEndpointRouteBuilder MapEquipmentEndpoints(this IEndpointRouteBuilder app)
    {
        var inventoryManager = new[] { nameof(Role.Admin), nameof(Role.Manager), nameof(Role.Warehouse) };

        _ = app.MapGet("/equipment", async (
            IEquipmentService equipmentService,
            CancellationToken cancellationToken) =>
            Results.Ok(await equipmentService.ListAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/equipment", async (
            CreateEquipmentRequest request,
            IEquipmentService equipmentService,
            CancellationToken cancellationToken) =>
            Results.Ok(await equipmentService.CreateAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPut("/equipment/order", async (
            ReorderEquipmentRequest request,
            IEquipmentService equipmentService,
            CancellationToken cancellationToken) =>
            {
                await equipmentService.ReorderAsync(request, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPut("/equipment/{id:guid}", async (
            Guid id,
            UpdateEquipmentRequest request,
            IEquipmentService equipmentService,
            CancellationToken cancellationToken) =>
            Results.Ok(await equipmentService.UpdateAsync(id, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPut("/equipment/{id:guid}/status", async (
            Guid id,
            SetEquipmentStatusRequest request,
            IEquipmentService equipmentService,
            CancellationToken cancellationToken) =>
            Results.Ok(await equipmentService.SetStatusAsync(id, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        return app;
    }
}
