namespace Purch.Application.Pos;

/// <summary>
/// Several cart adds in one call, applied together or not at all. Carries no prices: like every cart call, each line
/// names only the item, variant, quantity and choices, and the server prices it from its own database.
/// </summary>
/// <param name="BatchId">Client-generated idempotency key, new for each batch. Sending the same id again, a retry after
/// a lost response, adds nothing a second time and answers with the cart as it stands.</param>
/// <param name="Lines">The adds, in the order the cashier tapped them (at most 50).</param>
public sealed record AddLinesBatchRequest(Guid BatchId, IReadOnlyList<AddTransactionLineRequest> Lines);
