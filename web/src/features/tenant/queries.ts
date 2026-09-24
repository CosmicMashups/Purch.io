import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSession } from '../auth/useSession';
import { tenantApi } from './api';

export const tenantKeys = { settings: ['tenant', 'settings'] as const };

/**
 * GET /tenant/settings is Admin-only, so for every other role this is disabled and `data` stays
 * undefined. Callers must treat "unknown" as "off", as the Flutter client does.
 */
export function useTenantSettings() {
  const { role } = useSession();
  return useQuery({
    queryKey: tenantKeys.settings,
    queryFn: () => tenantApi.get(),
    enabled: role === 'Admin',
    staleTime: 5 * 60_000,
  });
}

/** Every settings save returns the whole updated settings, which becomes the cached copy (and re-themes the app live). */
function useSettingsMutation<TVars>(fn: (vars: TVars) => ReturnType<typeof tenantApi.get>) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (vars: TVars) => fn(vars),
    onSuccess: (settings) => qc.setQueryData(tenantKeys.settings, settings),
  });
}

export const useUpdateBranding = () => useSettingsMutation((body: Parameters<typeof tenantApi.updateBranding>[0]) => tenantApi.updateBranding(body));
export const useUpdateBir = () => useSettingsMutation((body: Parameters<typeof tenantApi.updateBir>[0]) => tenantApi.updateBir(body));
export const useUpdateBarcode = () => useSettingsMutation((value: boolean) => tenantApi.updateBarcode(value));
export const useUpdateCreditLedger = () => useSettingsMutation((value: boolean) => tenantApi.updateCreditLedger(value));
export const useUpdateInventoryTracking = () => useSettingsMutation((value: boolean) => tenantApi.updateInventoryTracking(value));
