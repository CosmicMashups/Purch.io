import { readBrandCache } from '../../theme/brandCache';
import { useTenantSettings } from '../tenant/queries';

/** The business's own name: from its settings, or the last copy this device cached while they were unreachable. */
export function useBusinessName(): string {
  const settings = useTenantSettings();
  return settings.data?.name ?? readBrandCache()?.businessName ?? '';
}
