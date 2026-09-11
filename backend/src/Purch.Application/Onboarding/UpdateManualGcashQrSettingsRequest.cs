namespace Purch.Application.Onboarding;

/// <summary>QrImageUrl is an already-hosted URL, not a file upload — same convention as branding's logo (see UpdateBrandingRequest).</summary>
public sealed record UpdateManualGcashQrSettingsRequest(
    string? QrImageUrl,
    string? AccountName,
    string? AccountNumber);
