using Purch.Domain.Enums;

namespace Purch.Application.Pos;

/// <summary>One existing line to return some or all of, at the price it was originally sold for.</summary>
public sealed record ReturnLineRequest(Guid OriginalLineId, decimal Quantity);

/// <summary>One item to take instead, priced at today's price. Only plain items and item variants are
/// supported for now — no combos, no weight/volume batch-tracked items — see AdjustmentService.</summary>
public sealed record ReplacementLineRequest(Guid ItemId, Guid? ItemVariantId, decimal Quantity);

/// <summary>
/// An exchange: some quantity of an already-completed sale's lines is returned and some different item(s)
/// are taken instead. Always needs an Admin/Manager's approval — see ApproverAuthorizationService. When the
/// return and replacement totals differ, <see cref="SettlementMethod"/> says how the difference was
/// settled (collecting more from the customer, or refunding them).
/// </summary>
public sealed record CreateExchangeRequest(
    IReadOnlyList<ReturnLineRequest> ReturnLines,
    IReadOnlyList<ReplacementLineRequest> ReplacementLines,
    string Reason,
    string? ApproverPin,
    PaymentMethod? SettlementMethod = null,
    decimal? SettlementAmountTendered = null);

public sealed record AdjustmentLineDto(
    Guid ItemId,
    string ItemName,
    Guid? ItemVariantId,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record AdjustmentDto(
    Guid Id,
    Guid OriginalTransactionId,
    long? OriginalReceiptNumber,
    DateTimeOffset CreatedAt,
    string Reason,
    Guid ApprovedByUserId,
    string ApprovedByName,
    IReadOnlyList<AdjustmentLineDto> ReturnLines,
    IReadOnlyList<AdjustmentLineDto> ReplacementLines,
    decimal ReturnedTotal,
    decimal ReplacementTotal,
    decimal PriceDifference,
    PaymentMethod? SettlementMethod,
    decimal? ChangeGiven);

/// <summary>How much of one original sale line is still available to return.</summary>
public sealed record ReturnableLineDto(Guid LineId, decimal RemainingQuantity);
