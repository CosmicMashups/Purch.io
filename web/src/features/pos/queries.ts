import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuthStore } from '../../lib/authStore';
import { adaptiveListInterval } from '../../lib/pollInterval';
import { createAddQueue } from './addQueue';
import { posApi } from './api';
import { useCartAdds, type CartAdds } from './useCartAdds';
import type { AddLineRequest, ManualDiscountRequest, OpenItemRequest, OverridePriceRequest, RecordPaymentRequest, Transaction } from './types';

export const posKeys = { cart: ['pos', 'cart'] as const, parked: ['pos', 'parked'] as const };

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
export const useApplyPromoCode = () => useCartMutation((code: string | null) => posApi.applyPromoCode(code));
export const useApplySeniorPwd = () => useCartMutation((apply: boolean) => posApi.applySeniorPwd(apply));
export const useSetOrderType = () => useCartMutation((orderType: string) => posApi.setOrderType(orderType));
export const useSetLineDiscount = () =>
  useCartMutation((v: { lineId: string; body: ManualDiscountRequest }) => posApi.setLineDiscount(v.lineId, v.body));
export const useSetCartDiscount = () => useCartMutation((body: ManualDiscountRequest) => posApi.setCartDiscount(body));
export const useOverrideLinePrice = () =>
  useCartMutation((v: { lineId: string; body: OverridePriceRequest }) => posApi.overrideLinePrice(v.lineId, v.body));
export const useAddOpenItem = () => useCartMutation((body: OpenItemRequest) => posApi.addOpenItem(body));
export const useSetLineNote = () =>
  useCartMutation((v: { lineId: string; note: string | null; approverPin?: string }) => posApi.setLineNote(v.lineId, v.note, v.approverPin));

/** Carts set aside on this branch. */
export const useParkedCarts = (enabled = true) => useQuery({ queryKey: posKeys.parked, queryFn: posApi.listParked, enabled, staleTime: 0 });

/** Parking empties the register: drop the cart cache so the next visit fetches a fresh one. */
export function useParkCart() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (label?: string) => posApi.parkCart(label),
    onSuccess: async () => {
      qc.removeQueries({ queryKey: posKeys.cart });
      await qc.invalidateQueries({ queryKey: posKeys.parked });
    },
  });
}

/** Resuming makes the parked cart this register's open cart (changedLines rides on it). */
export function useResumeCart() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => posApi.resumeCart(id),
    onSuccess: async (cart) => {
      qc.setQueryData(posKeys.cart, cart);
      await qc.invalidateQueries({ queryKey: posKeys.parked });
    },
  });
}

export function useDiscardParked() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (v: { id: string; approverPin?: string }) => posApi.discardParked(v.id, v.approverPin),
    onSuccess: () => qc.invalidateQueries({ queryKey: posKeys.parked }),
  });
}

// Voiding, changing a line's quantity and removing a line all go through useApproverGatedAction instead of a
// plain mutation now: any of them may come back needing a manager/admin PIN, which a mutation's onSuccess/
// onError alone can't pause for and retry.

/**
 * Paying completes the sale, so the returned transaction is the receipt. The old cart is dropped
 * from the cache so the next visit to Cashier fetches (and creates) a fresh open cart.
 */
export function usePay() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: RecordPaymentRequest) => posApi.pay(body),
    onSuccess: () => {
      qc.removeQueries({ queryKey: posKeys.cart });
      // Today's revenue figures are stale the instant a sale completes.
      void qc.invalidateQueries({ queryKey: ['dashboard'] });
    },
    // A refused sale (prices or promos moved, or the cart changed) leaves the cart open: read it again so the screen shows the server's current total.
    onError: () => qc.invalidateQueries({ queryKey: posKeys.cart }),
  });
}

export const kioskKeys = { pending: (branchId: string) => ['pos', 'kiosk-pending', branchId] as const };

// 10 s while orders are waiting, 20 s once none have been for 5 minutes.
const kioskPendingInterval = adaptiveListInterval({ activeMs: 10_000, idleMs: 20_000, quietAfterMs: 5 * 60_000 });

/** Orders customers placed at a kiosk that are waiting for a cashier. Refreshes on its own while the page is open. */
export const usePendingKioskOrders = (branchId: string | null) =>
  useQuery({
    queryKey: kioskKeys.pending(branchId ?? ''),
    queryFn: () => posApi.listKioskPending(branchId as string),
    enabled: branchId !== null,
    refetchInterval: kioskPendingInterval,
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

export const usePosAdds = (): CartAdds => useCartAdds(posAddQueue, posApi.addLines, posKeys.cart);
