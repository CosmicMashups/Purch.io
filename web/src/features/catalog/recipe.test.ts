import { describe, expect, it } from 'vitest';
import { buildRecipeLines, selectionFromRecipe } from './recipe';

describe('selectionFromRecipe', () => {
  it('marks a line with a quantity as used and a blank one as availability-only', () => {
    expect(
      selectionFromRecipe([
        { inventoryItemId: 'a', inventoryItemName: 'Beans', quantityPerOrder: 18 },
        { inventoryItemId: 'b', inventoryItemName: 'Dressing', quantityPerOrder: null },
      ]),
    ).toEqual({ a: { used: true, quantity: '18' }, b: { used: false, quantity: '' } });
  });
});

describe('buildRecipeLines', () => {
  it('sends the quantity for a used ingredient and null for an availability-only one', () => {
    expect(buildRecipeLines({ bun: { used: true, quantity: '1' }, dressing: { used: false, quantity: '' } })).toEqual({
      ok: true,
      lines: [
        { inventoryItemId: 'bun', quantityPerOrder: 1 },
        { inventoryItemId: 'dressing', quantityPerOrder: null },
      ],
    });
  });

  it('ignores whatever was typed once an ingredient is switched to availability-only', () => {
    expect(buildRecipeLines({ a: { used: false, quantity: '999' } })).toEqual({ ok: true, lines: [{ inventoryItemId: 'a', quantityPerOrder: null }] });
  });

  it('allows an empty recipe, which turns the item back into its own inventory item', () => {
    expect(buildRecipeLines({})).toEqual({ ok: true, lines: [] });
  });

  it('requires a quantity above zero for a used ingredient', () => {
    expect(buildRecipeLines({ a: { used: true, quantity: '  ' } })).toMatchObject({ ok: false, inventoryItemId: 'a', message: expect.stringMatching(/Enter how much/) });
    expect(buildRecipeLines({ a: { used: true, quantity: 'abc' } })).toMatchObject({ ok: false, message: 'Enter a number' });
    expect(buildRecipeLines({ a: { used: true, quantity: '-1' } })).toMatchObject({ ok: false, message: 'Cannot be negative' });
    expect(buildRecipeLines({ a: { used: true, quantity: '0' } })).toMatchObject({ ok: false, message: 'Must be more than 0' });
  });

  it('accepts a decimal quantity', () => {
    expect(buildRecipeLines({ a: { used: true, quantity: '18.5' } })).toEqual({ ok: true, lines: [{ inventoryItemId: 'a', quantityPerOrder: 18.5 }] });
  });
});
