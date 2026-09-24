import { apiClient } from '../../lib/apiClient';
import type { TenantSettings } from './types';

export interface UpdateBrandingBody {
  logoUrl: string | null;
  backgroundColorHex: string | null;
  accentColorHex: string | null;
  primaryTextColorHex: string | null;
  secondaryTextColorHex: string | null;
  fontFamily: string | null;
  kioskPosterImageUrl: string | null;
}

export interface UpdateBirBody {
  tin: string | null;
  registeredBusinessName: string | null;
  registeredAddress: string | null;
  creditLedgerRetentionDays: number | null;
}

export const tenantApi = {
  get: () => apiClient.get<TenantSettings>('/tenant/settings').then((r) => r.data),
  updateBranding: (body: UpdateBrandingBody) => apiClient.put<TenantSettings>('/tenant/settings/branding', body).then((r) => r.data),
  updateBir: (body: UpdateBirBody) => apiClient.put<TenantSettings>('/tenant/settings/bir', body).then((r) => r.data),
  updateBarcode: (requiresBarcodePerItem: boolean) =>
    apiClient.put<TenantSettings>('/tenant/settings/barcode', { requiresBarcodePerItem }).then((r) => r.data),
  updateCreditLedger: (creditLedgerEnabled: boolean) =>
    apiClient.put<TenantSettings>('/tenant/settings/credit-ledger', { creditLedgerEnabled }).then((r) => r.data),
  updateInventoryTracking: (useSeparateInventoryTracking: boolean) =>
    apiClient.put<TenantSettings>('/tenant/settings/inventory-tracking', { useSeparateInventoryTracking }).then((r) => r.data),
};
