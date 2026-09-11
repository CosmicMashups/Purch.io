namespace Purch.Application.Pos;

/// <summary>E4's fulfillment choice (e.g. "Dine In"/"Take Out") — deliberately a
/// free-form string, not a fixed enum, since neither spec doc enumerates the set
/// and different verticals may want different labels.</summary>
public sealed record SetOrderTypeRequest(string OrderType);
