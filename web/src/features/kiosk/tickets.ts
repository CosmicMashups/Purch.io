import { KitchenStatus, type Transaction } from '../pos/types';

export const ORDER_TYPES = ['Dine In', 'Take Out'] as const;
export type OrderType = (typeof ORDER_TYPES)[number];

/** How the customer says they will pay at the counter. The cashier still takes the real payment. */
export const KIOSK_PAYMENTS = ['cash', 'card', 'ewallet', 'discount'] as const;
export type KioskPayment = (typeof KIOSK_PAYMENTS)[number];

export const PAYMENT_LABEL: Record<KioskPayment, string> = {
  cash: 'Cash',
  card: 'Credit / Debit Card',
  ewallet: 'E-Wallet (GCash / Maya)',
  discount: 'With Discounts (Senior, PWD, Others)',
};

/** Which discount the customer will ask for when they choose "With Discounts". */
export const DISCOUNT_HINTS = ['senior', 'pwd', 'other'] as const;
export type DiscountHint = (typeof DISCOUNT_HINTS)[number];

export const DISCOUNT_LABEL: Record<DiscountHint, string> = { senior: 'Senior Citizen', pwd: 'PWD', other: 'Other discount' };

/** One short phrase for a chip or a receipt line: "Cash", or "With Discounts: PWD". Empty when nothing was chosen. */
export function paymentSummary(payment: string | null | undefined, hint?: string | null): string {
  if (!payment) return '';
  if (payment === 'discount') return hint && hint in DISCOUNT_LABEL ? `With Discounts: ${DISCOUNT_LABEL[hint as DiscountHint]}` : 'With Discounts';
  return PAYMENT_LABEL[payment as KioskPayment] ?? payment;
}

/** The status a kitchen ticket moves to when its button is pressed. Picked up is the end. */
export function nextKitchenStatus(status: KitchenStatus): KitchenStatus | null {
  switch (status) {
    case KitchenStatus.Queued:
      return KitchenStatus.Preparing;
    case KitchenStatus.Preparing:
      return KitchenStatus.Ready;
    case KitchenStatus.Ready:
      return KitchenStatus.PickedUp;
    default:
      return null;
  }
}

export function statusLabel(status: KitchenStatus): string {
  return ['Queued', 'Preparing', 'Ready', 'Picked up'][status] ?? 'Unknown';
}

export function actionLabel(status: KitchenStatus): string | null {
  switch (status) {
    case KitchenStatus.Queued:
      return 'Start preparing';
    case KitchenStatus.Preparing:
      return 'Mark ready';
    case KitchenStatus.Ready:
      return 'Picked up';
    default:
      return null;
  }
}

/** Oldest order first, so the kitchen works the queue in the order customers placed it. */
export function byPrepNumber(orders: Transaction[]): Transaction[] {
  return [...orders].sort((a, b) => (a.kioskPrepNumber ?? Number.MAX_SAFE_INTEGER) - (b.kioskPrepNumber ?? Number.MAX_SAFE_INTEGER));
}

/** The order board splits what customers wait for from what they can collect. */
export function splitBoard(orders: Transaction[]): { ready: Transaction[]; preparing: Transaction[] } {
  const sorted = byPrepNumber(orders);
  return {
    ready: sorted.filter((o) => o.kitchenStatus === KitchenStatus.Ready),
    preparing: sorted.filter((o) => o.kitchenStatus !== KitchenStatus.Ready && o.kitchenStatus !== KitchenStatus.PickedUp),
  };
}

/** The choices on a line that change how it is made: variant, combo picks and modifiers. */
export function lineDetails(line: Transaction['lines'][number]): string[] {
  const variant = Object.values(line.itemVariantAttributes ?? {}).join(', ');
  const all = [
    ...(variant ? [variant] : []),
    ...line.comboSelections.map((c) => `${c.slotLabel}: ${c.selectedItemName}`),
    ...line.modifierSelections.map((m) => m.modifierName),
  ];
  // "2 pcs Fried Chicken" is two picks of the same item: say it once, with a count.
  const counts = new Map<string, number>();
  for (const detail of all) counts.set(detail, (counts.get(detail) ?? 0) + 1);
  return [...counts].map(([detail, count]) => (count > 1 ? `${detail} x${count}` : detail));
}
