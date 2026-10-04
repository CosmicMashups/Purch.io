export type StockScope = 'items' | 'ingredients';

export const STOCK_SCOPE_TABS = [
  { id: 'items', label: 'Items' },
  { id: 'ingredients', label: 'Ingredients' },
] as const;
