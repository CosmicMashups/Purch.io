import { PricingType, type Item, type ItemVariant, type ModifierGroup } from '../catalog/types';
import { SENIOR_PWD_DISCOUNT_RATE } from './pricing/pricingEngine';
import type { AddPreview, PendingRow } from './addQueue';
import { groupOptions } from './options';
import type { AddLineRequest, Transaction } from './types';

/**
 * Local-first display for the register. The server still prices and owns the cart; these helpers only let the
 * screen show a price the moment a tap happens, from catalog data already on the device, instead of showing
 * "Adding..." until the server answers. Whatever the device works out here is a preview: the server's answer
 * replaces it, Charge waits until every add has landed, and the Charge screen always shows the server's total.
 */

/**
 * The price the device expects for one unit of this add, or undefined when it cannot say (a combo, whose slots
 * carry upcharges, or a variant or modifier the device has not loaded). Never guesses: an unknown means the
 * row keeps its "Adding..." look until the server answers.
 */
export function previewFor(
  item: Item,
  request: AddLineRequest,
  catalog: { variants?: ItemVariant[]; modifierGroups?: ModifierGroup[] },
): AddPreview | undefined {
  if (item.pricingType === PricingType.Combo || request.comboSelections?.length) return undefined;

  let unitPrice = item.basePrice;
  const details: string[] = [];

  if (request.itemVariantId) {
    const variant = catalog.variants?.find((v) => v.id === request.itemVariantId);
    if (!variant) return undefined;
    unitPrice = variant.priceOverride ?? item.basePrice;
    details.push(...Object.values(variant.attributes));
  }

  const pickedIds = [...(request.selectedModifierIds ?? []), ...(request.selectedCategoryItemIds ?? [])];
  if (pickedIds.length > 0) {
    const known = new Map((catalog.modifierGroups ?? []).flatMap((g) => groupOptions(g)).map((o) => [o.id, o] as const));
    for (const id of pickedIds) {
      const option = known.get(id);
      if (!option) return undefined;
      unitPrice += option.priceDelta;
      details.push(option.name);
    }
  }

  return { itemId: item.id, unitPrice, details };
}

const round2 = (value: number) => Math.round(value * 100) / 100;

/**
 * The cart as the screen shows it while adds are still on their way: the server's last answer plus the pending
 * adds the device could price. Promotions already on the cart are kept as the server worked them out and are not
 * recomputed for the pending lines, so the figure can move a little when the server's answer lands.
 */
export function withPending(cart: Transaction, pending: PendingRow[]): Transaction {
  const added = pending.reduce((sum, row) => sum + (row.unitPrice === undefined ? 0 : row.unitPrice * row.quantity), 0);
  if (added === 0) return cart;

  const seniorShare = cart.seniorPwdDiscountApplied ? round2(added * SENIOR_PWD_DISCOUNT_RATE) : 0;
  return {
    ...cart,
    subtotal: round2(cart.subtotal + added),
    discountAmount: round2(cart.discountAmount + seniorShare),
    totalAmount: round2(cart.totalAmount + added - seniorShare),
  };
}
