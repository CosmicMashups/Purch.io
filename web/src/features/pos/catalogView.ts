import { PricingType, TingiMode, type Item } from '../catalog/types';

/** How tapping an item adds it. The server prices whatever is added. */
export type AddFlow = 'direct' | 'variant' | 'combo' | 'weight';

export function addFlowFor(item: Pick<Item, 'pricingType' | 'tingiMode'>): AddFlow {
  if (item.pricingType === PricingType.VariantMatrix) return 'variant';
  if (item.pricingType === PricingType.Combo) return 'combo';
  if (item.pricingType === PricingType.WeightVolume || item.tingiMode !== TingiMode.None) return 'weight';
  return 'direct';
}

export type StockBadge = 'out' | 'low' | null;

/** `isOutOfStock` is the server's verdict. "Low" only compares stock with the item's own alert level. */
export function stockBadge(item: Pick<Item, 'isOutOfStock' | 'stockOnHand' | 'lowStockThreshold'>): StockBadge {
  if (item.isOutOfStock) return 'out';
  if (item.lowStockThreshold !== null && item.stockOnHand <= item.lowStockThreshold) return 'low';
  return null;
}

export interface CatalogFilter {
  categoryId: string | null;
  query: string;
}

/** Inactive items are never sold, so they never reach the grid. */
export function filterItems(items: Item[], { categoryId, query }: CatalogFilter): Item[] {
  const q = query.trim().toLowerCase();
  return items
    .filter((item) => item.isActive)
    .filter((item) => categoryId === null || item.categoryId === categoryId)
    .filter(
      (item) =>
        q === '' || item.name.toLowerCase().includes(q) || (item.sku?.toLowerCase().includes(q) ?? false) || (item.barcode?.toLowerCase().includes(q) ?? false),
    );
}

/** A scanner types the code and presses Enter, so only an exact barcode or SKU match counts. */
export function findByCode(items: Item[], code: string): Item | null {
  const wanted = code.trim().toLowerCase();
  if (wanted === '') return null;
  return items.find((item) => item.isActive && (item.barcode?.toLowerCase() === wanted || item.sku?.toLowerCase() === wanted)) ?? null;
}

export interface QuickAmount {
  label: string;
  amount: number;
}

const BILLS = [50, 100, 200, 500, 1000] as const;

/**
 * Tender shortcuts for the cash keypad. This is input convenience only: the server records what was
 * tendered and the change. Amounts are whole pesos plus the exact total.
 */
export function cashQuickAmounts(total: number): QuickAmount[] {
  if (!(total > 0)) return [];
  const amounts: QuickAmount[] = [{ label: 'Exact', amount: total }];
  const seen = new Set<number>([total]);
  const add = (label: string, amount: number) => {
    if (!seen.has(amount)) {
      seen.add(amount);
      amounts.push({ label, amount });
    }
  };
  add('Round up', Math.ceil(total / 100) * 100);
  for (const bill of BILLS.filter((b) => b > total).slice(0, 3)) add(`${bill}`, bill);
  return amounts;
}

/** Whether the tender can be confirmed: at least the total. */
export function tenderCoversTotal(tendered: number, total: number): boolean {
  return Number.isFinite(tendered) && tendered >= total;
}

/** A preview of the change to hand back. The server's `changeGiven` on the receipt is the record. */
export function changePreview(tendered: number, total: number): number {
  return tenderCoversTotal(tendered, total) ? Math.round((tendered - total) * 100) / 100 : 0;
}
