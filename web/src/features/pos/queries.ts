import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuthStore } from '../../lib/authStore';
import { createAddQueue } from './addQueue';
import { posApi } from './api';
import { useCartAdds, type CartAdds } from './useCartAdds';
import type { AddLineRequest, RecordPaymentRequest, Transaction } from './types';

export const posKeys = { cart: ['pos', 'cart'] as const };

/** The device's open cart. Fetching creates one if none exists, so this is safe to call on entering Cashier. */
export const useCart = (enabled: boolean) => useQuery({ queryKey: posKeys.cart, queryFn: posApi.getCart, enabled, staleTime: 0, refetchOnWindowFocus: true });

/** Each cart call returns the freshly priced cart, so the cache is set from the response instead of refetching. */
function useCartMutation<TVars>(fn: (vars: TVars) => Promise<Transaction>) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (vars: TVars) => fn(vars),
    onSuccess: (cart) => qc.setQueryData(posKeys.cart, cart),
  });
}

export const useAddLine = () => useCartMutation((body: AddLineRequest) => posApi.addLine(body));
export const useUpdateLine = () =>
  useCartMutation(({ lineId, quantity }: { lineId: string; quantity: number }) => posApi.updateLine(lineId, quantity));
export const useRemoveLine = () => useCartMutation((lineId: string) => posApi.removeLine(lineId));
export const useApplyPromoCode = () => useCartMutation((code: string | null) => posApi.applyPromoCode(code));
export const useApplySeniorPwd = () => useCartMutation((apply: boolean) => posApi.applySeniorPwd(apply));
export const useSetOrderType = () => useCartMutation((orderType: string) => posApi.setOrderType(orderType));
export const useVoidCart = () => useCartMutation(() => posApi.voidCart());

/**
 * Paying completes the sale, so the returned transaction is the receipt. The old cart is dropped
 * from the cache so the next visit to Cashier fetches (and creates) a fresh open cart.
 */
export function usePay() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: RecordPaymentRequest) => posApi.pay(body),
    onSuccess: () => qc.removeQueries({ queryKey: posKeys.cart }),
  });
}

export const kioskKeys = { pending: (branchId: string) => ['pos', 'kiosk-pending', branchId] as const };

/** Orders customers placed at a kiosk that are waiting for a cashier. Refreshes on its own while the page is open. */
export const usePendingKioskOrders = (branchId: string | null) =>
  useQuery({
    queryKey: kioskKeys.pending(branchId ?? ''),
    queryFn: () => posApi.listKioskPending(branchId as string),
    enabled: branchId !== null,
    refetchInterval: 10_000,
  });

/** Claiming turns the kiosk order into this device's open cart, so the cart cache is set from the response. */
export function useClaimKioskOrder() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (transactionId: string) => posApi.claimKioskOrder(transactionId),
    onSuccess: async (cart) => {
      qc.setQueryData(posKeys.cart, cart);
      await qc.invalidateQueries({ queryKey: ['pos', 'kiosk-pending'] });
    },
  });
}

/** Adds to the register's cart. One queue for the whole app, so an add started on one screen finishes on any. */
export const posAddQueue = createAddQueue();

// Adds waiting for one session must never be sent under the next one.
useAuthStore.subscribe((state, previous) => {
  if (previous.accessToken && !state.accessToken) posAddQueue.reset();
});

export const usePosAdds = (): CartAdds => useCartAdds(posAddQueue, posApi.addLine, posKeys.cart);
