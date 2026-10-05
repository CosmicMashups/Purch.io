import { describe, expect, it } from 'vitest';
import { PricingType, TingiMode, type Item } from '../catalog/types';
import { addFlowFor, buildRailTiles, cashQuickAmounts, changePreview, filterItems, findByCode, groupItemsByCategory, OTHER_SECTION_ID, stockBadge, tenderCoversTotal } from './catalogView';

const item = (over: Partial<Item>): Item =>
  ({
    id: 'i',
    name: 'Latte',
    sku: null,
    barcode: null,
    categoryId: null,
    basePrice: 100,
    imageUrl: null,
    pricingType: PricingType.Unit,
    stockOnHand: 10,
    isActive: true,
    departmentId: null,
    tingiMode: TingiMode.None,
    packagedSize: null,
    tingiIncrementStep: null,
    tingiAllowedSizes: [],
    serviceDurationMinutes: null,
    lowStockThreshold: null,
    isOutOfStock: false,
    ...over,
  }) as Item;

describe('addFlowFor', () => {
  it('picks a flow from the pricing type', () => {
    expect(addFlowFor(item({}))).toBe('direct');
    expect(addFlowFor(item({ pricingType: PricingType.Service }))).toBe('direct');
    expect(addFlowFor(item({ pricingType: PricingType.Bundle }))).toBe('direct');
    expect(addFlowFor(item({ pricingType: PricingType.VariantMatrix }))).toBe('variant');
    expect(addFlowFor(item({ pricingType: PricingType.Combo }))).toBe('combo');
    expect(addFlowFor(item({ pricingType: PricingType.WeightVolume }))).toBe('weight');
  });

  it('treats any tingi mode as weight entry', () => {
    expect(addFlowFor(item({ tingiMode: TingiMode.Fixed }))).toBe('weight');
  });
});

describe('stockBadge', () => {
  it('trusts the server for out of stock and compares with the alert level for low', () => {
    expect(stockBadge(item({ isOutOfStock: true }))).toBe('out');
    expect(stockBadge(item({ stockOnHand: 3, lowStockThreshold: 5 }))).toBe('low');
    expect(stockBadge(item({ stockOnHand: 6, lowStockThreshold: 5 }))).toBeNull();
    expect(stockBadge(item({ stockOnHand: 1, lowStockThreshold: null }))).toBeNull();
  });
});

describe('filterItems', () => {
  const items = [
    item({ id: 'a', name: 'Iced Latte', categoryId: 'coffee', barcode: '4800001' }),
    item({ id: 'b', name: 'Cookie', categoryId: 'bakery', sku: 'CK-1' }),
    item({ id: 'c', name: 'Retired', categoryId: 'coffee', isActive: false }),
  ];

  it('never shows inactive items', () => {
    expect(filterItems(items, { categoryId: null, query: '' }).map((i) => i.id)).toEqual(['a', 'b']);
  });

  it('filters by category and by name, sku or barcode', () => {
    expect(filterItems(items, { categoryId: 'coffee', query: '' }).map((i) => i.id)).toEqual(['a']);
    expect(filterItems(items, { categoryId: null, query: 'ck-' }).map((i) => i.id)).toEqual(['b']);
    expect(filterItems(items, { categoryId: null, query: '48000' }).map((i) => i.id)).toEqual(['a']);
  });
});

