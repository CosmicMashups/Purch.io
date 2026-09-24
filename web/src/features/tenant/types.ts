/** Mirrors Purch.Application.Onboarding.TenantSettingsDto (BusinessType is an integer enum). */
export interface TenantSettings {
  id: string;
  name: string;
  businessType: number;
  brandingLogoUrl: string | null;
  brandingBackgroundColorHex: string | null;
  brandingAccentColorHex: string | null;
  brandingPrimaryTextColorHex: string | null;
  brandingSecondaryTextColorHex: string | null;
  brandingFontFamily: string | null;
  requiresBarcodePerItem: boolean;
  tin: string | null;
  registeredBusinessName: string | null;
  registeredAddress: string | null;
  creditLedgerRetentionDays: number | null;
  creditLedgerEnabled: boolean;
  kioskPosterImageUrl: string | null;
  useSeparateInventoryTracking: boolean;
}
