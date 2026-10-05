import { PricingType, type Item } from '../catalog/types';

/** The most of one thing a single order may carry, whatever the stock. */
export const MAX_QUANTITY = 99;

export interface QuantityCap {
  max: number;
  /** True when stock, not the general limit, is what stops the customer, so the page can say "Only N left". */
  limitedByStock: boolean;
}

/**
 * How many of an item the customer can still order. When the business counts stock on the items themselves, that count
 * (less what the same item already has in this order, on other lines) is the limit. Services, combos and weighed items
 * have no count of their own, and when stock is tracked through ingredients the item's own count is meaningless, so the
 * server's sold-out flag is all there is to go on.
 */
export function quantityCap(item: Item, separateTracking: boolean | undefined, orderedElsewhere: number): QuantityCap {
  const hasOwnCount =
    separateTracking === false && item.pricingType !== PricingType.Service && item.pricingType !== PricingType.Combo && item.pricingType !== PricingType.WeightVolume;
  if (!hasOwnCount) return { max: MAX_QUANTITY, limitedByStock: false };

  const left = Math.max(0, Math.floor(item.stockOnHand) - orderedElsewhere);
  return left < MAX_QUANTITY ? { max: left, limitedByStock: true } : { max: MAX_QUANTITY, limitedByStock: false };
}
