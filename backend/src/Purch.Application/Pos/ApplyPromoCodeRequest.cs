namespace Purch.Application.Pos;

/// <summary>A null or blank Code clears whatever promo is currently applied.</summary>
public sealed record ApplyPromoCodeRequest(string? Code);
