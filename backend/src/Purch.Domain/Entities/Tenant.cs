using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

public class Tenant : Entity
{
    public string Name { get; set; } = string.Empty;

    public BusinessType BusinessType { get; set; }

    public DeploymentMode DeploymentMode { get; set; }

    public string? BrandingLogoUrl { get; set; }

    /// <summary>App background colour (hex, e.g. #F8FAFC). Null = the client's built-in default.</summary>
    public string? BrandingBackgroundColorHex { get; set; }

    /// <summary>Accent / primary action colour (hex). Null = the client's built-in default.</summary>
    public string? BrandingAccentColorHex { get; set; }

    /// <summary>Primary text colour (hex). Null = the client's built-in default.</summary>
    public string? BrandingPrimaryTextColorHex { get; set; }

    /// <summary>Secondary text colour (hex). Null = the client's built-in default.</summary>
    public string? BrandingSecondaryTextColorHex { get; set; }

    public string? BrandingFontFamily { get; set; }

    public string FeatureFlagsJson { get; set; } = "{}";

    /// <summary>Schema-only hook, unread by any gate in v1 — see docs/adr.</summary>
    public LicenseStatus LicenseStatus { get; set; } = LicenseStatus.Trial;

    public bool RequiresBarcodePerItem { get; set; }

    // --- BIR / compliance settings (A5) ---
    public string? Tin { get; set; }

    public string? RegisteredBusinessName { get; set; }

    public string? RegisteredAddress { get; set; }

    // --- Utang ledger retention (NFR10) ---
    public int? CreditLedgerRetentionDays { get; set; }

    /// <summary>B7 — whether this shop offers "utang"/credit sales at all. Off by default: credit limit checks and the ledger UI stay hidden until an admin opts in.</summary>
    public bool CreditLedgerEnabled { get; set; }

    /// <summary>Kiosk landing screen promotional poster — a URL to a hosted image displayed full-bleed as the hero on the customer-facing kiosk (E1). Null falls back to the wordmark card.</summary>
    public string? KioskPosterImageUrl { get; set; }
}
