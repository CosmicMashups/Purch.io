namespace Purch.Application.Onboarding;

/// <summary>OpenItemEnabled: null leaves the till's open-item (ring up something not in the catalog) setting as it is. ParkedCartTtlHours: null keeps the parked-cart keep-for window; 1..168 otherwise.</summary>
public sealed record UpdateBarcodeSettingRequest(bool RequiresBarcodePerItem, bool? OpenItemEnabled = null, int? ParkedCartTtlHours = null);
