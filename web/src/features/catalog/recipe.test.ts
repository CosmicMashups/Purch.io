import { describe, expect, it } from 'vitest';
import { buildRecipeLines, selectionFromRecipe } from './recipe';

describe('selectionFromRecipe', () => {
  it('maps lines to typed text, blank for availability-only', () => {
    expect(
      selectionFromRecipe([
        { inventoryItemId: 'a', inventoryItemName: 'Beans', quantityPerOrder: 18 },
        { inventoryItemId: 'b', inventoryItemName: 'Cup', quantityPerOrder: null },
      ]),
    ).toEqual({ a: '18', b: '' });
  });
});

describe('buildRecipeLines', () => {
  it('parses quantities and treats blank as null', () => {
    expect(buildRecipeLines({ a: '18.5', b: '  ' })).toEqual({
      ok: true,
      lines: [
        { inventoryItemId: 'a', quantityPerOrder: 18.5 },
        { inventoryItemId: 'b', quantityPerOrder: null },
      ],
    });
  });

  it('allows an empty recipe, which turns the item back into its own inventory item', () => {
    expect(buildRecipeLines({})).toEqual({ ok: true, lines: [] });
  });

  it('flags the first bad line', () => {
    expect(buildRecipeLines({ a: 'abc' })).toEqual({ ok: false, inventoryItemId: 'a', message: 'Enter a number' });
    expect(buildRecipeLines({ a: '-1' })).toEqual({ ok: false, inventoryItemId: 'a', message: 'Cannot be negative' });
  });

  it('accepts zero', () => {
    expect(buildRecipeLines({ a: '0' })).toEqual({ ok: true, lines: [{ inventoryItemId: 'a', quantityPerOrder: 0 }] });
  });
});