describe('groupItemsByCategory', () => {
  const categories = [
    { id: 'bakery', name: 'Bakery', sortOrder: 2, imageUrl: null },
    { id: 'coffee', name: 'Coffee', sortOrder: 1, imageUrl: 'coffee.png' },
    { id: 'empty', name: 'Empty shelf', sortOrder: 3, imageUrl: null },
  ];
  const items = [
    item({ id: 'a', name: 'Iced Latte', categoryId: 'coffee' }),
    item({ id: 'b', name: 'Cookie', categoryId: 'bakery' }),
    item({ id: 'c', name: 'Mocha', categoryId: 'coffee' }),
    item({ id: 'd', name: 'Retired', categoryId: 'coffee', isActive: false }),
    item({ id: 'e', name: 'Mystery', categoryId: null }),
    item({ id: 'f', name: 'From a deleted category', categoryId: 'gone' }),
  ];

  it('makes one section per category in the shop order, with only active items, and no "All"', () => {
    const sections = groupItemsByCategory(items, categories);
    expect(sections.map((section) => [section.id, section.items.map((i) => i.id)])).toEqual([
      ['coffee', ['a', 'c']],
      ['bakery', ['b']],
      [OTHER_SECTION_ID, ['e', 'f']],
    ]);
    expect(sections.map((section) => section.name)).not.toContain('All');
  });

  it('leaves out a category with nothing in it, so there is no empty heading to scroll to', () => {
    expect(groupItemsByCategory(items, categories).map((section) => section.id)).not.toContain('empty');
  });

  it('keeps nothing sellable unreachable: uncategorised items land in a closing Other section', () => {
    const last = groupItemsByCategory(items, categories).at(-1);
    expect(last?.name).toBe('Other');
    expect(last?.imageUrl).toBeNull();
  });

  it('narrows every section at once for a search and drops the ones left empty', () => {
    const sections = groupItemsByCategory(items, categories, 'mo');
    expect(sections.map((section) => [section.id, section.items.map((i) => i.id)])).toEqual([['coffee', ['c']]]);
  });
});

describe('buildRailTiles', () => {
  const categories = [
    { id: 'coffee', name: 'Coffee', sortOrder: 1, imageUrl: 'coffee.png' },
    { id: 'bakery', name: 'Bakery', sortOrder: 2, imageUrl: null },
  ];

  it('has a tile per category with its picture and never an "All" tile', () => {
    const tiles = buildRailTiles(categories, groupItemsByCategory([item({ id: 'a', categoryId: 'coffee' }), item({ id: 'b', categoryId: 'bakery' })], categories));
    expect(tiles.map((tile) => [tile.label, tile.imageUrl, tile.disabled])).toEqual([
      ['Coffee', 'coffee.png', false],
      ['Bakery', null, false],
    ]);
  });

  it('dims the tile of a category with nothing to show and adds Other only when needed', () => {
    const onlyCoffee = groupItemsByCategory([item({ id: 'a', categoryId: 'coffee' }), item({ id: 'z', categoryId: null })], categories);
    const tiles = buildRailTiles(categories, onlyCoffee);
    expect(tiles.map((tile) => [tile.label, tile.disabled])).toEqual([
      ['Coffee', false],
      ['Bakery', true],
      ['Other', false],
    ]);
  });
});

describe('findByCode', () => {
  const items = [item({ id: 'a', barcode: '4800001' }), item({ id: 'b', sku: 'CK-1' }), item({ id: 'c', barcode: '999', isActive: false })];

  it('matches a barcode or SKU exactly', () => {
    expect(findByCode(items, ' 4800001 ')?.id).toBe('a');
    expect(findByCode(items, 'ck-1')?.id).toBe('b');
  });

  it('ignores partial, inactive and blank codes', () => {
    expect(findByCode(items, '48000')).toBeNull();
    expect(findByCode(items, '999')).toBeNull();
    expect(findByCode(items, '  ')).toBeNull();
  });
});

describe('cashQuickAmounts', () => {
  it('offers exact, the next hundred and the next bills above the total', () => {
    expect(cashQuickAmounts(137.5)).toEqual([
      { label: 'Exact', amount: 137.5 },
      { label: 'Round up', amount: 200 },
      { label: '200', amount: 200 },
      { label: '500', amount: 500 },
      { label: '1000', amount: 1000 },
    ].filter((a, i, all) => all.findIndex((b) => b.amount === a.amount) === i));
  });

  it('does not repeat an amount and skips a round-up equal to the total', () => {
    const amounts = cashQuickAmounts(200).map((a) => a.amount);
    expect(new Set(amounts).size).toBe(amounts.length);
    expect(amounts[0]).toBe(200);
  });

  it('offers nothing for an empty cart', () => {
    expect(cashQuickAmounts(0)).toEqual([]);
  });
});

describe('tender helpers', () => {
  it('needs the tender to cover the total', () => {
    expect(tenderCoversTotal(100, 100)).toBe(true);
    expect(tenderCoversTotal(99.99, 100)).toBe(false);
    expect(tenderCoversTotal(NaN, 100)).toBe(false);
  });

  it('previews change without floating-point noise', () => {
    expect(changePreview(500, 137.6)).toBe(362.4);
    expect(changePreview(50, 100)).toBe(0);
  });
});
