using Purch.Application.Catalog;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var catalogManager = new[] { nameof(Role.Admin), nameof(Role.Manager) };

        // --- Categories (B5) ---
        _ = app.MapGet("/categories", async (ICategoryService categoryService, CancellationToken cancellationToken) =>
            Results.Ok(await categoryService.ListAsync(cancellationToken))).RequireAuthorization();

        _ = app.MapPost("/categories", async (
            CreateCategoryRequest request,
            ICategoryService categoryService,
            CancellationToken cancellationToken) =>
            Results.Ok(await categoryService.CreateAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(catalogManager));

        _ = app.MapPut("/categories/{categoryId:guid}", async (
            Guid categoryId,
            UpdateCategoryRequest request,
            ICategoryService categoryService,
            CancellationToken cancellationToken) =>
            Results.Ok(await categoryService.UpdateAsync(categoryId, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(catalogManager));

        // --- Items (B1–B2 base form; pricing-type sub-resources land with their own screens) ---
        _ = app.MapGet("/items", async (IItemService itemService, CancellationToken cancellationToken) =>
            Results.Ok(await itemService.ListAsync(cancellationToken))).RequireAuthorization();

        _ = app.MapPost("/items", async (
            CreateItemRequest request,
            IItemService itemService,
            CancellationToken cancellationToken) =>
            Results.Ok(await itemService.CreateAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(catalogManager));

        _ = app.MapPut("/items/{itemId:guid}", async (
            Guid itemId,
            UpdateItemRequest request,
            IItemService itemService,
            CancellationToken cancellationToken) =>
            Results.Ok(await itemService.UpdateAsync(itemId, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(catalogManager));

        // --- Modifier groups (B5's other half) ---
        _ = app.MapGet("/modifier-groups", async (IModifierGroupService modifierGroupService, CancellationToken cancellationToken) =>
            Results.Ok(await modifierGroupService.ListAsync(cancellationToken))).RequireAuthorization();

        _ = app.MapPost("/modifier-groups", async (
            CreateModifierGroupRequest request,
            IModifierGroupService modifierGroupService,
            CancellationToken cancellationToken) =>
            Results.Ok(await modifierGroupService.CreateAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(catalogManager));

        _ = app.MapPost("/modifier-groups/{groupId:guid}/modifiers", async (
            Guid groupId,
            CreateItemModifierRequest request,
            IModifierGroupService modifierGroupService,
            CancellationToken cancellationToken) =>
            Results.Ok(await modifierGroupService.AddModifierAsync(groupId, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(catalogManager));

        // --- Weight/volume batches (B2a) ---
        _ = app.MapGet("/items/{itemId:guid}/batches", async (
            Guid itemId,
            IItemBatchService itemBatchService,
            CancellationToken cancellationToken) =>
            Results.Ok(await itemBatchService.ListForItemAsync(itemId, cancellationToken))).RequireAuthorization();

        _ = app.MapPost("/items/{itemId:guid}/batches", async (
            Guid itemId,
            CreateItemBatchRequest request,
            IItemBatchService itemBatchService,
            CancellationToken cancellationToken) =>
            Results.Ok(await itemBatchService.ReceiveAsync(itemId, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(catalogManager));

        return app;
    }
}
