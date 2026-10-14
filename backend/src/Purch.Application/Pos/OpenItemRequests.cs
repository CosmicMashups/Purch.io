namespace Purch.Application.Pos;

/// <summary>Rings up something that is not in the catalog: a name (1..80), a price above zero, a quantity above
/// zero and an optional note (up to 200). It becomes a line with no catalog item, no cost and no stock effect.</summary>
public sealed record AddOpenItemRequest(string Name, decimal UnitPrice, decimal Quantity, string? Note = null);

/// <summary>Sets or clears (blank) the free-text note on a cart line, up to 200 characters.</summary>
public sealed record SetLineNoteRequest(string? Note);
