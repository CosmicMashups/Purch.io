namespace Purch.Application.Onboarding;

/// <summary>ISO 3166 alpha-2 country; it sets the currency and VAT rate used on purchase orders.</summary>
public sealed record UpdateCountryRequest(string Country);
