import { describe, expect, it } from 'vitest';
import type { Category, Item } from '../catalog/types';
import { buildStockGroups, ingredientRef, itemRef, parseStockRef, stockRefFields } from './stockOptions';
import type { InventoryCategory, InventoryItem } from './types';

const item = (id: string, name: string, categoryId: string | null, isActive = true) => ({ id, name, categoryId, isActive, sku: null }) as Item;
const ingredient = (id: string, name: string, categoryId: string | null, over: Partial<InventoryItem> = {}) =>
  ({ id, name, categoryId, isActive: true, isAutoCreatedForItem: false, baseUnit: 'g', sku: null, ...over }) as InventoryItem;

describe('buildStockGroups', () => {
  const categories = [{ id: 'd', name: 'Drinks', sortOrder: 0 }] as Category[];
  const ingredientCategories: InventoryCategory[] = [{ id: 'dairy', name: 'Dairy', sortOrder: 0 }];

  it('groups items and ingredients by their own category, with uncategorised ones last in each kind', () => {
    const groups = buildStockGroups(
      [item('b', 'Brownie', null), item('l', 'Latte', 'd')],
      categories,
      [ingredient('m', 'Milk', 'dairy'), ingredient('s', 'Salt', null)],
      ingredientCategories,
    );
    expect(groups.map((g) => g.label)).toEqual(['Items · Drinks', 'Items · Uncategorised', 'Ingredients · Dairy', 'Ingredients · Uncategorised']);
    expect(groups[0].options.map((o) => o.value)).toEqual([itemRef('l')]);
    expect(groups[2].options[0]).toMatchObject({ value: ingredientRef('m'), hint: 'g' });
  });

  it('leaves out inactive entries and ingredients that only mirror an item', () => {
    const groups = buildStockGroups(
      [item('x', 'Old', null, false)],
      [],
      [ingredient('a', 'Latte', null, { isAutoCreatedForItem: true }), ingredient('i', 'Gone', null, { isActive: false })],
      [],
    );
    expect(groups).toEqual([]);
  });
});

describe('stock refs', () => {
  it('splits into the two request fields', () => {
    expect(stockRefFields('item:abc')).toEqual({ itemId: 'abc', inventoryItemId: null });
    expect(stockRefFields('ingredient:xyz')).toEqual({ itemId: null, inventoryItemId: 'xyz' });
    expect(stockRefFields('')).toEqual({ itemId: null, inventoryItemId: null });
  });

  it('rejects an unknown kind', () => {
    expect(parseStockRef('thing:1')).toBeNull();
  });
});
