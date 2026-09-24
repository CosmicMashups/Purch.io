import { KitchenStatus, type Transaction } from '../pos/types';

export const ORDER_TYPES = ['Dine In', 'Take Out'] as const;
export type OrderType = (typeof ORDER_TYPES)[number];

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
  return [
    ...(variant ? [variant] : []),
    ...line.comboSelections.map((c) => `${c.slotLabel}: ${c.selectedItemName}`),
    ...line.modifierSelections.map((m) => m.modifierName),
  ];
}
