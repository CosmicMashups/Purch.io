using Purch.Application.CreditLedger;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

public static class CreditLedgerEndpoints
{
    public static IEndpointRouteBuilder MapCreditLedgerEndpoints(this IEndpointRouteBuilder app)
    {
        var manager = new[] { nameof(Role.Admin), nameof(Role.Manager) };
        // A cashier looks up/records against a customer's account at checkout
        // time, same posOperator set PosEndpoints uses — Warehouse has no
        // reason to touch a customer credit account.
        var posOperator = new[] { nameof(Role.Admin), nameof(Role.Manager), nameof(Role.Cashier) };

        _ = app.MapGet("/credit-ledger", async (
            ICustomerCreditLedgerService creditLedgerService,
            CancellationToken cancellationToken) =>
            Results.Ok(await creditLedgerService.ListAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        _ = app.MapPost("/credit-ledger", async (
            CreateCustomerCreditLedgerRequest request,
            ICustomerCreditLedgerService creditLedgerService,
            CancellationToken cancellationToken) =>
            Results.Ok(await creditLedgerService.CreateAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(manager));

        _ = app.MapPost("/credit-ledger/{ledgerId:guid}/payments", async (
            Guid ledgerId,
            RecordCreditPaymentRequest request,
            ICustomerCreditLedgerService creditLedgerService,
            CancellationToken cancellationToken) =>
            Results.Ok(await creditLedgerService.RecordPaymentAsync(ledgerId, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        _ = app.MapGet("/credit-ledger/reminders", async (
            int? withinDays,
            ICustomerCreditLedgerService creditLedgerService,
            CancellationToken cancellationToken) =>
            Results.Ok(await creditLedgerService.ListRemindersAsync(withinDays ?? 7, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(manager));

        return app;
    }
}
