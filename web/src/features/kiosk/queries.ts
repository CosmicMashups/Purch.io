import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { displayApi, kioskApi, type PlaceKioskOrderRequest } from './api';
import type { KitchenStatus } from '../pos/types';
import { toPricingRules } from './promoRules';

export const kioskKeys = {
  branding: ['kiosk', 'branding'] as const,
  promoRules: ['kiosk', 'promoRules'] as const,
  display: (role: string, branchId: string) => ['display', role, branchId] as const,
};

/** The poster is decoration. If it cannot load the landing screen simply shows the wordmark. */
export const useKioskBranding = () => useQuery({ queryKey: kioskKeys.branding, queryFn: kioskApi.branding, retry: false, staleTime: 5 * 60_000 });

/** The automatic promotions the kiosk's on-screen total uses. Without them the cart shows its pre-promo
 * total and the server's pricing at Submit is still what counts, so a failed load is silent and not retried. */
export const useKioskPromoRules = () =>
  useQuery({ queryKey: kioskKeys.promoRules, queryFn: () => kioskApi.promoRules().then(toPricingRules), retry: false, staleTime: 60_000, meta: { silent: true } });

/** Reports its own, friendlier failure message (see KioskProcessingPage), so the global toast is silenced. */
export const usePlaceKioskOrder = () =>
  useMutation({ mutationFn: ({ body, signal }: { body: PlaceKioskOrderRequest; signal?: AbortSignal }) => kioskApi.placeOrder(body, signal), meta: { silent: true } });

/** Both displays poll, because the API has no push channel. */
export const DISPLAY_POLL_MS = 5_000;

export const useDisplayOrders = (role: 'KitchenDisplay' | 'OrderBoard', branchId: string | null) =>
  useQuery({
    queryKey: kioskKeys.display(role, branchId ?? ''),
    queryFn: () => displayApi.pending(role, branchId as string),
    enabled: branchId !== null,
    refetchInterval: DISPLAY_POLL_MS,
    staleTime: 0,
  });

export function useSetKitchenStatus(branchId: string | null) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ transactionId, status }: { transactionId: string; status: KitchenStatus }) => displayApi.setKitchenStatus(transactionId, status),
    onSuccess: () => qc.invalidateQueries({ queryKey: kioskKeys.display('KitchenDisplay', branchId ?? '') }),
  });
}
