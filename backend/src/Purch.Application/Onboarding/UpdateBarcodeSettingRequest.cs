namespace Purch.Application.Onboarding;

/// <summary>OpenItemEnabled: null leaves the till's open-item (ring up something not in the catalog) setting as it is.</summary>
public sealed record UpdateBarcodeSettingRequest(bool RequiresBarcodePerItem, bool? OpenItemEnabled = null);
