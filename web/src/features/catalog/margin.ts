import { parseCostPrice } from './schemas';

const pesos = (cents: number) => `${cents < 0 ? '-' : ''}₱${(Math.abs(cents) / 100).toFixed(2)}`;

/** Read-only hint under price and cost. Integer-cents math so 0.1 - 0.07 never shows 0.030000000000000002. */
export function marginHint(price: number, costText: string): string {
  const cost = parseCostPrice(costText);
  if (!cost.ok || cost.value === null) return 'Cost unknown';
  if (!Number.isFinite(price) || price <= 0) return 'Set a price to see the margin';
  const priceCents = Math.round(price * 100);
  const costCents = Math.round(cost.value * 100);
  const margin = priceCents - costCents;
  return `Margin ${pesos(margin)} (${Math.round((margin * 100) / priceCents)}%)`;
}
