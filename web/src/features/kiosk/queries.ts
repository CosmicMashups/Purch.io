import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { displayApi, kioskApi } from './api';
import type { AddLineRequest, KitchenStatus, Transaction } from '../pos/types';

export const kioskKeys = {
  cart: ['kiosk', 'cart'] as const,
  branding: ['kiosk', 'branding'] as const,
  display: (role: string, branchId: string) => ['display', role, branchId] as const,
};

export const useKioskCart = () => useQuery({ queryKey: kioskKeys.cart, queryFn: kioskApi.getCart, staleTime: 0 });

/** The poster is decoration. If it cannot load the landing screen simply shows the wordmark. */
export const useKioskBranding = () => useQuery({ queryKey: kioskKeys.branding, queryFn: kioskApi.branding, retry: false, staleTime: 5 * 60_000 });

function useCartMutation<TVars>(fn: (vars: TVars) => Promise<Transaction>) {
  const qc = useQueryClient();
  return useMutation({ mutationFn: (vars: TVars) => fn(vars), onSuccess: (cart) => qc.setQueryData(kioskKeys.cart, cart) });
}

export const useKioskAddLine = () => useCartMutation((body: AddLineRequest) => kioskApi.addLine(body));
export const useKioskUpdateLine = () => useCartMutation(({ lineId, quantity }: { lineId: string; quantity: number }) => kioskApi.updateLine(lineId, quantity));
export const useKioskRemoveLine = () => useCartMutation((lineId: string) => kioskApi.removeLine(lineId));

/**
 * Choosing how to receive the order is what sends it: the type is set, then the order is submitted.
 * The submitted order is the confirmation; the next customer gets a fresh cart.
 */
export function useSubmitKioskOrder() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (orderType: string) => {
      await kioskApi.setOrderType(orderType);
      return kioskApi.submit();
    },
    onSuccess: () => qc.removeQueries({ queryKey: kioskKeys.cart }),
  });
}

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
