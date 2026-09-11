namespace Purch.Application.Onboarding;

/// <summary>
/// LogoUrl is an already-hosted URL, not a file upload — actual image upload
/// (to Supabase Storage / a local filesystem path per IDeploymentContext) is
/// a separate, more involved endpoint deferred for now. Font is in scope per
/// the full-spec build, but note it only reaches app screens and digital
/// receipts — a thermal printer's built-in font set can't render an arbitrary
/// custom font (see the plan's Key Architecture Decisions).
/// </summary>
public sealed record UpdateBrandingRequest(string? LogoUrl, string? ThemeColorHex, string? FontFamily);
