import { useTenantSettings } from '../features/tenant/queries';
import { readBrandCache } from './brandCache';

/**
 * The business's own logo, if it set one. Only an Admin can read tenant settings, so everyone else gets the last
 * logo an Admin loaded on this device (see G10). Null means "use the Purch.io logo".
 */
export function useBrandLogo(): string | null {
  const settings = useTenantSettings();
  return settings.data?.brandingLogoUrl ?? readBrandCache()?.logoUrl ?? null;
}
