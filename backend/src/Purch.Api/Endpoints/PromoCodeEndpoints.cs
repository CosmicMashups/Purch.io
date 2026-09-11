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

        return app;
    }
}
