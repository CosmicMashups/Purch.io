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
  /** Off hides every department screen, field and report. Mirrors Tenant.UseDepartmentTracking. */
  useDepartmentTracking: boolean;
  /** Receiving stock updates the item's cost price. */
  updateCostOnReceiving?: boolean;
  /** Lets the cashier ring up something not in the catalog. */
  openItemEnabled?: boolean;
  /** Hours a parked cart is kept before it is released, 1 to 168. */
  parkedCartTtlHours?: number;
}
