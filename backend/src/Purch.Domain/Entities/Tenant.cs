using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

public class Tenant : Entity
{
    public string Name { get; set; } = string.Empty;

    public BusinessType BusinessType { get; set; }

    public DeploymentMode DeploymentMode { get; set; }

    public string? BrandingLogoUrl { get; set; }

    public string? BrandingThemeColorHex { get; set; }

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
}
