using Purch.Application.Promotions;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

public static class PromoCodeEndpoints
{
    public static IEndpointRouteBuilder MapPromoCodeEndpoints(this IEndpointRouteBuilder app)
    {
        var promoManager = new[] { nameof(Role.Admin), nameof(Role.Manager) };

        // --- Promo codes (D4/FR12) — cashier-entered cart-level discount codes ---
        _ = app.MapGet("/promo-codes", async (
            IPromoCodeService promoCodeService,
            CancellationToken cancellationToken) =>
            Results.Ok(await promoCodeService.ListAsync(cancellationToken)))
            .RequireAuthorization();

        _ = app.MapPost("/promo-codes", async (
            CreatePromoCodeRequest request,
            IPromoCodeService promoCodeService,
            CancellationToken cancellationToken) =>
            Results.Ok(await promoCodeService.CreateAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(promoManager));

        // --- BOGO ("Buy 1 Take 1") rules — automatic, no code entry ---
        _ = app.MapGet("/promos/bogo", async (
            IBogoPromoRuleService bogoPromoRuleService,
            CancellationToken cancellationToken) =>
            Results.Ok(await bogoPromoRuleService.ListAsync(cancellationToken)))
            .RequireAuthorization();

        _ = app.MapPost("/promos/bogo", async (
            CreateBogoPromoRuleRequest request,
            IBogoPromoRuleService bogoPromoRuleService,
            CancellationToken cancellationToken) =>
            Results.Ok(await bogoPromoRuleService.CreateAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(promoManager));

        _ = app.MapPut("/promos/bogo/{id:guid}", async (
            Guid id,
            UpdateBogoPromoRuleRequest request,
            IBogoPromoRuleService bogoPromoRuleService,
            CancellationToken cancellationToken) =>
            Results.Ok(await bogoPromoRuleService.UpdateAsync(id, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(promoManager));

        // --- Combo bundle rules — automatic, no code entry ---
        _ = app.MapGet("/promos/combos", async (
            IComboPromoRuleService comboPromoRuleService,
            CancellationToken cancellationToken) =>
            Results.Ok(await comboPromoRuleService.ListAsync(cancellationToken)))
            .RequireAuthorization();

        _ = app.MapPost("/promos/combos", async (
            CreateComboPromoRuleRequest request,
            IComboPromoRuleService comboPromoRuleService,
            CancellationToken cancellationToken) =>
            Results.Ok(await comboPromoRuleService.CreateAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(promoManager));

        _ = app.MapPut("/promos/combos/{id:guid}", async (
            Guid id,
            UpdateComboPromoRuleRequest request,
            IComboPromoRuleService comboPromoRuleService,
            CancellationToken cancellationToken) =>
            Results.Ok(await comboPromoRuleService.UpdateAsync(id, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(promoManager));

        // --- Item discount rules — automatic, no code entry ---
        _ = app.MapGet("/promos/item-discounts", async (
            IItemDiscountPromoRuleService itemDiscountPromoRuleService,
            CancellationToken cancellationToken) =>
            Results.Ok(await itemDiscountPromoRuleService.ListAsync(cancellationToken)))
            .RequireAuthorization();

        _ = app.MapPost("/promos/item-discounts", async (
            CreateItemDiscountPromoRuleRequest request,
            IItemDiscountPromoRuleService itemDiscountPromoRuleService,
            CancellationToken cancellationToken) =>
            Results.Ok(await itemDiscountPromoRuleService.CreateAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(promoManager));

        _ = app.MapPut("/promos/item-discounts/{id:guid}", async (
            Guid id,
            UpdateItemDiscountPromoRuleRequest request,
            IItemDiscountPromoRuleService itemDiscountPromoRuleService,
            CancellationToken cancellationToken) =>
            Results.Ok(await itemDiscountPromoRuleService.UpdateAsync(id, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(promoManager));

        return app;
    }
}
