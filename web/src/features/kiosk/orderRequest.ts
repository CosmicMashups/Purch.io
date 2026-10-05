import type { AddLineRequest } from '../pos/types';
import type { PlaceKioskOrderRequest } from './api';
import type { Checkout } from './kioskStore';
import type { LocalCartLine } from './localCart';

/** The cart as the lines the server prices. A combo's picks go along; fixed parts the server fills in itself are echoed harmlessly. */
export function toRequestLines(lines: LocalCartLine[]): AddLineRequest[] {
  return lines.map((line) => ({
    itemId: line.itemId,
    itemVariantId: line.itemVariantId,
    quantity: line.quantity,
    ...(line.comboSelections.length > 0 ? { comboSelections: line.comboSelections.map((c) => ({ slotId: c.slotId, selectedItemId: c.selectedItemId })) } : {}),
    ...(line.modifierSelections.some((m) => m.itemModifierId) ? { selectedModifierIds: line.modifierSelections.flatMap((m) => (m.itemModifierId ? [m.itemModifierId] : [])) } : {}),
    ...(line.modifierSelections.some((m) => m.itemId) ? { selectedCategoryItemIds: line.modifierSelections.flatMap((m) => (m.itemId ? [m.itemId] : [])) } : {}),
  }));
}

/** Everything that decides what the server would build, so a changed order never reuses the previous order's id. */
export function orderFingerprint(lines: LocalCartLine[], checkout: Checkout): string {
  return JSON.stringify({ lines: toRequestLines(lines), type: checkout.orderType, payment: checkout.payment, hint: checkout.discountHint });
}

export function buildPlaceOrderRequest(orderId: string, lines: LocalCartLine[], checkout: Checkout): PlaceKioskOrderRequest {
  return {
    orderId,
    lines: toRequestLines(lines),
    orderType: checkout.orderType ?? '',
    paymentPreference: checkout.payment,
    discountHint: checkout.payment === 'discount' ? checkout.discountHint : null,
  };
}
